# Gate G5 Decision — Experimental Hybrid Context Compaction Observability

## Decision

**PASS for the cumulative G5 experimental hybrid context compaction slice,
including its observability contract.**

Gate G5 delivers, and this document approves as a single cumulative record,
the experimental per-provider-call hybrid context compaction mechanism
together with this leaf's privacy-safe structured observability over it:

1. **Deterministic bounded-assembly policy** — one experimental
   `HarnessHybridProfile`, evaluated per real provider call against a
   configured hard limit and trigger margin, that evicts recoverable
   rehydrated bodies first, treats the configured upstream `IChatReducer`'s
   proposal as advisory-only (never trusted without independent structural
   and size verification), and falls back to a deterministic
   preservation-only path when no accepted proposal converges within a
   bounded attempt count.
2. **Explicit termination, never silent forwarding** — an assembly that
   cannot converge (`Irreducible`) or is destabilized by concurrent mutation
   until its attempt budget is exhausted (`ConcurrentMutationLimit`) throws
   rather than dispatching an over-budget or stale context.
3. **One-shot rehydration delivery per outer run, with atomic same-run
   admission** — a non-retransmission coordinator reserves a lease per digest
   per provider call so a rehydrated body is delivered to the real provider
   at most once per outer run; a per-digest revision counter defends against
   losing a recoverable body across sequential lease releases; and a
   per-run provider-call admission gate makes concurrent same-run provider
   dispatch structurally impossible, so two nested calls within one outer
   run can never race each other into (or through) the real provider.
4. **Compaction/context-composition observability** — a privacy-safe
   `HarnessContextDiagnostics` snapshot and four public progress events
   (started, completed, terminated, composed) reporting every assembly
   attempt's outcome, sizes, stages, per-assembly correlation (`AssemblyId`),
   and final per-category contribution, wired through the existing
   progress-reporter accessor seam with no new ambient singleton and no
   optional parameter; an explicit `HarnessContextMeasurementUnit` on every
   size estimator so no generic integer is ever mislabeled as a provider
   token count; and a shared `HarnessContextAttribution` UTF-8 byte snapshot
   riding along on the existing `HarnessArtifactDiagnostics` contract for
   both offload and rehydration decisions.
5. **Bounded, backpressured progress delivery** — `ChannelProgressReporter`
   applies genuine producer-side backpressure once its bounded channel
   saturates, rather than accumulating an unbounded background write queue.

`HarnessProviderComposition` remains the sole selected-provider composition
root; `HarnessCompactionComposition` is invoked internally by it against the
exact same resolved capability profile and installs at most one
`HarnessHybridCompactionChatClient`, never a second, independently-invoked
composition root and never a second competing compaction component.
Conversation compaction policy was explicitly out of scope for ADR-0006; this
gate and ADR-0007 are where that policy, and its observability, are decided.

No public Harness runtime configuration/composition API is approved by this
gate. The only public API surface introduced by this leaf is the
observability contract: four enums, three records (`HarnessContextDiagnostics`,
`HarnessContextCategoryContribution`, `HarnessContextAttribution`), and four
`IProgressEvent` records. Every compaction mechanism, request, result,
policy, and status type — including the estimator interface itself — remains
`internal`.

## Evidence identity

Cumulative test counts on top of the G4 foundation gate (`gate-g4.md`):

| Item | Cumulative Harness tests | Cumulative project tests | Harness delta | Project delta |
|---|---|---|---|---|
| G4 final (baseline) | 305 | 1,874 | — | — |
| #99 | 420 | 1,989 | +115 | +115 |
| #100 | 480 | 2,049 | +60 | +60 |
| #101 | 569 | 2,138 | +89 | +89 |
| Compaction/context-composition observability | 601 | 2,172 | +32 | +34 |
| Concurrency blocker fix (PR #106) | 609 | 2,180 | +8 | +8 |
| Same-run provider-call admission gate + bounded backpressure fix | **610** | **2,181** | **+1** | **+1** |

- Every delta above was measured directly against the working tree via
  `dotnet test`, not estimated from source occurrences.
- Final reviewed G5 integration head: `2c0bbd80` on `harness/g5-integration`; the
  observability/gate leaf merged through
  [PR #102](https://github.com/ncosentino/foundry/pull/102), a subsequent
  evidence refresh through [PR #104](https://github.com/ncosentino/foundry/pull/104),
  and the concurrency-blocker fix through
  [PR #106](https://github.com/ncosentino/foundry/pull/106). The final
  same-run provider-call admission gate and `ChannelProgressReporter`
  bounded-backpressure fix merged through
  [PR #108](https://github.com/ncosentino/foundry/pull/108).
- All `dotnet build`/`dotnet test` commands for this gate were run with
  `$env:NUGET_PACKAGES='G:\dev\caches\nuget\packages'` set.

## Files changed (cumulative)

New files:
- `src/NexusLabs.Foundry.MicrosoftAgentFramework/Diagnostics/HarnessContextCompactionOutcome.cs`
- `src/NexusLabs.Foundry.MicrosoftAgentFramework/Diagnostics/HarnessContextAssemblyStageCategory.cs`
- `src/NexusLabs.Foundry.MicrosoftAgentFramework/Diagnostics/HarnessContextCategory.cs`
- `src/NexusLabs.Foundry.MicrosoftAgentFramework/Diagnostics/HarnessContextCategoryContribution.cs`
- `src/NexusLabs.Foundry.MicrosoftAgentFramework/Diagnostics/HarnessContextDiagnostics.cs`
- `src/NexusLabs.Foundry.MicrosoftAgentFramework/Diagnostics/HarnessContextMeasurementUnit.cs`
- `src/NexusLabs.Foundry.MicrosoftAgentFramework/Diagnostics/HarnessContextAttribution.cs`
- `src/NexusLabs.Foundry.MicrosoftAgentFramework/Harness/Context/HarnessContextDiagnosticsFactory.cs`
- `src/NexusLabs.Foundry.MicrosoftAgentFramework/Progress/HarnessContextCompactionStartedEvent.cs`
- `src/NexusLabs.Foundry.MicrosoftAgentFramework/Progress/HarnessContextCompactionCompletedEvent.cs`
- `src/NexusLabs.Foundry.MicrosoftAgentFramework/Progress/HarnessContextCompactionTerminatedEvent.cs`
- `src/NexusLabs.Foundry.MicrosoftAgentFramework/Progress/HarnessContextComposedEvent.cs`
- `src/NexusLabs.Foundry.MicrosoftAgentFramework.Tests/Harness/HarnessContextObservabilityTests.cs` (14 tests)
- `src/NexusLabs.Foundry.MicrosoftAgentFramework.Tests/Harness/HarnessContextDiagnosticsValidationTests.cs` (13 tests)
- `docs/adr/adr-0007-experimental-hybrid-context-compaction.md`
- `specs/001-maf-harness-first-class/evidence/gate-g5.md` (this file)

Modified files (final, cumulative state; mechanisms described in full in the
architecture sections below):
- `Harness/Context/HarnessHybridCompactionChatClient.cs` — required nullable
  trailing `IProgressReporterAccessor? progressAccessor` constructor
  parameter; reports Started/Completed-or-Terminated/Composed events per
  assembly attempt; both `GetResponseAsync`/`GetStreamingResponseAsync`
  acquire the coordinator's per-run provider-call gate before assembly.
- `Harness/Context/HarnessCompactionComposition.cs`,
  `HarnessCompactionCompositionRequest.cs`, `Harness/HarnessProviderComposition.cs`
  — threaded the same `IProgressReporterAccessor?` through the composer's
  request record and its one call site, rather than a second accessor seam.
- `Harness/Context/HarnessHybridContextPolicy.cs`, `IHarnessContextSizeEstimator.cs`,
  `HarnessUtf8ContextSizeEstimator.cs`, `HarnessUtf8TextSizeEstimator.cs` —
  required `HarnessContextMeasurementUnit MeasurementUnit` on the estimator
  interface; both production estimators report `Utf8Bytes`.
- `Diagnostics/HarnessArtifactDiagnostics.cs` — added a public `Attribution`
  property, populated automatically by the existing `ForOffload`/`ForRehydration`
  factories.
- `Harness/Capabilities/HarnessCapabilityResolver.cs` — `Compaction`'s
  `TrustBoundary` is `ExternalContent`, `AotStatus` is `Unverified`,
  `DiagnosticsStatus` is `Available`; `Stability`/`DefaultEnabledInBundle`/
  `DeliveryPhase` unchanged (`Experimental`/`false`/`G5`).
- `Harness/Context/HarnessCompactionRunCoordinator.cs`,
  `HarnessDeliveredSegmentFilteringSnapshotProvider.cs` — per-digest
  `Revisions` tracking plus `GetRevision`, the `ProviderCallGate` semaphore
  plus `EnterProviderCallAsync`, and the snapshot provider's conversion from
  stateless to a stateful effective-version computation.
- `Progress/ChannelProgressReporter.cs`, `Progress/IProgressSink.cs`,
  `docs/progress-reporting.md` — `Report` applies bounded producer-side
  backpressure once the channel saturates, catching only `ChannelClosedException`.
- Test fixtures updated for the trailing `progressAccessor` parameter:
  `Harness/HarnessConstantSizeContextEstimator.cs`, `HarnessFixedSizeContextEstimator.cs`
  (report `HostDefinedUnits`), `HarnessCompactionCancellationTests.cs`,
  `HarnessCompactionComposeTests.cs`, `HarnessCompactionSeamTests.cs`,
  `HarnessCompactionSessionIntegrationTests.cs`,
  `HarnessProviderCompositionCompactionTests.cs`.
- `Harness/HarnessCompactionRunCoordinatorTests.cs` (25 tests),
  `Harness/HarnessCapabilityProfileTests.cs` (+3),
  `Harness/HarnessArtifactObservabilityTests.cs` (+2 multibyte),
  `ProgressEventCoverageTests.cs`, `ChannelProgressReporterTests.cs` (8) —
  cover the admission gate/revision defense, capability resolution matrix,
  UTF-8 attribution, event-type catalogue, and channel backpressure/dispose
  behavior respectively, each described in the relevant section below.

(All paths above are relative to
`src/NexusLabs.Foundry.MicrosoftAgentFramework[.Tests]/`.) Every direct
construction call site of `HarnessHybridCompactionChatClient`/
`HarnessCompactionCompositionRequest` outside the dedicated test fixtures
passes an explicit `progressAccessor`/`ProgressAccessor` value.

## One-top-level-type-per-file disposition

The repository convention is one top-level C# type per file (already
established by G4's identical split for
`HarnessArtifactOffloadDecisionEvent.cs`/`HarnessArtifactRehydrationDecisionEvent.cs`).
This gate honors that convention by giving each of the four new progress
events its own file under `Progress/`:
`HarnessContextCompactionStartedEvent.cs`, `HarnessContextCompactionCompletedEvent.cs`,
`HarnessContextCompactionTerminatedEvent.cs`, and `HarnessContextComposedEvent.cs`.
The diagnostics contract is likewise split across seven separate files under
`Diagnostics/` (the `HarnessContextDiagnostics` record itself, four
single-enum files, one contribution record, and the `HarnessContextAttribution`
record) rather than combined into one file. No file introduced by this gate
contains more than one top-level type.

## Selected extension point and why it sees every provider request

`HarnessHybridCompactionChatClient` is installed as the innermost
`IChatClient` decorator, directly wrapping the real provider client, beneath
every other Foundry-owned middleware (`ApprovalResponseBinding`,
`ApprovalNotRequiredFunctionBypassing`, `FunctionInvokingChatClient`,
`MessageInjectingChatClient`, `HarnessExecutionBindingChatClient`,
`PerServiceCallChatHistoryPersistingChatClient` when configured, telemetry
when configured). Both `FunctionInvokingChatClient` and
`MessageInjectingChatClient` recurse by invoking their own inner client afresh
for every tool round or injected batch respectively, so every such call
cascades fully down to this node; `PerServiceCallChatHistoryPersistingChatClient`
prepends its loaded history before calling its inner client, so this node —
inner to it — always observes the complete, already-prepended message set for
that exact call. A per-call decorator at this exact position is therefore the
only seam that observes every intermediate provider request, not only the
first or last one per outer turn.

This is a deliberate departure from MAF 1.15's built-in
`AIContextProvider`/`CompactionProvider` seam, which is evaluated once per
agent turn against a history index that has not yet observed the current tool
round's result — structurally insufficient for a caller that needs every
intermediate provider request bounded. The node never persists a transient
rehydrated body itself: every call re-adapts the exact messages presented for
that call and assembles bounded context fresh from the caller-supplied
session/snapshot integration, with no singleton mutable history retained by
the node; the only cross-call state it owns is the non-retransmission
coordinator's per-digest lease bookkeeping (reserve/commit/release) and its
per-run provider-call gate, never the bodies themselves. This is what makes
rehydration history-safe: a rehydrated body can be forwarded to the real
provider without ever being written back into persisted session/history
state, so no compacted artifact reference is ever silently replaced by its
resolved body in storage.

## Policy / trigger / fallback / sequence / termination matrix

| Outcome | Category | Meaning | Terminal event |
|---|---|---|---|
| `WithinLimit` | Success | Verified entries fit the hard limit with no reducing proposal and no fallback needed. | Completed |
| `Reduced` | Success | Evicting recoverable bodies and/or a verified, strictly size-reducing reducer proposal brought entries within the hard limit. | Completed |
| `PreservationFallback` | Success | The deterministic preservation-only fallback (required entries plus any optional content that still fits) reached the hard limit after the reducer failed to produce a fitting, verified, strictly-reducing proposal within the attempt bound. | Completed |
| `Irreducible` | Termination | Required (and retained optional) content alone still exceeds the hard limit even after the fallback. | Terminated |
| `ConcurrentMutationLimit` | Termination | Injected entries kept invalidating in-flight proposals until the attempt budget was exhausted before a detected version change could be consumed as a restart. | Terminated |

Stages recorded, in execution order, on `HarnessContextDiagnostics.Stages`:
`SnapshotCaptured` → `RecoverableBodyEviction` → `ReducerAttempt` (repeated up
to the configured bound) → `RestartedAfterMutation` (only when a newer
snapshot version was observed mid-attempt) → `DeterministicFallback` (only
when no accepted reducer proposal converged).

Trigger evaluation: a policy's `TriggerThreshold` is always `HardLimit -
TriggerMargin`; assembly is triggered whenever the current estimated size is
at or above that threshold. Every size, the threshold, and the hard limit are
reported in whatever `HarnessContextMeasurementUnit` the configured
`IHarnessContextSizeEstimator` declares — never assumed to be a provider
token count. `HarnessUtf8ContextSizeEstimator` and the text-only estimator
report `Utf8Bytes`; the fixed/constant test estimators report
`HostDefinedUnits`; `EstimatedTokens` is reserved, unused by any estimator in
this codebase today, for a future tokenizer-backed estimator.

Event sequencing (per assembly attempt, enforced by
`HarnessHybridCompactionChatClient.AssembleBoundedMessagesAsync`), each event
carrying the same per-attempt `AssemblyId` (`Guid`, generated once
immediately before Started — see "Observability and privacy" below for the
full correlation contract):

1. `HarnessContextCompactionStartedEvent` — emitted after message adaptation,
   snapshot integration, and assembler construction have all succeeded,
   immediately before `AssembleAsync` is called. A classifier or
   snapshot-construction exception propagates directly without emitting this
   event or any subsequent event.
2. Exactly one terminal event (for attempts that emitted a Started event):
   - `HarnessContextCompactionCompletedEvent` — reported immediately once a
     success outcome is known, deliberately before the post-assembly
     execution-binding revalidation, so the decision remains observable even
     if that later revalidation fails; or
   - `HarnessContextCompactionTerminatedEvent` — reported immediately before
     `HarnessCompactionIrreducibleException` is thrown for either termination
     outcome (`Irreducible` or `ConcurrentMutationLimit`). The exception
     itself carries only the outcome, final size, and hard limit — never the
     diagnostics instance — so the Terminated event is the only
     caller-observable surface for that instance on termination.
   Exceptional failures during assembly (cancellation, binding invalidation,
   or reducer exception) propagate directly without masquerading as Completed
   or Terminated.
3. `HarnessContextComposedEvent` — reported only after a completed event,
   only once execution-binding revalidation has also passed, and only
   immediately before the bounded messages are returned for dispatch;
   carries the identical `HarnessContextDiagnostics` instance as the
   preceding completed event. Never reported for a terminated attempt.

No event of any kind is reported when the chat client was constructed with a
`null` progress accessor — every `Report*` helper is a no-op in that case,
and ordinary assembly behavior (including throwing on termination) is fully
preserved regardless.

## One-shot rehydration, atomic leases, and same-run admission

Every call to `AssembleBoundedMessagesAsync` generates a fresh lease token and
wraps the configured snapshot provider in a
`HarnessDeliveredSegmentFilteringSnapshotProvider` bound to that lease and the
shared `HarnessCompactionRunCoordinator` for the active outer run. A
recoverable segment body already promoted to Delivered earlier in the same
run scope — or currently reserved by a different, concurrently-running
provider call within the same run — is filtered back out before the
assembler ever considers it. On success, the caller
(`GetResponseAsync`/`GetStreamingResponseAsync`) commits the lease's forwarded
digests to Delivered only once the real provider call itself completes
successfully; on any failure — including a canceled or failed real-provider
call — the lease is released instead, so a subsequent retry within the same
run scope can still reserve, and ultimately deliver, the exact same digests.
This guarantees a given body is dispatched to the real provider at most once
per outer run, while never permanently losing the ability to recover it on a
genuine retry.

Two further mechanisms defend that guarantee against concurrent same-run
provider calls:

- **Per-digest revision defense.** `HarnessCompactionRunCoordinator` tracks a
  monotonic per-digest revision inside the active run's state, incremented on
  every externally-observable reservation/delivery state change for that
  digest: a digest's first reservation, every `Complete` a lease processes
  for that digest (whether promoted to Delivered or released unpromoted),
  and every explicit `Release`. The same lease re-reserving a digest it
  already holds, and a failed reservation attempt, both bump nothing.
  `HarnessDeliveredSegmentFilteringSnapshotProvider` computes its own
  effective monotonic version from the inner snapshot's version plus the
  sorted `(digest, coordinator revision)` pairs for every recoverable
  segment the inner snapshot reports, and reports a strictly greater
  effective version whenever either input changes — even when the inner
  snapshot's own version is unchanged. This closes a gap where a losing
  call's own filtered snapshot would otherwise never observe a winning
  call's later release, because the inner snapshot provider's version only
  changes when a new message is injected, never when purely
  coordinator-internal reservation/delivery state changes.
  `HarnessContextAssembler`'s existing version-comparison restart logic —
  unmodified — restarts an assembly whenever this effective version
  advances, so a call whose reservation is released becomes visible to any
  sibling still active in the same run, and the body is neither
  double-delivered nor permanently lost.
- **Same-run provider-call admission gate.** `HarnessCompactionRunCoordinator`'s
  per-run `RunState` owns a `SemaphoreSlim(1, 1)` gate (`ProviderCallGate`)
  and an `internal async Task<IDisposable> EnterProviderCallAsync(CancellationToken)`
  method that requires the same active run scope every other lease-lifecycle
  method requires, checks for a pre-canceled token before touching any state
  (so a pre-canceled token throws the same `OperationCanceledException` type
  a mid-wait cancellation does), then awaits the gate and returns an
  idempotent releaser. `HarnessHybridCompactionChatClient.GetResponseAsync`
  and `GetStreamingResponseAsync` each acquire this gate via a `using`
  declaration immediately after `EnsureRunScope()` and before assembly
  begins, holding it across assembly, real dispatch, and the
  `Complete`/`Release` decision — the entire method/iterator, never released
  early. A canceled wait creates no reservation and leaves the gate exactly
  as if the call had never been made. Because the gate lives on `RunState`
  (the same `AsyncLocal<RunState>`-scoped instance the reservation protocol
  already uses), two different **outer** runs — each its own `RunState` —
  remain fully concurrent; only nested calls *within one outer run* are
  serialized. This makes two same-run calls racing each other into (or
  through) the real provider structurally impossible, independent of the
  revision-bump defense-in-depth above.

`HarnessCompactionRunCoordinatorTests.cs` (25 tests) proves both mechanisms:
a deterministic, single-threaded test reproduces cross-release visibility
end to end (filtered first capture, release-without-forwarding, a
version-changed second capture with the body restored, and no later
re-reservation once delivered); a real, genuinely-concurrent barrier-gated
test proves the admission gate blocks a second same-run call until the first
completes and releases, after which the raw body is delivered exactly once;
a cancellation-while-waiting test proves the gate is left unaffected for a
subsequent caller; direct unit tests cover `GetRevision`'s scope and
advance-only-on-real-change behavior; and an unmodified test continues
proving two concurrent **outer** runs proceed fully independently.

## Bounded, backpressured progress delivery

`ChannelProgressReporter.Report` enqueues to its bounded `Channel<T>` via
`ChannelWriter<T>.WriteAsync` (Wait mode — never `TryWrite`, so a full
channel never silently drops an event). Whenever capacity is available — the
common case — the enqueue completes synchronously and `Report` returns
immediately. Only when the channel is momentarily saturated does `Report`
itself synchronously block on the pending write, applying backpressure
directly to the caller instead of accumulating an unbounded set of
fire-and-forget pending-write tasks; sink I/O itself remains entirely on the
background consumer task, and `Report` never runs a sink directly. Writing
after the channel has already been completed (e.g. after `DisposeAsync`)
never throws synchronously out of `Report`: the resulting
`ChannelClosedException` — the one specific, expected exception for this
condition — is caught and surfaced through the existing, non-throwing
`IProgressReporterErrorHandler` path, consistent with how every other
enqueue failure is already reported. Any other, unexpected exception from
that wait is not caught and propagates directly to the caller, so it is
never reshaped into a handled error or otherwise hidden. `DisposeAsync` is a
plain complete-then-drain (`_channel.Writer.TryComplete()` then await the
consumer task): because `Report` never leaves an enqueue running in the
background unobserved — it either completes synchronously or the caller's
own call is the one waiting on it — there is no separate pending-write set
for disposal to wait on.

`ChannelProgressReporterTests.cs` (8 tests) proves: the fast synchronous path
when capacity is available; genuine producer-side blocking under a
capacity-1 saturated channel, with the blocked `Report` call running on its
own background `Task` (since it now blocks the calling thread), staying
incomplete while the sink is deliberately held open, and completing once the
sink releases, with all events still delivered exactly once and in channel
order; and that enqueuing after `DisposeAsync` surfaces `ChannelClosedException`
through the recorded error handler synchronously (by the time `Report`
returns), never thrown out of `Report` and never silently dropped. Class and
interface docs (`ChannelProgressReporter.cs`, `IProgressSink.cs`,
`docs/progress-reporting.md`) describe delivery as non-blocking while
capacity exists and bounded/backpressured once the channel saturates.

## Cancellation taxonomy and message-injection behavior

`HarnessCompactionCancellationTests.cs` proves every named cancellation
surface always throws `OperationCanceledException` or, for trust-binding
invalidation, `InvalidOperationException` — never a successful response and
never a silently swallowed fallback, with no broad catch anywhere in the
path:

- a pre-canceled token, both non-streaming (`GetResponseAsync`) and
  streaming (`GetStreamingResponseAsync`) — the real provider leaf is never
  invoked;
- cancellation raised by the upstream reducer itself during a bounded
  attempt;
- cancellation observed at the exact instant assembly finishes but before
  dispatch (a dedicated checkpoint distinct from the assembler's own internal
  checks);
- cancellation during a message-injection-induced extra provider call (an
  injected batch's own recursive call into this node is subject to the same
  cancellation checks as any other call);
- cancellation during snapshot/finalization capture, and while waiting on
  the same-run provider-call admission gate; and
- trust-binding invalidation detected between successful assembly and
  dispatch, both non-streaming and streaming — `EnsureCurrent` is checked
  once at entry and again immediately after a successful assembly, right
  before dispatch, so a binding that becomes stale during an
  observable-duration assembly is still caught before any bounded message is
  ever handed to the real provider.

`MessageInjectingChatClient` recurses into its own inner client afresh for
every injected batch, so an injection-driven extra call is observed by this
node exactly like any other provider call — its own started/terminal/composed
events are reported independently, correlated by that call's own
`SequenceNumber` and `AssemblyId`, never merged into or confused with the
triggering call's events.

## Observability and privacy

- **Contract.** `HarnessContextDiagnostics` carries: outcome category (5
  values, the 2 termination members doubling as the termination category),
  explicit `HarnessContextMeasurementUnit`, original/final sizes, trigger
  threshold, hard limit, attempt count, ordered stage categories, per-category
  final size/entry-count contributions (empty on termination), and a final
  sequence-validity flag (`true` on success, `null` on termination). Private
  constructors plus `ForSuccess`/`ForTermination`/`Create` factories reject any
  invalid outcome, undefined enum value, negative size/threshold/limit/
  attempt/entry-count, a final size exceeding the hard limit on success,
  duplicate categories, or a contribution-size sum (checked against `int`
  overflow) that disagrees with the final size on success.
  `HarnessContextDiagnosticsFactory.Create` maps the internal result to the
  public snapshot entirely through switch expressions that throw
  `ArgumentOutOfRangeException` on any unrecognized internal value — never by
  parsing an exception or evidence string; a negative estimator result is
  rejected with `InvalidOperationException`.
- **No new ambient singleton.** The chat client and the compaction composer's
  request record each accept a required nullable `IProgressReporterAccessor?`
  at their existing construction/request seam — no new ambient singleton,
  and no optional parameter anywhere in this gate's public or internal
  signatures.
- **Correlation.** Every event carries `WorkflowId`, `AgentId`,
  `ParentAgentId` (via `IProgressReporterContext`), `Depth`, `SequenceNumber`,
  and `AssemblyId`, matching the G4 `HarnessArtifactOffloadDecisionEvent`/
  `HarnessArtifactRehydrationDecisionEvent` pattern. A root→child→grandchild
  nested-reporter test proves the three-level tree and one shared, strictly
  increasing `SequenceNumber`; a concurrency test proves two genuinely
  concurrent same-agent assemblies each produce their own distinct
  `AssemblyId`, with each attempt's Started/Completed/Composed trio
  remaining internally pairable despite interleaved `SequenceNumber`s.
- **Binding-revalidation ordering.** When the trusted execution binding is
  invalidated between a successful assembly decision and the post-assembly
  `EnsureCurrent` revalidation, the already-emitted Started/Completed events
  remain as reported, neither Composed nor Terminated is ever emitted (this
  is not itself a structured `Irreducible`/`ConcurrentMutationLimit`
  outcome), and `InvalidOperationException` propagates to the caller.
- **Attribution across offload/rehydration.** `HarnessContextAttribution`
  carries `Operation`, `InputUtf8Bytes`, and `OutputUtf8Bytes` (nullable),
  always in UTF-8 bytes; `OutputUtf8Bytes` is null for `Failed`/
  `RecoveryRequired` offloads and every non-`Resolved` rehydration outcome.
  `HarnessArtifactDiagnostics.ForOffload`/`.ForRehydration` build a matching
  attribution automatically, so the same value rides on the snapshot already
  attached to the internal result and its progress event — no second,
  independently computed value exists anywhere. Dedicated multibyte tests
  assert every byte count matches `Encoding.UTF8.GetByteCount` exactly, never
  a UTF-16 char/code-unit count.
- **Never present anywhere in the public surface:** raw message text,
  artifact bodies, workspace paths, owner identities, tool arguments/results,
  exception text, or classifier output text. Confirmed by a reflection-based
  test (`Events_NeverContainRawMessageText`) that recursively walks every
  public string property and each record's default `ToString()` across every
  emitted event, seeded with distinctive marker text that never appears.
- **Diagnostics/progress identity.** The exact same `HarnessContextDiagnostics`
  instance is carried by the dispatch result, the Completed (or Terminated)
  event, and — on success — the Composed event; never a second,
  independently rebuilt snapshot. On termination, no caller-visible internal
  result carries the diagnostics instance — the Terminated event is the only
  caller-observable surface for it.

## Session/history safety

The chat client never persists a rehydrated body back into session or
conversation history storage: every call re-adapts the exact messages
presented for that call and assembles bounded context fresh from the
caller-supplied session/snapshot integration on each invocation. Combined
with the one-shot delivery coordinator's purely in-memory, per-run lease
bookkeeping (never the artifact bodies themselves), this means a stored
conversation transcript and any persisted session state are always
unaffected by whether, or how many times, hybrid compaction ran — rehydration
is a transient, per-call, per-provider-request concern, never a mutation of
durable history. `HarnessCompactionSessionIntegrationTests.cs` and
`HarnessCompactionSeamTests.cs` cover this directly against the real
session/snapshot integration seam. Retention/deletion behavior for
workspace-backed artifacts referenced from compacted conversation history is
unchanged from ADR-0006 (no new retention or deletion behavior is introduced
by this gate).

## Public API disposition

The only public API surface introduced by this gate is:

| Type | Kind | Namespace |
|---|---|---|
| `HarnessContextCompactionOutcome` | enum | `NexusLabs.Foundry.MicrosoftAgentFramework.Diagnostics` |
| `HarnessContextAssemblyStageCategory` | enum | `NexusLabs.Foundry.MicrosoftAgentFramework.Diagnostics` |
| `HarnessContextCategory` | enum | `NexusLabs.Foundry.MicrosoftAgentFramework.Diagnostics` |
| `HarnessContextMeasurementUnit` | enum | `NexusLabs.Foundry.MicrosoftAgentFramework.Diagnostics` |
| `HarnessContextCategoryContribution` | sealed record | `NexusLabs.Foundry.MicrosoftAgentFramework.Diagnostics` |
| `HarnessContextDiagnostics` | sealed record | `NexusLabs.Foundry.MicrosoftAgentFramework.Diagnostics` |
| `HarnessContextAttribution` | sealed record | `NexusLabs.Foundry.MicrosoftAgentFramework.Diagnostics` |
| `HarnessContextCompactionStartedEvent` | sealed record : `IProgressEvent` | `NexusLabs.Foundry.MicrosoftAgentFramework.Progress` |
| `HarnessContextCompactionCompletedEvent` | sealed record : `IProgressEvent` | `NexusLabs.Foundry.MicrosoftAgentFramework.Progress` |
| `HarnessContextCompactionTerminatedEvent` | sealed record : `IProgressEvent` | `NexusLabs.Foundry.MicrosoftAgentFramework.Progress` |
| `HarnessContextComposedEvent` | sealed record : `IProgressEvent` | `NexusLabs.Foundry.MicrosoftAgentFramework.Progress` |

Every public member of every type above carries complete XML documentation
(summary, and `<param>`/`<exception>` where applicable). None introduces an
optional parameter or a default interface member. `HarnessArtifactDiagnostics.Attribution`
is a new public property on an existing public record, not a new type; it is
populated automatically inside the existing `internal` `ForOffload`/`ForRehydration`
factories.

`IHarnessContextSizeEstimator`, `HarnessContextDiagnosticsFactory`,
`HarnessHybridCompactionChatClient`, `HarnessCompactionComposition`,
`HarnessCompactionCompositionRequest`, `HarnessHybridContextPolicy`,
`HarnessCompactionRunCoordinator`, and every other compaction
mechanism/request/result/status type remain `internal`, unchanged in
visibility from prior gates. `HarnessCapability.Compaction`'s resolver
definition is data (field values on an existing internal definition), not
new public API.

`IAgentRunDiagnostics`, `IDiagnosticsSink`, and `IAgentMetrics` are
deliberately **not** expanded by this gate, matching the same G4 precedent:
the diagnostics snapshot attached to a compaction/composition decision is
available directly through that decision's own result or its corresponding
progress event; aggregating either into a completed-run diagnostics summary,
a sink, or a metrics surface is recorded below as later hardening scope, to
avoid a duplicate writer for the same underlying decision.

## Capability disposition

| Field | Value |
|---|---|
| Capability | `HarnessCapability.Compaction` |
| Stability | `Experimental` |
| `DefaultEnabledInBundle` | `false` |
| Source package | `Microsoft.Agents.AI` |
| Trust boundary | `ExternalContent` |
| AOT status | `Unverified` |
| Diagnostics status | `Available` |
| Delivery phase | `G5` |

> **Diagnostics status footnote.** `DiagnosticsStatus.Available` records that
> the progress/diagnostics *contract* is fully implemented for this
> capability. It does **not** assert that the `Compaction` capability itself
> is activated, enabled by default, or ready for general use — that remains
> governed independently by `Stability: Experimental` and
> `DefaultEnabledInBundle: false` above.

Resolution matrix (`HarnessCapabilityProfileTests.cs`):

| Requested | Accepted | Evidence through `G5`? | Resolved state |
|---|---|---|---|
| No | — | — | `Disabled` |
| Yes | No | — | `Disabled` |
| Yes | Yes | No (evidence only through an earlier phase) | `Deferred` |
| Yes | Yes | Yes | `Enabled` |

"Not requested" is `Disabled` regardless of acceptance or evidence phase —
acceptance and phase are never independently sufficient to enable a
capability the caller never asked for. `DefaultEnabledInBundle: false` is
additionally covered by the pre-existing `Resolve_CompleteBundleBeforeG6_DefersBundleDefaults`
bundle-lane test, which already proves an unrequested experimental capability
stays off by default within the complete-bundle lane.

Trust boundary is `ExternalContent` rather than `None` because hybrid-compacted
context is, ultimately, host-classified external/conversational content
flowing through the exact same message-classification path every other
context entry already flows through. AOT status is `Unverified` rather than
`Verified` because no NativeAOT application in this codebase directly
executes hybrid composition end-to-end; existing hosted Harness AOT coverage
only compiles the composition graph (proving the types are trim/AOT-safe to
construct), never runs a hybrid-enabled agent under NativeAOT. `Compatible`
was considered and rejected as overstating current evidence.

## Accepted limitations and deferrals (cumulative)

- **Estimator is not token-exact unless labeled.** A `Utf8Bytes`- or
  `HostDefinedUnits`-labeled estimator's hard limit is a byte or arbitrary
  bound, not a provider token bound, even though byte counts and token
  counts are sometimes loosely correlated. No estimator in this codebase
  reports `EstimatedTokens` today.
- **Arbitrary reducer/provider behavior is out of scope.** The configured
  upstream `IChatReducer`'s actual reduction quality, latency, and any
  provider-specific behavior remain outside this decision's control; this
  gate bounds correctness (never accepting a non-reducing or structurally
  invalid proposal) and attempt count, not reducer quality.
- **No completed-run diagnostics aggregation.** The structured compaction
  diagnostics are inspectable per-decision (via the attached result or the
  corresponding progress event) but are not yet aggregated into
  `IAgentRunDiagnostics`, `IDiagnosticsSink`, or `IAgentMetrics`. Deferred to
  later hardening scope, consistent with the identical G4 offload/rehydration
  limitation.
- **No direct NativeAOT execution of the hybrid profile.** The reason
  `AotStatus` is `Unverified` rather than `Verified`; existing hosted
  Harness AOT coverage compiles the graph but does not execute a
  hybrid-enabled agent under NativeAOT.
- **Public runtime configuration remains deferred.** No public API exists
  for selecting, tuning, or composing a hybrid profile; every policy input
  remains an internal construction-time value, per this gate's explicit
  scope boundary.
- **Retention/deletion inherited from ADR-0006.** This gate introduces no
  new retention or deletion behavior for workspace-backed artifacts
  referenced from compacted conversation history; ADR-0006's accepted
  no-delete limitation continues to apply unchanged.

## Review dispositions (final)

Independent review — spanning the initial observability leaf, a dedicated
concurrency-blocker pass, and a final same-run admission/backpressure pass —
raised the following findings. Every one below was adopted in this gate's
current, cumulative state; none changed the `PASS` disposition:

1. **Per-assembly correlation.** Added the public `Guid AssemblyId` (see
   "Observability and privacy" above), proven by a dedicated concurrency
   test with two genuinely concurrent same-agent assemblies.
2. **ADR-0007 identity wording corrected.** The ADR previously overclaimed
   shared-instance diagnostics identity for *both* success and termination;
   only success shares one instance across the dispatch result, completed
   event, and composed event. Corrected in the ADR's Decision and
   Consequences sections.
3. **Gate capability footnote for `DiagnosticsStatus`.** Added the footnote
   under "Capability disposition" above clarifying that `Available` records
   only that the progress/diagnostics *contract* is implemented, not that
   the capability itself is activated.
4. **One-shot rehydration cross-release visibility.** Overlapping same-run
   provider calls could previously race such that a released-but-unpromoted
   reservation was never observed by a sibling call, causing zero delivery
   for the run. Fixed by the per-digest revision defense under "One-shot
   rehydration, atomic leases, and same-run admission" above.
5. **Same-run provider-call admission.** The revision fix alone did not
   prevent two same-run calls from dispatching filtered context to the real
   provider while a sibling reservation was still unresolved. Fixed by the
   per-run provider-call admission gate in the same section above.
6. **`ChannelProgressReporter` bounded memory and exception specificity.**
   An earlier fire-and-forget observer per saturated write, tracked in an
   unbounded background collection, could grow without bound under
   sustained load. Replaced by the genuine producer-side backpressure
   described under "Bounded, backpressured progress delivery" above, with
   `Report`'s catch clause narrowed to the one specific, expected
   `ChannelClosedException` rather than a broad `catch (Exception)`, so any
   other, unexpected exception from the pending write propagates to the
   caller instead of being reshaped into a handled error.

## Local validation

```powershell
$env:NUGET_PACKAGES='G:\dev\caches\nuget\packages'
dotnet build src\NexusLabs.Foundry.MicrosoftAgentFramework\NexusLabs.Foundry.MicrosoftAgentFramework.csproj -c Debug
dotnet build src\NexusLabs.Foundry.MicrosoftAgentFramework.Tests\NexusLabs.Foundry.MicrosoftAgentFramework.Tests.csproj -c Debug
dotnet test src\NexusLabs.Foundry.MicrosoftAgentFramework.Tests\NexusLabs.Foundry.MicrosoftAgentFramework.Tests.csproj -c Debug --filter "FullyQualifiedName~ChannelProgressReporterTests"
dotnet test src\NexusLabs.Foundry.MicrosoftAgentFramework.Tests\NexusLabs.Foundry.MicrosoftAgentFramework.Tests.csproj -c Debug --filter "FullyQualifiedName~HarnessContextObservabilityTests|FullyQualifiedName~HarnessContextDiagnosticsValidationTests"
dotnet test src\NexusLabs.Foundry.MicrosoftAgentFramework.Tests\NexusLabs.Foundry.MicrosoftAgentFramework.Tests.csproj -c Debug --filter "FullyQualifiedName~HarnessCompactionRunCoordinatorTests|FullyQualifiedName~HarnessContextAssembler|FullyQualifiedName~ConcurrentProviderCalls|FullyQualifiedName~Concurrent|FullyQualifiedName~ChannelProgressReporterTests"
dotnet test src\NexusLabs.Foundry.MicrosoftAgentFramework.Tests\NexusLabs.Foundry.MicrosoftAgentFramework.Tests.csproj -c Debug --filter "FullyQualifiedName~Harness"
dotnet test src\NexusLabs.Foundry.MicrosoftAgentFramework.Tests\NexusLabs.Foundry.MicrosoftAgentFramework.Tests.csproj -c Debug
dotnet build src\NexusLabs.Foundry.slnx -c Debug
```

Results (final measurement against the complete cumulative gate, including
the narrowed `ChannelClosedException` catch in `ChannelProgressReporter.Report`):

- `ChannelProgressReporterTests` in isolation: **8 passed, 0 failed**.
- New/modified test classes in isolation
  (`HarnessContextObservabilityTests` + `HarnessContextDiagnosticsValidationTests`):
  **27 passed, 0 failed** (14 observability and 13 diagnostics-validation
  tests).
- Coordinator/concurrency/channel/progress filter, combining every area
  touched by the admission-gate and backpressure fixes
  (`HarnessCompactionRunCoordinatorTests` + `HarnessContextAssembler` +
  `ConcurrentProviderCalls` + `Concurrent` + `ChannelProgressReporterTests`):
  **53 passed, 0 failed**, run **3 consecutive times** with identical,
  stable results each time.
- Full Harness filter (`FullyQualifiedName~Harness`): **610 passed, 0
  failed**, re-run **2 consecutive times** with identical, stable results
  each time.
- Full `NexusLabs.Foundry.MicrosoftAgentFramework.Tests` project: **2,181
  passed, 0 failed**, re-run **2 consecutive times** with identical, stable
  results each time.
- Full `src` solution build (`dotnet build src\NexusLabs.Foundry.slnx -c Debug`):
  succeeded, 0 errors; the same 4 pre-existing generated-code `CS0162`
  warnings in unrelated example apps (`DagRoutingApp.Agents`,
  `GeneratorCoexistenceApp`, `DevUIApp` twice), not touched by any change in
  this gate.

## Validation status and next permitted gates

- **Local:** complete — targeted new-test filters, a 3x-repeated
  coordinator/concurrency/channel/progress filter, a 2x-repeated full Harness
  filter, a 2x-repeated full project test suite, and a full solution build
  all pass with zero regressions and zero observed flakiness.
- **Review:** independent correctness, MAF-order, and architecture review
  completed; every finding in "Review dispositions (final)" above was
  adopted and revalidated.
- **Hosted CI:** passed for the final G5 integration state through PR #108:
  [build/test/package](https://github.com/ncosentino/foundry/actions/runs/30181327414/job/89738244873),
  [standard NativeAOT](https://github.com/ncosentino/foundry/actions/runs/30181327414/job/89738244885),
  [Harness NativeAOT](https://github.com/ncosentino/foundry/actions/runs/30181327413/job/89738244866),
  and
  [documentation](https://github.com/ncosentino/foundry/actions/runs/30181327430/job/89738245016).
- **Next permitted gates:** G6 (background agents, loop evaluation) and G7
  (test/AOT hardening, including the completed-run diagnostics aggregation
  this gate explicitly deferred) per the dependency graph in `tasks.md`; G5
  is cumulatively complete. Genuine producer-side flow control for the
  progress-event channel — previously a possible G7 follow-up — was
  implemented in this gate rather than deferred.

## ADR reference

See `docs/adr/adr-0007-experimental-hybrid-context-compaction.md` for the
full architectural decision record backing this gate: context/scope,
decision drivers, the decision itself, alternatives considered, and
consequences (positive, negative, and neutral).
