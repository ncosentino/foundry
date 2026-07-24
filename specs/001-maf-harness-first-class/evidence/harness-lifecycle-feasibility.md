# G4 Lifecycle, Eager-Offload, and Partial-Commit Feasibility

**Issue:** #40 (`[Harness G4.1] Prove lifecycle and partial-commit feasibility`),
task T041. **Parent gate:** #20 / plan.md Group 4. **Downstream leaves:** #41
(T042-T044, T049), #42 (T045, T048, T051), #43 (T046-T047, T050, T052-T053),
#44 (T054-T056).

**Exact versions traced:** `Microsoft.Agents.AI` / `Microsoft.Agents.AI.Harness`
1.15.0, `Microsoft.Extensions.AI` / `Microsoft.Extensions.AI.Abstractions`
10.6.0, matching `src/Directory.Packages.props` on this branch. All MEAI claims
below were cross-checked against the exact installed 10.6.0 binaries
(`ilspycmd` decompilation of
`%USERPROFILE%\.nuget\packages\microsoft.extensions.ai\10.6.0\lib\net10.0\Microsoft.Extensions.AI.dll`
and `...\microsoft.extensions.ai.abstractions\10.6.0\lib\net10.0\...dll`) and
against the exact `v10.6.0`-tagged public source, and against a disposable
local runtime probe (built, run, and deleted during this task; no probe
artifacts remain in the repository). MAF claims reuse the `dotnet-1.15.0`
citations already established in T013's `uplift-delta.md` and re-verify the
specific per-service-history file referenced below.

This report settles every decision item required by issue #40. None of the
five items listed in the task are left open.

## Decisions (settled)

1. **One shared transform, wired to both existing loops.** G4 introduces
   exactly one internal tool-result transformation seam. It is wired into
   *both* call sites that currently turn a raw tool return value into
   LLM-facing message content: Foundry's own `IterativeAgentLoop` message-append
   point (`IterativeAgentLoop.cs:376-381`) and the selected provider's
   `HarnessProviderComposition`-composed `FunctionInvokingChatClient.FunctionInvoker`
   delegate (`HarnessProviderComposition.cs:308-320`). G5's compaction/
   preservation policy is layered **on top of** this seam later; it does not
   gate or postpone wiring basic offload into the selected-provider path now.
   Both call sites invoke the same seam with the same input contract (see
   "Shared transform contract" below) so their offload/inline decisions are
   identical for identical content.
2. **Byte-threshold policy, not token estimation.** The offload decision is
   driven by a required, explicit `MaximumInlineToolResultBytes` concept: the
   maximum allowed UTF-8 byte length of the string produced by the existing
   `ToolResultSerializer.Serialize(object?)` (`ToolResultSerializer.cs:18-43`).
   G4 never estimates or consumes token counts to decide whether to offload.
   G5 may add a *separate*, additional token-budget layer on top later; it
   does not replace or blend with the G4 byte threshold.
3. **Fail closed with no workspace.** When a result exceeds
   `MaximumInlineToolResultBytes` and no authorized workspace is available from
   the current execution binding, the transform returns an explicit `Failed`
   outcome with structured evidence (call ID, tool name, serialized byte
   length, threshold, and the reason `NoAuthorizedWorkspace`). The oversized
   payload is never inlined, never truncated, and never silently dropped. The
   calling seam converts `Failed` into an explicit tool-error result (the same
   externally-visible shape `IterativeAgentLoop` already uses for tool
   failures, e.g. `$"Error: {result.ErrorMessage}"` at
   `IterativeAgentLoop.cs:378`), so the LLM and diagnostics both see a bounded,
   legible failure instead of an oversized or corrupted message.
4. **Content-addressed SHA-256 paths make writes and retries safe.** Every
   offloaded artifact is written to a workspace path derived deterministically
   from `SHA-256(UTF8 bytes of the serialized tool result)`. Because the path
   *is* a function of the content, re-attempting a write after an ambiguous
   outcome (timeout, cancellation, process restart) can only ever produce the
   same bytes at the same path — it can never silently overwrite a
   *different* logical artifact and never needs `IWorkspace.TryCompareExchange`
   to be safe. The two recovery states required by issue #40/#42 are:
   - **`artifact-written / reference-not-committed`** — `IWorkspace.TryWriteFile`
     returned success, but the in-memory artifact-reference record or the
     offloaded reference string was never constructed (e.g. cancellation
     between the two steps). Recovery: retry recomputes the identical digest
     and path; the retry is a no-op write (content already present) followed
     by normal reference construction. The orphaned body is inert and
     harmless — see "no delete" in the restated bridge matrix below.
   - **`reference-committed / history-persistence-failed`** — the transform
     returned a valid reference/offload string to its caller, but a later,
     out-of-seam step (the enclosing chat message never reaches
     `messages.AddRange`/`messages.Add`, or a per-service-call history
     provider throws while persisting the turn) fails. Recovery: the artifact
     and the (recomputable) reference remain independently valid; nothing
     about the artifact write needs to be redone, and any retry of the whole
     tool call again produces the same digest/path.
   Neither recovery state implies data loss or a dangling reference to a
   missing body, because artifact persistence always strictly precedes
   reference construction (never the reverse).
5. **`ListChildrenAsync` full-scan is an accepted limitation, not a bound.**
   Per T020's `workspace-identity-feasibility.md` (lines 70, 79-82), a future
   `WorkspaceAgentFileStore.ListChildrenAsync` must derive its result from
   `IWorkspace.GetFilePaths()` and filter in memory with a required output
   entry cap. That cap bounds the *returned* list; it does **not** bound the
   O(total workspace files) scan cost of `GetFilePaths()` itself. G4 accepts
   this as a documented limitation of the bridge, not a claim that listing is
   cheap or that the cap makes the operation O(returned entries). Large-
   workspace listing cost remains an explicit, carried-forward measurement
   concern (T020 "Constraints for dependent work", G4 T041-T056).

## Trace 1 — FICC raw-result passthrough (proves the selected-provider seam)

### Source-level trace (exact 10.6.0 line numbers)

`Microsoft.Extensions.AI.FunctionInvokingChatClient` (public surface, not
experimental):

```csharp
// FunctionInvokingChatClient.cs:274
public Func<FunctionInvocationContext, CancellationToken, ValueTask<object?>>? FunctionInvoker { get; set; }

// FunctionInvokingChatClient.cs:1283-1290
protected virtual ValueTask<object?> InvokeFunctionAsync(FunctionInvocationContext context, CancellationToken cancellationToken)
{
    _ = Throw.IfNull(context);
    return FunctionInvoker is { } invoker ?
        invoker(context, cancellationToken) :
        context.Function.InvokeAsync(context.Arguments, cancellationToken);
}

// FunctionInvokingChatClient.cs:1242-1276 (inside CreateResponseMessages)
FunctionResultContent CreateFunctionResultContent(FunctionInvocationResult result)
{
    _ = Throw.IfNull(result);
    object? functionResult;
    if (result.Status == FunctionInvocationStatus.RanToCompletion)
    {
        // If the result is already a FunctionResultContent with a matching CallId, use it directly.
        if (result.Result is FunctionResultContent frc &&
            frc.CallId == result.CallContent.CallId)
        {
            return frc;
        }
        functionResult = result.Result ?? "Success: Function completed.";
    }
    else { /* error-message construction */ }
    return new FunctionResultContent(result.CallContent.CallId, functionResult) { Exception = result.Exception };
}
```

`result.Result` above is exactly the boxed `object?` produced by whatever
`InvokeFunctionAsync` returned — i.e. whatever `FunctionInvoker` returned, when
one is set. If `FunctionInvoker` returns a `FunctionResultContent` whose
`CallId` matches the pending call, `CreateFunctionResultContent` **returns it
unmodified** (`return frc;`) instead of taking the `functionResult = result.Result
?? "Success: ..."` branch that would otherwise feed into a *new*
`FunctionResultContent(callId, functionResult)`. `FunctionResultContent.Result`
(`FunctionResultContent.cs:21`) is a bare `object?` property — FICC itself
performs **no JSON serialization** of it anywhere in this type; serialization
to wire format is deferred entirely to the specific provider `IChatClient`
implementation later in the pipeline, outside FICC and outside Foundry's
control. Therefore "bypasses default wrapping/serialization" means precisely:
the `functionResult = result.Result ?? "Success: ..."` **rewrap** step is
skipped, and whatever value we place in `frc.Result` is the exact, final value
that leaves FICC — not a value FICC has re-derived, re-labeled, or
re-formatted.

`HarnessProviderComposition.cs:298-320` already resolves the discoverable FICC
instance and assigns a custom `FunctionInvoker`:

```csharp
var functionInvokingChatClient = agent.GetService<FunctionInvokingChatClient>();
...
functionInvokingChatClient.FunctionInvoker = async (context, cancellationToken) =>
{
    request.ExecutionBinding.EnsureCurrent(request.ExecutionContextAccessor, request.SessionId);
    return await context.Function.InvokeAsync(context.Arguments, cancellationToken);
};
```

This is the exact, already-proven (T013 `uplift-delta.md`, `function.invoker:
Echo:probe-call` trace line) seam G4 extends: replace the final
`return await context.Function.InvokeAsync(...)` line with a call into the
shared transform, which itself calls `context.Function.InvokeAsync(...)` to
get the raw result, serializes it with `ToolResultSerializer.Serialize`,
applies the byte-threshold decision, and returns a caller-agnostic structured
decision. The FICC delegate maps `Inline` back to the original boxed value and
maps `Offload`/`ExistingReference`/`Fail` to a matching-call-id
`FunctionResultContent`; the transform itself never constructs
caller-specific chat content.

### Empirical proof (disposable local probe, run then deleted)

A throwaway console project (`Microsoft.Extensions.AI` 10.6.0, a fake
`IChatClient` returning one `FunctionCallContent` then a final text message,
one real `AIFunction` whose body returns a string the probe never expects to
see) was built at `_probe_ficc/` and executed, then the entire directory was
deleted (`git status --short` confirms nothing untracked remains). The probe:

1. Registered a tool `GetBigThing` whose real invocation returns
   `"RAW-LARGE-PAYLOAD-THAT-WOULD-NORMALLY-BE-WRAPPED"`.
2. Set `FunctionInvoker` to ignore that return value entirely and instead
   return `new FunctionResultContent(context.CallContent.CallId,
   "artifact://sha256/deadbeef (247000 bytes offloaded)")`.
3. Ran the pipeline through `chatClient.AsBuilder().UseFunctionInvocation().Build()`
   and inspected the resulting `ChatRole.Tool` message.

Output:

```text
CallId matches request: True
Result is exact reference-typed instance (no re-wrap): True
Result value: artifact://sha256/deadbeef (247000 bytes offloaded)
Result CLR type: System.String
Serialized-looks-like-JSON: False
```

`ReferenceEquals(frc.Result, OffloadReference)` was `True` — the exact managed
string instance we constructed inside `FunctionInvoker` reached the outgoing
tool message unmodified, and the real tool body's return value
(`"RAW-LARGE-PAYLOAD..."`) never appears anywhere in the response. This
empirically confirms the source-level trace above on the exact installed
10.6.0 binary, not just on decompiled/read source.

## Trace 2 — `IterativeAgentLoop` full-payload append point

`IterativeAgentLoop.cs:367-382` (`OneRoundTrip`/`MultiRound` branch of the
per-iteration round loop):

```csharp
// Add tool result messages
foreach (var (fc, result) in functionCalls.Zip(roundResults))
{
    var resultContent = result.Succeeded
        ? ToolResultSerializer.Serialize(result.Result)
        : $"Error: {result.ErrorMessage}";

    messages.Add(new ChatMessage(ChatRole.Tool,
        [new FunctionResultContent(fc.CallId, resultContent)]));
}
```

This is the exact, sole point in `IterativeAgentLoop` where a raw
`ToolCallResult.Result` (`ToolCallResult.cs:37`, an `object?`) becomes
LLM-facing message content within an iteration: `ToolResultSerializer.Serialize`
already runs here, and `messages.Add(...)` is the append into the local
`messages` list that is sent to the *next* `GetResponseAsync` call in the same
iteration's round loop (`IterativeAgentLoop.cs:197-201` builds `messages` fresh
per iteration as `[system, user]`; there is no cross-iteration message list —
each iteration's `PromptFactory(context)` is instead responsible for deciding
what, if anything, to embed from `context.LastToolResults`, per the
`ToolCallResult` XML doc: *"available via `IterativeContext.LastToolResults`
for exactly one iteration... The prompt factory decides whether to embed them
in the next prompt."*).

**Wiring point:** T051 replaces the single expression
`ToolResultSerializer.Serialize(result.Result)` at line 377 with a call into
the shared transform (which itself calls `ToolResultSerializer.Serialize`
internally as its first step, per Decision 2). This is the earliest and only
point Foundry controls before the resulting string is embedded in a
`ChatMessage` and handed to the next model call in this loop.

**Explicit deferral — not a gap.** `ToolCallResult.Result` itself (the raw,
unserialized object stored in `context.LastToolResults`, consumed only by a
user-supplied `PromptFactory`) is **not** intercepted by this seam. A
`PromptFactory` is arbitrary caller code with no Foundry-defined
"message-creation" hook; there is no seam to intercept there without changing
the `PromptFactory` contract itself, which is out of scope for G4. This is a
deliberate, recorded scope boundary: G4 guarantees the two *automatic*
message-construction points (this one and Trace 1) never leak an oversized raw
payload into a chat message; a `PromptFactory` that manually re-serializes
`context.LastToolResults[i].Result` into its own prompt string is the
`PromptFactory` author's responsibility and is unaffected by this seam.

## Trace 3 — per-service history insertion, and why the transform must precede it

`Microsoft.Agents.AI.PerServiceCallChatHistoryPersistingChatClient`
(`dotnet-1.15.0`, internal type, public behavior only) sits **between** FICC
and the leaf provider `IChatClient` — i.e. it is *inner* relative to FICC, not
outer:

```text
FunctionInvokingChatClient
  -> MessageInjectingChatClient
    -> PerServiceCallChatHistoryPersistingChatClient   <-- persists here
      -> AIContextProviderChatClient(CompactionProvider)
        -> OpenTelemetryChatClient
          -> caller IChatClient (real provider)
```

(Order re-confirmed from T013 `uplift-delta.md` "Source-traced middleware
order" and from `HarnessProviderComposition.cs:208-234`, which builds the
`ChatClientBuilder` chain in the matching order: function invocation is
configured before `UsePerServiceCallChatHistoryPersistence`.)

Source (`PerServiceCallChatHistoryPersistingChatClient.cs`, `GetResponseAsync`):

```csharp
var messagesForService = skipSimulation
    ? newMessages
    : await agent.LoadChatHistoryAsync(session, newMessages, options, cancellationToken).ConfigureAwait(false);

response = await base.GetResponseAsync(messagesForService, options, cancellationToken).ConfigureAwait(false);
...
await agent.NotifyProvidersOfNewMessagesAsync(session, newMessages, response.Messages, options, cancellationToken).ConfigureAwait(false);
```

`NotifyProvidersOfNewMessagesAsync` — the actual persistence call — fires
*after* `base.GetResponseAsync` returns, i.e. after the underlying provider
round trip for **that** service call completes. Combined with FICC's own
`GetResponseAsync` loop (`FunctionInvokingChatClient.cs:1174-1177`,
`messages.AddRange(CreateResponseMessages(results))`), the sequence across one
tool-round is:

1. Round *N* request goes down through the persistence decorator (history is
   loaded and prepended), reaches the real provider, returns a
   `FunctionCallContent`.
2. The persistence decorator's `NotifyProvidersOfNewMessagesAsync` for round
   *N* fires here — it persists round *N*'s request/response messages. **The
   tool result for this call does not exist yet at this point**, because FICC
   has not yet invoked the function.
3. Back up in FICC, `InvokeFunctionAsync`/`FunctionInvoker` now runs
   (Trace 1), and `CreateResponseMessages`/`messages.AddRange` constructs the
   `ChatRole.Tool` message and appends it to FICC's own in-memory `messages`
   list.
4. That list (now including the tool-result message) becomes the request for
   round *N+1*, which flows back down through the same persistence decorator;
   only *that* round's `NotifyProvidersOfNewMessagesAsync` call persists the
   tool-result message.

The shared transform runs inside step 3 (`FunctionInvoker`, before
`CreateResponseMessages` ever executes) — strictly before the tool-result
message object is constructed at all, and therefore strictly before either the
in-memory `messages.AddRange` in FICC or the out-of-process
`NotifyProvidersOfNewMessagesAsync` persistence call in step 4 can ever see it.
There is no ordering in which an unoffloaded oversized `FunctionResultContent`
could reach a persisted history entry: the object simply never exists in that
form. This is the concrete reason the transform must be wired into
`FunctionInvoker` itself and not, for example, into a later decorator or into
the history provider's store callback — by the time either of those runs, an
oversized payload would already be embedded in a `ChatMessage`.

`IterativeAgentLoop` has no equivalent per-service persistence hook of its own
(Trace 2); its "insertion point" *is* the `messages.Add` call, so the ordering
argument collapses to "the transform must run before that one line," which is
where T051 wires it.

## Trace 4 — compaction/retrieval lifecycle boundary (G5 remains responsible for policy)

T013's `uplift-delta.md` "Compaction finding" (already executed against this
exact 10.6.0/1.15.0 graph) established, and this report does not repeat the
full trace, only its consequence for G4:

- `CompactionProvider` runs *inside* FICC, before the first model call of a
  turn, and — once per-service persistence sets the local-history sentinel
  conversation ID — treats history as remote-managed and skips the
  intermediate second-call reduction path.
- The history reducer configured on `InMemoryChatHistoryProvider` compacts
  **previously persisted** history at the next `history.provide` boundary; it
  does not see the *current* round's `FunctionCallContent`/`FunctionResultContent`
  pair until that pair has already been persisted by
  `PerServiceCallChatHistoryPersistingChatClient` in a later round.
- Consequently, MAF 1.15 exposes no compaction extension point that observes
  every complete current tool-call/result pair before the next model request.

**What this means for G4, definitively:** because (per Trace 3) the offload
transform already runs and replaces the oversized raw value *before* any
`FunctionResultContent`/`ChatMessage` is constructed, the object that
eventually reaches history persistence and, later, any compaction/reducer pass
is *already* the small offloaded reference string — never the raw payload.
G4's job is exactly and only that pre-empting replacement. G5 owns everything
that happens *after* a durable reference message exists in history: deciding
*when* to compact, *which* reference-bearing messages to preserve verbatim
across a reducer pass, and how to validate that a reduced/compacted history
still contains valid tool-call/result sequencing (`data-model.md` "Compaction
Decision" fields/validation). G4 does not implement, assume, or depend on any
particular compaction/reducer behavior; it only guarantees the input to that
future stage is already bounded.

## Shared transform contract

**Input** (identical for both call sites):

- the raw tool-call result object (`object?`) exactly as returned by tool
  invocation — for the FICC path, exactly what `context.Function.InvokeAsync(...)`
  returns; for the iterative path, exactly `ToolCallResult.Result`;
- call identity: tool/function name, call ID, iteration/round index;
- the current, already-validated execution binding (`IAgentExecutionContext` /
  `HarnessExecutionBinding`), from which an authorized `IWorkspace` may or may
  not be resolvable;
- the configured `MaximumInlineToolResultBytes` policy value;
- a `CancellationToken`.

**Output** — one of exactly four outcomes (mirrors `data-model.md`'s
"Tool-Result Offload Decision" fields: `Decision: inline, offload, existing
artifact reference, fail`):

- **Inline** — serialized byte length is at or under the threshold. The
  decision carries both the original raw result and the already-computed
  `ToolResultSerializer.Serialize` string. The FICC caller returns the raw
  result to preserve current wrapping semantics; the iterative caller uses the
  serialized string it already requires. (Boundary rule:
  exactly-at-threshold inlines; only strictly-over-threshold offloads.)
- **Offload** — over threshold and a workspace is available; the transform
  writes the content-addressed artifact (Decision 4), constructs an artifact
  reference record, and returns the bounded offload reference string.
- **Existing reference** — the exact same digest was already offloaded earlier
  in the same run/session; the transform reuses the same artifact path and
  reference. Implementations may skip a write after an existence check or may
  perform an idempotent overwrite of identical bytes; no in-memory cache is
  required by this contract.
- **Fail** — over threshold with no authorized workspace (Decision 3), or an
  unrecoverable write failure; returns structured failure evidence, never a
  partial/truncated string.

**Wiring contract** (both call sites, without finalizing every internal type
as public API — these are illustrative shapes for downstream tasks, not a
public contract commitment):

- `IterativeAgentLoop` (`Tools/` per T051's file path): the round loop calls
  the transform in place of the bare `ToolResultSerializer.Serialize(result.Result)`
  expression at line 377, using `Succeeded` results only (failed tool calls
  keep their existing `$"Error: ..."` path unchanged — offload only ever
  applies to successful, oversized results). It maps `Inline` to the decision's
  serialized string, `Offload`/`ExistingReference` to the reference string, and
  `Fail` to a bounded explicit error string while retaining the structured
  failure outcome for progress/diagnostics.
- `HarnessProviderComposition`'s assigned `FunctionInvoker`
  (`HarnessProviderComposition.cs:308-320`): the delegate calls
  `context.Function.InvokeAsync(...)` first (unchanged), then passes the
  result through the transform. It returns the original raw object for
  `Inline`; for `Offload`/`ExistingReference`, it returns a matching-call-id
  `FunctionResultContent` containing the bounded reference; for `Fail`, it
  returns a matching-call-id `FunctionResultContent` containing bounded
  explicit failure evidence. The latter two use the Trace 1 passthrough so
  FICC never re-wraps them.
- Both call sites resolve the authorized workspace and byte-threshold policy
  through the *existing* per-execution binding seam (`HarnessExecutionBinding`
  / `IAgentExecutionContextAccessor`), not through any new ambient lookup —
  consistent with T020's "initial bridge requires per-execution binding; no
  singleton bridge" constraint, which this transform inherits unchanged.

## Artifact and reference state machine

### Content-addressed path format

`{workspace-relative-artifact-root}/{sha256-hex-lowercase}` — e.g. a sharded
form such as `.foundry/artifacts/<first 2 hex chars>/<next 2 hex chars>/<full
64-char hex digest>` to keep any single directory listing bounded, matching
`GetFilePaths()`'s flat-enumeration cost concern (Decision 5). The digest is
computed over the UTF-8 bytes of the exact string `ToolResultSerializer.Serialize`
produced — the same string the byte-threshold decision was measured against
(Decision 2), so "the content that was measured" and "the content that is
hashed and stored" are always the same string, with no separate re-encoding
step that could desynchronize digest from size. `IWorkspace.TryWriteFile(path,
content)` takes a `string` (`IWorkspace.cs:47`), which the already-serialized
text satisfies directly with no additional encoding.

### Recorded metadata (per `data-model.md` "Workspace Artifact" / "Artifact
Reference" fields, restated here as the concrete G4 set)

- Content digest (SHA-256, hex) and byte size (both independently
  verifiable — a reader must recompute the digest over the retrieved content
  and compare, never trust the path alone).
- Canonical workspace path (the content-addressed path above).
- Owning execution binding (user/orchestration identity from
  `IAgentExecutionContext`, audit-only, never an authorization decision by
  itself — consistent with T020's `UserId` note).
- Producing call metadata: tool/function name, call ID, iteration/round,
  run/session identity.
- Creation evidence (timestamp/step) and staleness/liveness status (`Live`,
  `Stale`, `Missing`, `Expired` per `data-model.md`'s Artifact Reference state
  transitions).

### State machine (from `data-model.md`, settled as the G4 scope)

```text
RawResult -> OffloadPending -> ArtifactPersisted -> ReferenceCommitted -> OffloadedReference
                                       |                     |
                                       v                     v
                              RecoveryRequired        RecoveryRequired
                            (artifact-written /     (reference-committed /
                             reference-not-committed) history-persistence-failed)
```

`RawResult -> OffloadPending -> InlineResult` is the non-offload path (under
threshold). `RawResult -> Failed` is the no-workspace/unrecoverable path
(Decision 3), reachable directly from `OffloadPending` without ever reaching
`ArtifactPersisted`.

### No-workspace behavior

Covered exactly by Decision 3: `Failed`, never `InlineResult`,
`OffloadedReference`, or a truncated string.

### Cancellation points

1. **Before artifact write starts** — `ThrowIfCancellationRequested()` before
   calling `TryWriteFile`; no side effect; safe to retry the whole decision.
2. **During the artifact write** — `IWorkspace.TryWriteFile` is synchronous
   (T020: "the bridge cannot interrupt an already-running synchronous
   workspace call"); cancellation cannot abort a write already in flight, and
   the transform must not use `Task.Run` to fabricate false interruption
   semantics (T020's explicit prohibition carries over unchanged).
3. **After a successful write, before reference construction** — cancellation
   here yields `artifact-written/reference-not-committed` (Decision 4).
4. **After reference construction, before it reaches a persisted chat
   message/history entry** — cancellation or an unrelated downstream failure
   here yields `reference-committed/history-persistence-failed` (Decision 4).
5. **After completion** — no further cancellation is meaningful; the offload
   is done.

### What can and cannot be atomic

**Can be treated as atomic (logically, not necessarily physically):** a
single `TryWriteFile` call to one content-addressed path, because the path
uniquely determines the expected bytes — any successful write to that exact
path can only ever be that one deterministic payload. Foundry can rely on
"write succeeded" meaning "the expected bytes are now retrievable at that
path" without needing a separate verification read, *for the purpose of
idempotent retry reasoning*.

**Cannot be claimed atomic:**
- Physical write atomicity (e.g. atomic rename vs. in-place write) is an
  `IWorkspace` implementation detail Foundry does not control or guarantee —
  the interface exposes no such contract (T020's operation mapping table).
- The end-to-end sequence — write artifact, construct reference, append the
  reference-bearing chat message, persist that turn to history — is **not**
  one transaction. There is no cross-operation rollback: a failure at step 3
  or 4 does not undo the artifact write (nor should it, since the artifact
  write is itself harmless and idempotently reproducible). This is exactly
  why the two named recovery states exist instead of a single all-or-nothing
  "commit."

### Retry/idempotency semantics

Retrying the entire offload decision after any failure or ambiguous outcome is
always safe:

- The digest is a pure function of the already-serialized string, so retrying
  recomputes the identical digest and path every time for the same tool
  result.
- Re-writing identical content to the same content-addressed path is either a
  no-op (content already present) or a fresh write — both are safe and
  observably identical to a caller.
- Reference construction is a pure function of `(digest, path, size, owning
  binding, call metadata)` with no side effects of its own, so it can be
  recomputed freely from an already-persisted artifact without re-writing it
  (the "existing reference" outcome above).
- Only the outer history/message persistence step (owned by MAF's
  per-service-call persistence or by `IterativeAgentLoop`'s own message list)
  can fail independently of the artifact/reference machinery; that failure is
  exactly `reference-committed/history-persistence-failed`, and retrying the
  *enclosing tool call* (not just the offload step) is the recovery path,
  which — because of content addressing — cannot create a duplicate or
  divergent artifact.
- Because `IWorkspace` exposes no delete operation (T020), an artifact
  orphaned by a cancellation between write and reference-commit is never
  cleaned up in G4. This is accepted as harmless, permanent debris, not a
  defect — see "Deliberate deferrals" below.

## G4 rehydration mechanism boundary

G4 (T043/#43: T046, T047, T050, T052, T053) builds only the **explicit**
primitives:

- resolving a specific artifact reference by its recorded digest/path and
  verifying the digest still matches the stored content (`Live`, or an
  explicit `Stale`/`Missing`/`Expired`/unauthorized outcome — never a silent
  substitution);
- injecting an explicitly-requested, resolved rehydration payload as a
  **marked recoverable context segment**, positioned after the eager-offload
  boundary so it is distinguishable from an ordinary (never-offloaded) tool
  result (`data-model.md` "Rehydration Decision", `Delivery mode: marked
  recoverable context segment`);
- the explicit rule that a rehydrated body bypasses eager re-offload for the
  *active* request even if it is again over `MaximumInlineToolResultBytes`
  (`data-model.md`, Rehydration Decision validation) — this prevents an
  infinite offload/rehydrate loop within one request, without claiming
  anything about future requests.

G4 does **not** build: any automatic trigger that decides *when* to rehydrate
based on relevance/compaction pressure, any policy that chooses *which*
references to keep resident across turns, or any integration with a
compaction/reducer pass. All of that is G5's "hybrid context" responsibility
(`plan.md` Group 5 deliverables list "explicit rehydration decisions" as a G5
deliverable built *on top of* the G4 primitive, not a redefinition of it).
Concretely: G4 answers "given this exact reference, can I get its body back,
and how do I mark that body so it's recoverable/evictable later?"; G5 answers
"should the agent rehydrate this reference right now, and how does that
interact with what compaction just reduced?"

This report does not implement G5 compaction and does not claim any live
`FileMemory`/`FileAccess` provider is composed in G4 — MAF's own
`FileAccessStore` remains opt-in, experimental (`MAAI001`), upstream surface
(T013 `uplift-delta.md`, "Experimental surface"), and Foundry's own
`IWorkspace` remains the sole authoritative store for G4 artifacts, per
`plan.md` Group 4's "no competing authoritative file-memory store" gate
criterion.

## Bridge operation support matrix (restated from T020)

This is the same matrix `workspace-identity-feasibility.md` already
established (T020, evidence lines 63-82); it is restated here verbatim in
outcome terms because #41's `WorkspaceAgentFileStore` and #42's offload
transform both depend on it being exact and unchanged, not re-derived.

| `AgentFileStore` operation | Outcome | Required behavior |
|---|---|---|
| `WriteAsync` | **Supported** (ordinary write only) | Canonicalize first; check cancellation before/after; throw the workspace's own failure exception; never claim compare-exchange/CAS semantics |
| `ReadAsync` | **Conditional** | Return content on success; map "missing" to `null` **only** through an explicit, workspace-specific missing-file classifier (no interface-level typed "not found" contract exists); surface every other failure as-is |
| `DeleteAsync` | **Unsupported** | `IWorkspace` has no delete operation at all; fail explicitly (e.g. `NotSupportedException`) — never return a success-shaped `false` |
| `ListChildrenAsync` | **Supported with limits** | Derive from `GetFilePaths()`; canonicalize the directory; return direct children only, directories before files, case-insensitive de-dup; enforce a required output entry cap — the cap bounds the *result*, not the *scan* (Decision 5) |
| `FileExistsAsync` | **Supported** | Canonicalize; fail closed on an invalid path |
| `SearchAsync` | **Unsupported generically** | `IWorkspace` cannot inspect size or bound a read before allocating full content — no built-in CAS-adjacent or size-aware primitive exists; a bounded-search adapter would need separate evidence before any profile may enable it |
| `CreateDirectoryAsync` | **Partial** | Valid only as a no-op for profiles that accept a file-materialized directory model; an "empty" created directory is not observable through listing or search (`IWorkspace` has no directory-as-object concept) |

Additional restated facts required by #41/#42/#43 to avoid re-deriving them:

- **No CAS claim for ordinary writes.** `AgentFileStore.WriteAsync(path,
  content)` supplies no expected version/ETag/content; mapping it to
  `IWorkspace.TryCompareExchange` would fabricate a guarantee that does not
  exist upstream. Ordinary writes map only to `TryWriteFile`.
  `TryCompareExchange` remains available, unweakened, to Foundry-native
  callers directly (not through the bridge).
- **No delete, no generic search.** Both remain permanently unsupported for
  this bridge shape; profiles must disable or omit tools/paths that could
  invoke them rather than rely on a predictable runtime exception as the
  primary capability-selection mechanism.
- **Partial `CreateDirectoryAsync`.** Same as above — a no-op is only valid
  where the consuming profile does not depend on empty-directory visibility.
- **List cap bounds the result set, full-scan cost is real.** Restated exactly
  as Decision 5.
- **Per-execution binding, not ambient.** The bridge (and, by extension, the
  artifact store built on top of it) requires a store instance bound to one
  authorized `IWorkspace` and one immutable execution identity at
  construction time. Ambient (`IAgentExecutionContextAccessor.Current`-on-
  every-call) resolution remains unapproved by this feasibility line; if ever
  reconsidered it needs its own evidence and must never be a singleton.
- **No workspace serialized/restored.** `AgentFileStore` carries no user,
  tenant, session, or workspace identity of its own, and restored
  `AgentSession.StateBag` may restore provider state but must never restore or
  replace the host-authorized workspace. A path string or restored session
  value must never be used to select a workspace.

## Leaf dependency order

```text
#41 (T042 T043 T044 -> T049)   Workspace bridge:
                                path/traversal/read/write/failure-mapping tests,
                                contention/non-CAS tests, cross-session isolation
                                tests, THEN the WorkspaceAgentFileStore bridge
                                implementation itself.
#43 (T046 T047 -> T050 T052 T053)  Artifact reference types:
                                digest/stale/missing reference tests and explicit
                                rehydration selection tests, THEN the artifact
                                reference record, rehydration, and resolution
                                types.
        \
         \--> both #41 and #43 land before -->
#42 (T045 T048 -> T051)         Transform wiring:
                                the shared transform needs a real workspace to
                                write to (#41) and a real artifact-reference
                                type to construct (#43) before it can be wired
                                into IterativeAgentLoop and
                                HarnessProviderComposition's FunctionInvoker.
                                        |
                                        v
#44 (T054 T055 T056)             Observability and gate:
                                progress events, diagnostics, and the G4
                                ADR/gate record — requires the prior three
                                leaves to exist so it can observe/attribute
                                real offload and rehydration decisions rather
                                than a stub.
```

The tests-first ordering inside #41, #42, and #43 remains exactly as recorded
in each issue's own execution fence. The cross-leaf arrows above are a G4
implementation sequencing decision derived by T041, not language already
present in those issue bodies: T051 needs both the bridge and artifact-reference
types, and #44 needs real offload/rehydration outcomes to observe. #41 and the
test-first portion of #43 may proceed in parallel; #42's tests may also start
early, but T051 cannot land until #41 and T050 are available.

## Required deterministic fixtures per downstream leaf, and stop conditions

All fixtures below use deterministic, fake/in-process collaborators only
(`InMemoryWorkspace`, a fake `IChatClient` in the style of
`HarnessCompatibilityProbe`'s `ProbeChatClient`, and explicit fault-injection
hooks) — never a live model or live network call, consistent with
`tasks.md`'s "Tests" header: *"Deterministic correctness tests precede
implementation. Broad provider and stochastic evaluation tasks run only in
hosted CI."*

| Task | Required deterministic fixtures | Stop condition |
|---|---|---|
| T042 | Canonical path-equivalence set (`kb/foo.md`, `./kb/foo.md`, `kb//foo.md`, `/kb/foo.md` all resolve identically); `..`-traversal rejection; write-then-read round trip; missing-file read through the explicit classifier vs. a surfaced non-missing failure; `FileExistsAsync` canonicalization and fail-closed-on-invalid-path | Every row of the restated bridge matrix has at least one passing assertion; unsupported operations throw the documented exception type, not a success-shaped result; suite runs against `InMemoryWorkspace` only |
| T043 | Two deterministically-sequenced (not real-thread-raced) ordinary writes to the same path, asserting "last write wins, no exception, no fabricated CAS"; an explicit assertion that the bridge's `WriteAsync` never accepts or threads through a version/ETag argument | Passes with zero flakiness across repeated deterministic runs; explicitly documents `TryCompareExchange` as the only CAS-capable path, never the bridge |
| T044 | Two per-execution-bound stores over two distinct `IWorkspace` instances with colliding logical paths but distinguishable sentinel content; a store whose execution binding is invalidated mid-scope | No isolation assertion may pass "by accident" of shared static state — content must differ per session and cross-reads must be provably impossible; identity-mismatch fails closed, never falls back to a prior session's workspace |
| T045 | A fixed tool result whose serialized UTF-8 byte length is a known constant, tested at exactly the threshold (must inline) and threshold+1 (must offload); run through **both** wiring seams (`IterativeAgentLoop` and `HarnessProviderComposition`) with identical content to prove one shared decision | Deterministic off-by-one boundary coverage passes on both seams with no live provider call; the raw oversized string never appears verbatim in the resulting `ChatMessage`/history in either seam |
| T046 | A resolvable reference to known content; out-of-band content mutation after reference creation (digest mismatch -> `Stale`); a reference to a path that was never written (`Missing`) | Every non-`Live` terminal state has an explicit, distinguishable failure-evidence assertion; no path allows a digest-mismatched body to be delivered as if valid |
| T047 | An explicit (deterministic-caller-driven, not model-driven) rehydration request against a `Live` reference, verifying exact original-body recovery and a "marked recoverable context segment" flag; the same request against `Stale`/`Missing`/unauthorized references, verifying no body is ever injected; a rehydrated body that is again over `MaximumInlineToolResultBytes`, verifying it is not re-offloaded within the same active request | Rehydration is exercised only through the explicit G4 primitive (no automatic trigger fixture — that is explicitly out of scope, see the rehydration boundary section); the bypass-re-offload invariant has its own dedicated assertion |
| T048 | (a) cancellation requested before any write — no artifact written, `OperationCanceledException` surfaces; (b) write succeeds, then injected failure before reference construction — assert `artifact-written/reference-not-committed`, then assert a content-identical retry is a safe no-op; (c) reference constructed, then injected failure in a fake history-persistence callback — assert `reference-committed/history-persistence-failed`, then assert the artifact and reference remain independently resolvable | Both named recovery states produce structurally distinguishable evidence (not the same generic exception shape); a retried offload after either failure never produces two different artifacts for identical content; no fixture relies on real multi-threaded race timing — only deterministic fault-injection hooks |

## AOT, diagnostics, and public API implications

- **AOT.** SHA-256 digest computation (`System.Security.Cryptography.SHA256`)
  and content-addressed path formatting are reflection-free and AOT-safe.
  `ToolResultSerializer.Serialize` already uses reflection-based
  `JsonSerializer.Serialize(result, result.GetType())` for non-`string`/
  non-`JsonElement` results (`ToolResultSerializer.cs:36-42`); this is a
  pre-existing characteristic the shared transform inherits unchanged, not a
  new AOT regression introduced by G4 — it was already the serialization path
  used by `IterativeAgentLoop` before this task. If an artifact-reference
  record type is ever itself JSON-serialized (e.g. for diagnostics or a future
  index), it should follow this codebase's existing source-generated
  `JsonSerializerContext` convention (`HarnessSessionEnvelopeJsonContext.cs`
  is the established precedent) rather than reflection-based serialization.
- **Diagnostics.** T054/T055 (#44) own the concrete progress-event and
  diagnostics types. This report only settles *what* must be observable at
  the category level: serialized byte size before the decision, the
  threshold compared against, the decision taken (inline/offload/existing-
  reference/fail), the content digest, and the artifact path — but never raw
  artifact content, matching the existing security/abuse convention already
  recorded in T020 ("Do not include file contents, user IDs, or tenant IDs in
  diagnostics").
- **Public API.** No new public Foundry API surface is introduced by settling
  this report. Every type discussed above (`WorkspaceAgentFileStore`, the
  shared transform, artifact-reference/rehydration/resolution types) remains
  internal, matching `plan.md` Group 4's release line: *"Internal or
  experimental prerelease; no public neutral abstraction."* Any MAF/MEAI
  experimental surface this work touches inherits the existing `MAAI001`
  marker already recorded in T013 (compaction options/strategies and custom
  file stores) — G4 does not remove or bypass that marker, and does not
  compose any experimental upstream file-store/file-access provider.

## Deliberate deferrals (explicit, not open questions)

- G5 compaction/preservation policy, trigger margin, and fallback are not
  implemented or assumed here — see Trace 4 and the rehydration boundary
  section.
- Automatic/policy-driven rehydration triggers remain G5, not G4 (rehydration
  boundary section).
- `PromptFactory`-embedded raw tool results in the iterative cross-iteration
  path are not intercepted by this seam (Trace 2) — a documented scope
  boundary, not an oversight.
- Artifact garbage collection/deletion is not possible in G4 because
  `IWorkspace` has no delete operation; orphaned artifacts from
  cancelled/failed offloads are accepted as permanent, harmless debris until a
  future workspace capability adds deletion.
- Bounded/generic search over artifacts remains unsupported (restated bridge
  matrix); artifact resolution is by exact digest/reference identity only.
- Ambient (non-per-execution) workspace resolution remains unapproved,
  inherited unchanged from T020.
- True cross-process/transactional atomicity across "write artifact, commit
  reference, persist history" is not achievable with `IWorkspace`'s
  synchronous, non-transactional contract; G4 accepts idempotent-recoverable
  consistency instead, per the state machine and retry semantics above.
- No live `FileMemory`/`FileAccess` provider is composed in G4; Foundry
  `IWorkspace` remains the sole authoritative store.

## Sources

- [MEAI `FunctionInvokingChatClient` 10.6.0](https://github.com/dotnet/extensions/blob/v10.6.0/src/Libraries/Microsoft.Extensions.AI/ChatCompletion/FunctionInvokingChatClient.cs) — `FunctionInvoker` (line 274), `InvokeFunctionAsync` (lines 1283-1290), `CreateResponseMessages`/`CreateFunctionResultContent` passthrough (lines 1231-1276), `messages.AddRange` append (lines 1174-1177)
- [MEAI `FunctionResultContent` 10.6.0](https://github.com/dotnet/extensions/blob/v10.6.0/src/Libraries/Microsoft.Extensions.AI.Abstractions/Contents/FunctionResultContent.cs) — `Result` is a bare `object?`, no JSON-serialization coupling in this type
- Local decompilation of the exact installed 10.6.0 binaries
  (`microsoft.extensions.ai` / `microsoft.extensions.ai.abstractions`,
  `net10.0` TFM) via `ilspycmd`, cross-checked against the public source above
  (used and then deleted for this task)
- Disposable local runtime probe (built, executed, and deleted for this task;
  not present in the working tree) — empirical proof of the passthrough in
  Trace 1
- [MAF `PerServiceCallChatHistoryPersistingChatClient` 1.15.0](https://github.com/microsoft/agent-framework/blob/dotnet-1.15.0/dotnet/src/Microsoft.Agents.AI/ChatClient/PerServiceCallChatHistoryPersistingChatClient.cs)
- [MAF `CompactionProvider` 1.15.0](https://github.com/microsoft/agent-framework/blob/dotnet-1.15.0/dotnet/src/Microsoft.Agents.AI/Compaction/CompactionProvider.cs)
- [MAF `AgentFileStore` 1.15.0](https://github.com/microsoft/agent-framework/blob/dotnet-1.15.0/dotnet/src/Microsoft.Agents.AI/Harness/FileStore/AgentFileStore.cs)
- `specs/001-maf-harness-first-class/evidence/uplift-delta.md` (T013) — executed lifecycle traces this report builds on directly
- `specs/001-maf-harness-first-class/evidence/workspace-identity-feasibility.md` (T020) — bridge operation matrix restated above
- `specs/001-maf-harness-first-class/data-model.md` — Workspace Artifact, Artifact Reference, Tool-Result Offload Decision, Compaction Decision, Rehydration Decision entity definitions
- `specs/001-maf-harness-first-class/plan.md` — Group 4/Group 5 deliverables and gate criteria
- `src/NexusLabs.Foundry.MicrosoftAgentFramework/ToolResultSerializer.cs`
- `src/NexusLabs.Foundry.MicrosoftAgentFramework/Iterative/IterativeAgentLoop.cs`
- `src/NexusLabs.Foundry.MicrosoftAgentFramework/Iterative/ToolCallResult.cs`
- `src/NexusLabs.Foundry.MicrosoftAgentFramework/Harness/HarnessProviderComposition.cs`
- `src/NexusLabs.Foundry.MicrosoftAgentFramework/Workspace/IWorkspace.cs`
- `src/NexusLabs.Foundry.MicrosoftAgentFramework/Workspace/CompareExchangeResult.cs`
- `src/NexusLabs.Foundry.MicrosoftAgentFramework/Workspace/WriteFileResult.cs`
- `src/NexusLabs.Foundry.MicrosoftAgentFramework/Context/IAgentExecutionContext.cs`
- `src/NexusLabs.Foundry.MicrosoftAgentFramework/Harness/HarnessSessionEnvelopeJsonContext.cs` (AOT-safe serialization precedent)
- GitHub issues [#40](https://github.com/ncosentino/foundry/issues/40), [#41](https://github.com/ncosentino/foundry/issues/41), [#42](https://github.com/ncosentino/foundry/issues/42), [#43](https://github.com/ncosentino/foundry/issues/43), [#44](https://github.com/ncosentino/foundry/issues/44), [#20](https://github.com/ncosentino/foundry/issues/20), [#16](https://github.com/ncosentino/foundry/issues/16)
