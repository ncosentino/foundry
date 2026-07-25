# Gate G5 Decision — Experimental Hybrid Context Compaction Observability

## Decision

**PASS for the cumulative G5 experimental hybrid context compaction slice,
including this leaf's observability contract — pending independent review
and hosted CI.**

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
3. **One-shot rehydration delivery per outer run** — a non-retransmission
   coordinator reserves a lease per digest per provider call so a rehydrated
   body is delivered to the real provider at most once per outer run; a
   nested or later call within the same run observes it filtered back out of
   its own snapshot.
4. **Compaction/context-composition observability (this leaf)** — a
   privacy-safe `HarnessContextDiagnostics` snapshot and four public progress
   events (started, completed, terminated, composed) reporting every
   assembly attempt's outcome, sizes, stages, and final per-category
   contribution, wired through the existing progress-reporter accessor seam
   with no new ambient singleton and no optional parameter; an explicit
   `HarnessContextMeasurementUnit` on every size estimator so no generic
   integer is ever mislabeled as a provider token count; and a shared
   `HarnessContextAttribution` UTF-8 byte snapshot riding along on the
   existing `HarnessArtifactDiagnostics` contract for both offload and
   rehydration decisions.

`HarnessProviderComposition` remains the sole selected-provider composition
root; `HarnessCompactionComposition` is invoked internally by it against the
exact same resolved capability profile and installs at most one
`HarnessHybridCompactionChatClient`, never a second, independently-invoked
composition root and never a second competing compaction component.
Conversation compaction policy was explicitly out of scope for ADR-0006; this
gate and ADR-0007 are where that policy, and its observability, are decided.

No public Harness runtime configuration/composition API is approved by this
gate. The only public API surface introduced by this leaf is the
observability contract: six enums, three records (`HarnessContextDiagnostics`,
`HarnessContextCategoryContribution`, `HarnessContextAttribution`), and four
`IProgressEvent` records. Every compaction mechanism, request, result,
policy, and status type — including the estimator interface itself — remains
`internal`.

## Evidence identity

Cumulative history on top of the G4 foundation gate (`gate-g4.md`):

| Item | Cumulative Harness tests | Cumulative project tests | Delta |
|---|---|---|---|
| G4 final (baseline) | 305 | 1,874 | — |
| #99 | 420 | 1,989 | +115 |
| #100 | 480 | 2,049 | +60 |
| #101 | 569 | 2,138 | +89 |
| **Compaction/context-composition observability (this leaf)** | **600** | **2,169** | **+31** |

- Cumulative counts through #101 were supplied as known prior measurements for
  this branch's history; this leaf's own +31 delta was measured directly
  against the current working tree via `dotnet test`, not estimated from
  source occurrences.
- This leaf's 31 new tests break down as: 8 in the new
  `HarnessContextObservabilityTests.cs` covering exactly-once Started/Completed/
  Terminated/Composed emission per outcome, measurement-unit correctness, the
  absent-profile no-op case, and the privacy sweep for raw message text; 3 in
  `HarnessCapabilityProfileTests.cs` asserting the Compaction capability's
  exact requested+accepted/deferred/disabled resolution matrix and evidence
  field values; 2 in `HarnessArtifactObservabilityTests.cs` proving UTF-8 byte
  counts (never UTF-16 char/code-unit counts) are reported for multibyte
  offload/rehydration content; 13 in the new
  `HarnessContextDiagnosticsValidationTests.cs` covering negative-estimator
  rejection, checked-sum overflow, duplicate-category rejection,
  non-positive-entry-count/negative-size rejection, undefined-`HarnessContextMeasurementUnit`
  rejection on both `ForSuccess` and `ForTermination`, undefined-`HarnessContextCategory`
  rejection on `HarnessContextCategoryContribution.Create`, and `ForSuccess`'s
  own defensive rejection of an undefined category on a contribution
  constructed by bypassing `Create` via reflection; a classifier exception and
  a null snapshot-integration result before assembly begins must each emit no
  progress event (2 more in `HarnessContextObservabilityTests.cs`); and a
  root→child→grandchild nested `IProgressReporter` scope carries
  `WorkflowId`/`AgentId`/`ParentAgentId`/`Depth` and a strictly increasing
  shared `SequenceNumber` across Started/Completed/Composed for a successful
  assembly and across Started/Terminated for an irreducible one (2 more in
  `HarnessContextObservabilityTests.cs`), plus one test pinning that a binding
  invalidated between the successful-assembly decision and the post-assembly
  trust revalidation still leaves Completed observable while Composed and
  Terminated are both never emitted and `InvalidOperationException`
  propagates directly (1 more in `HarnessContextObservabilityTests.cs`) — for
  a total of 13 tests in `HarnessContextObservabilityTests.cs`.
- The 10 pre-existing offload/rehydration outcome tests in
  `HarnessArtifactObservabilityTests.cs` were each extended in place with new
  `Attribution` assertions rather than duplicated into new test methods, so
  they contribute no additional count here despite carrying new coverage.
- All `dotnet build`/`dotnet test` commands for this leaf were run with
  `$env:NUGET_PACKAGES='G:\dev\caches\nuget\packages'` set.

## Files changed (current leaf — compaction/context-composition observability)

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
- `src/NexusLabs.Foundry.MicrosoftAgentFramework.Tests/Harness/HarnessContextObservabilityTests.cs` (13 tests)
- `src/NexusLabs.Foundry.MicrosoftAgentFramework.Tests/Harness/HarnessContextDiagnosticsValidationTests.cs` (13 tests)
- `docs/adr/adr-0007-experimental-hybrid-context-compaction.md`
- `specs/001-maf-harness-first-class/evidence/gate-g5.md` (this file)

Modified files:
- `src/NexusLabs.Foundry.MicrosoftAgentFramework/Harness/Context/HarnessHybridCompactionChatClient.cs`
  — added a required nullable trailing `IProgressReporterAccessor? progressAccessor`
  constructor parameter (no optional parameter); `AssembleBoundedMessagesAsync`
  now reports exactly one started event before assembly begins and exactly
  one terminal event per attempt — a terminated event immediately before
  throwing `HarnessCompactionIrreducibleException` on failure, or a completed
  event immediately once a success outcome is known (deliberately before the
  post-assembly execution-binding revalidation, so an already-successful
  decision remains observable even if that later revalidation itself fails)
  — and, only once that revalidation has also passed, a composed event
  carrying the identical diagnostics instance as the completed event, right
  before the bounded messages are returned for dispatch. Four private
  `Report*` helper methods build each event's correlation fields
  (`Timestamp`, `WorkflowId`, `AgentId`, `ParentAgentId` via
  `IProgressReporterContext`, `Depth`, `SequenceNumber`) directly from the
  current `IProgressReporter`, mirroring the existing
  `HarnessArtifactOffloadDecisionEvent`/`HarnessArtifactRehydrationDecisionEvent`
  pattern; each helper is a no-op when the accessor is `null`.
- `src/NexusLabs.Foundry.MicrosoftAgentFramework/Harness/Context/HarnessCompactionComposition.cs`,
  `HarnessCompactionCompositionRequest.cs` — threaded a required nullable
  trailing `IProgressReporterAccessor? ProgressAccessor` member/parameter
  through the composer's request record and its construction of
  `HarnessHybridCompactionChatClient`.
- `src/NexusLabs.Foundry.MicrosoftAgentFramework/Harness/HarnessProviderComposition.cs`
  — the compaction composition call site now passes the existing
  `HarnessProviderCompositionRequest.ProgressAccessor` value through, rather
  than introducing a second, independent accessor seam.
- `src/NexusLabs.Foundry.MicrosoftAgentFramework/Harness/Context/HarnessHybridContextPolicy.cs`,
  `IHarnessContextSizeEstimator.cs`, `HarnessUtf8ContextSizeEstimator.cs`,
  `HarnessUtf8TextSizeEstimator.cs` — added a required
  `HarnessContextMeasurementUnit MeasurementUnit` property to
  `IHarnessContextSizeEstimator`; both production estimators
  (`HarnessUtf8ContextSizeEstimator`, the text-only estimator) report
  `Utf8Bytes`.
- `src/NexusLabs.Foundry.MicrosoftAgentFramework/Diagnostics/HarnessArtifactDiagnostics.cs`
  — added a public `Attribution` property; `ForOffload`/`ForRehydration` now
  build a matching `HarnessContextAttribution.ForOffload`/`.ForRehydration`
  snapshot automatically from the same outcome/observed-size/reference-id
  inputs already supplied, so every existing offload/rehydration call site
  needed no changes beyond the factory bodies themselves.
- `src/NexusLabs.Foundry.MicrosoftAgentFramework/Harness/Capabilities/HarnessCapabilityResolver.cs`
  — the `Compaction` definition's `TrustBoundary` changed from `None` to
  `ExternalContent` (hybrid-compacted context is ultimately host-classified
  external/conversational content flowing through the same classification
  path as every other context entry) and its `AotStatus` changed from
  `Verified` to `Unverified` (no NativeAOT app directly executes hybrid
  composition; existing hosted Harness AOT coverage only compiles the
  composition graph). `DiagnosticsStatus` is `Available` (this leaf).
  `Stability` remains `Experimental`, `DefaultEnabledInBundle` remains
  `false`, and `DeliveryPhase` remains `G5`.
- `src/NexusLabs.Foundry.MicrosoftAgentFramework.Tests/Harness/HarnessConstantSizeContextEstimator.cs`,
  `HarnessFixedSizeContextEstimator.cs` — both fixture estimators now report
  `HarnessContextMeasurementUnit.HostDefinedUnits`, since neither measures
  bytes or tokens.
- `src/NexusLabs.Foundry.MicrosoftAgentFramework.Tests/Harness/HarnessCompactionCancellationTests.cs`,
  `HarnessCompactionComposeTests.cs`, `HarnessCompactionRunCoordinatorTests.cs`,
  `HarnessCompactionSeamTests.cs`, `HarnessCompactionSessionIntegrationTests.cs`,
  `HarnessProviderCompositionCompactionTests.cs` — updated direct
  `HarnessHybridCompactionChatClient` construction call sites for the new
  trailing `progressAccessor` parameter (explicit `null` in every case except
  where a test specifically exercises reporting).
- `src/NexusLabs.Foundry.MicrosoftAgentFramework.Tests/Harness/HarnessCapabilityProfileTests.cs`
  — added 3 new tests asserting the Compaction capability's exact
  requested+accepted/deferred/disabled resolution matrix and exact evidence
  field values.
- `src/NexusLabs.Foundry.MicrosoftAgentFramework.Tests/Harness/HarnessArtifactObservabilityTests.cs`
  — every existing offload/rehydration outcome test now also asserts
  `Attribution.Operation`/`InputUtf8Bytes`/`OutputUtf8Bytes`; added 2 new
  dedicated multibyte tests proving UTF-8 byte counts, never UTF-16
  char/code-unit counts, are reported for both offload and rehydration.
- `src/NexusLabs.Foundry.MicrosoftAgentFramework.Tests/ProgressEventCoverageTests.cs`
  — added the 4 new event type names to the hard-coded catalogue of every
  known `IProgressEvent` implementation.

Every existing direct construction call site of `HarnessHybridCompactionChatClient`
and `HarnessCompactionCompositionRequest` outside this leaf's own new test
file passes an explicit `progressAccessor`/`ProgressAccessor` value — no call
site was left uncompiled or defaulted.

## One-top-level-type-per-file disposition

The issue names a single plural file, `Progress/HarnessContextProgressEvents.cs`,
for the new progress events. The repository convention is one top-level C#
type per file (already established by G4's identical split for
`HarnessArtifactOffloadDecisionEvent.cs`/`HarnessArtifactRehydrationDecisionEvent.cs`).
This leaf honors that convention by giving each of the four new progress
events its own file under `Progress/`:
`HarnessContextCompactionStartedEvent.cs`, `HarnessContextCompactionCompletedEvent.cs`,
`HarnessContextCompactionTerminatedEvent.cs`, and `HarnessContextComposedEvent.cs`.
The diagnostics contract is likewise split across seven separate files under
`Diagnostics/` (the `HarnessContextDiagnostics` record itself, four
single-enum files, one contribution record, and the `HarnessContextAttribution`
record) rather than combined into one file. No file introduced by this leaf
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
coordinator's per-digest lease bookkeeping (reserve/commit/release), never
the bodies themselves.

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
`IHarnessContextSizeEstimator` declares.

Event sequencing (per assembly attempt, enforced by
`HarnessHybridCompactionChatClient.AssembleBoundedMessagesAsync`):

1. `HarnessContextCompactionStartedEvent` — emitted after message adaptation,
   snapshot integration, and assembler construction have all succeeded,
   immediately before `AssembleAsync` is called. A classifier or
   snapshot-construction exception propagates directly without emitting this
   event or any subsequent event — no dangling Started event is ever emitted
   for an attempt that never reached the assembler.
2. Exactly one terminal event (for attempts that emitted a Started event):
   - `HarnessContextCompactionCompletedEvent` — reported immediately once a
     success outcome is known, deliberately before the post-assembly
     execution-binding revalidation, so the decision remains observable even
     if that later revalidation fails; or
   - `HarnessContextCompactionTerminatedEvent` — reported immediately before
     `HarnessCompactionIrreducibleException` is thrown for either termination
     outcome (`Irreducible` or `ConcurrentMutationLimit`).
   Exceptional failures during assembly (cancellation, binding invalidation,
   or reducer exception) propagate directly without masquerading as Completed
   or Terminated — no Terminated event is emitted for a non-structured failure.
3. `HarnessContextComposedEvent` — reported only after a completed event,
   only once execution-binding revalidation has also passed, and only
   immediately before the bounded messages are returned for dispatch;
   carries the identical `HarnessContextDiagnostics` instance as the
   preceding completed event and the `HarnessBoundedMessageAssembly`
   dispatch result. Never reported for a terminated attempt.

No event of any kind is reported when the chat client was constructed with a
`null` progress accessor — every `Report*` helper is a no-op in that case, and
ordinary assembly behavior (including throwing on termination) is fully
preserved regardless.

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
- cancellation during snapshot/finalization capture; and
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
`SequenceNumber`, never merged into or confused with the triggering call's
events.

## One-shot rehydration / non-retransmission

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

## Observability and privacy (this leaf)

- **Contract.** `HarnessContextDiagnostics` carries: outcome category (5
  values, the 2 termination members doubling as the termination category),
  explicit `HarnessContextMeasurementUnit`, original/final sizes, trigger
  threshold, hard limit, attempt count, ordered stage categories, per-category
  final size/entry-count contributions (empty on termination), and a final
  sequence-validity flag (`true` on success, `null` on termination). Private
  constructor plus `ForSuccess`/`ForTermination` factories enforce every
  invariant: only a valid success/termination outcome for each factory, only a
  defined `HarnessContextMeasurementUnit` value, no negative
  size/threshold/limit/attempt value, final size never exceeding the hard limit
  on success, unique categories in contributions, an undefined
  `HarnessContextCategory` value on any contribution rejected defensively even
  though the only public construction path already rejects it, and the sum of
  category contribution sizes (checked against `int` overflow) always
  equalling the final size on success. `HarnessContextCategoryContribution`
  uses a private constructor and an internal `Create` factory that rejects an
  undefined `HarnessContextCategory` value, a negative size, or a non-positive
  entry count, so no invalid contribution can be constructed through its own
  public surface.
- **Deterministic mapping, never string parsing.**
  `HarnessContextDiagnosticsFactory.Create` maps the internal
  `HarnessContextAssemblyResult` to the public snapshot entirely through
  switch expressions (`ToPublicOutcome`, `ToPublicStage`, `ToPublicCategory`)
  that throw `ArgumentOutOfRangeException` on any unrecognized internal
  value — never by parsing an exception or evidence string. Per-category
  contributions are computed by summing the exact same
  `IHarnessContextSizeEstimator` instance that governed the originating
  policy decision over the final entries; a negative estimator result is
  rejected with `InvalidOperationException` and per-category accumulation
  uses `checked` arithmetic, so the contribution total always agrees with
  the reported final size and overflow is never silently truncated.
- **Explicit measurement unit.** `IHarnessContextSizeEstimator.MeasurementUnit`
  is a required property. `HarnessUtf8ContextSizeEstimator` and the
  text-only estimator report `Utf8Bytes`; the fixed/constant test estimators
  report `HostDefinedUnits`; `EstimatedTokens` is reserved, unused by any
  estimator in this codebase today, for a future tokenizer-backed estimator.
  Every size, threshold, and limit on a diagnostics instance or a started
  event is reported in whatever unit the governing estimator declared —
  never assumed to be a token count.
- **Exactly-once emission (for assemblies that reach the assembler).** A
  `HarnessContextCompactionStartedEvent` is emitted only after message
  adaptation, snapshot integration, and assembler construction have all
  succeeded — immediately before `AssembleAsync`. A classifier or
  snapshot-construction exception propagates directly without emitting any
  event. For attempts that do emit a Started event, exactly one terminal event
  follows (Completed or Terminated, never both); a Composed event follows a
  Completed event only on confirmed success after revalidation. Exceptional
  failures during assembly (cancellation, binding invalidation, reducer
  exception) propagate without masquerading as Completed or Terminated.
  No event is reported when the progress accessor is `null`.
- **No new ambient singleton.** The chat client and the compaction composer's
  request record each accept a required nullable
  `IProgressReporterAccessor?` at their existing construction/request seam,
  threaded from the existing `HarnessProviderCompositionRequest.ProgressAccessor`
  — no new ambient singleton, and no optional parameter anywhere in this
  leaf's public or internal signatures.
- **Correlation.** Every event carries `WorkflowId`, `AgentId`,
  `ParentAgentId` (via `IProgressReporterContext`), `Depth`, and
  `SequenceNumber` read directly from the current `IProgressReporter`,
  matching the existing `HarnessArtifactOffloadDecisionEvent`/
  `HarnessArtifactRehydrationDecisionEvent` pattern established in G4. A
  dedicated root→child→grandchild nested-reporter test proves this directly
  for the context events too: `AgentId`/`ParentAgentId`/`Depth` trace the
  exact three-level tree (`null`/0, root-agent/1, child-agent/2) and every
  Started/Completed/Composed (and, separately, Started/Terminated for an
  irreducible attempt) event shares one strictly increasing `SequenceNumber`
  across all three reporter instances rather than each restarting its own
  counter.
- **Binding-revalidation ordering.** A dedicated test pins the intentional
  split between Completed and Composed: when the trusted execution binding is
  invalidated in the window between a successful assembly decision and the
  post-assembly `EnsureCurrent` revalidation, the already-emitted Started and
  Completed events remain observed exactly as reported, `HarnessContextComposedEvent`
  is never emitted (revalidation never passed, so the context was never
  "ready for dispatch"), `HarnessContextCompactionTerminatedEvent` is also
  never emitted (a binding-invalidation failure is not itself a structured
  `Irreducible`/`ConcurrentMutationLimit` outcome), and `InvalidOperationException`
  propagates directly to the caller.
- **Attribution across offload/rehydration.** `HarnessContextAttribution`
  (public record) carries `Operation`, `InputUtf8Bytes`, and
  `OutputUtf8Bytes` (nullable), always measured in UTF-8 bytes regardless of
  direction. `OutputUtf8Bytes` is the artifact-derived output only — the
  reference identity's byte length for `Offloaded`/`ExistingReference`, the
  resolved body's byte length for a successful `Resolved` rehydration — and
  is `null` for `Failed`/`RecoveryRequired` offloads and every non-`Resolved`
  rehydration outcome, even though the caller may emit a separate bounded
  error string for those outcomes. `HarnessArtifactDiagnostics.ForOffload`/
  `.ForRehydration` build a matching attribution automatically from the same
  inputs already supplied, so the identical attribution value rides along on
  the exact same snapshot already attached to the internal offload
  outcome/rehydration result and its corresponding progress event — no second,
  independently computed value exists anywhere.
- **Multibyte correctness.** Dedicated tests inject Japanese (3 bytes/char)
  and mixed 1/2/4-byte-per-code-point UTF-8 content and assert every
  attribution/diagnostics byte count matches `Encoding.UTF8.GetByteCount`
  exactly — never a UTF-16 char or code-unit count.
- **Never present anywhere in the public surface:** raw message text,
  artifact bodies, workspace paths, owner identities, tool arguments/results,
  exception text, or classifier output text. Confirmed by
  `HarnessContextObservabilityTests.Events_NeverContainRawMessageText`, a
  reflection-based test that recursively walks every public string property
  (including nested `HarnessContextDiagnostics`/`HarnessContextCategoryContribution`
  collections) and each record's default `ToString()` across every emitted
  event for `WithinLimit`, `Reduced`, and `PreservationFallback` scenarios
  seeded with distinctive marker text, asserting the marker never appears.
- **Diagnostics/progress identity.** The exact same `HarnessContextDiagnostics`
  instance produced by `HarnessContextDiagnosticsFactory.Create` is carried by
  the `HarnessBoundedMessageAssembly` dispatch result, the Completed (or
  Terminated) event, and — on success — the Composed event; never a second,
  independently rebuilt snapshot.

## Public API disposition

The only public API surface introduced by this leaf is:

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
`HarnessCompactionCompositionRequest`, `HarnessHybridContextPolicy`, and every
other compaction mechanism/request/result/status type remain `internal`,
unchanged in visibility from prior gates. `HarnessCapability.Compaction`'s
resolver definition is data (field values on an existing internal
definition), not new public API.

`IAgentRunDiagnostics`, `IDiagnosticsSink`, and `IAgentMetrics` are
deliberately **not** expanded by this leaf, matching the same G4 precedent:
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
| Diagnostics status | `Available` (this leaf) |
| Delivery phase | `G5` |

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
stays off by default within the complete-bundle lane; this leaf added no new
default-bundle test since that behavior required no resolver changes.

Trust boundary is `ExternalContent` rather than `None` because hybrid-compacted
context is, ultimately, host-classified external/conversational content
flowing through the exact same message-classification path every other
context entry already flows through — no new classification behavior is
introduced by compaction itself, so recording `None` would understate the
actual trust surface. AOT status is `Unverified` rather than `Verified`
because no NativeAOT application in this codebase directly executes hybrid
composition end-to-end; existing hosted Harness AOT coverage only compiles
the composition graph (proving the types are trim/AOT-safe to construct),
never runs a hybrid-enabled agent under NativeAOT. `Compatible` was
considered and rejected in favor of the more conservative `Unverified`,
since no direct construction-and-execution test exists yet under NativeAOT
for this specific capability — `Compatible` would overstate current evidence.

## Accepted limitations (cumulative)

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
- **No completed-run diagnostics aggregation.** As recorded above, the
  structured compaction diagnostics are inspectable per-decision (via the
  attached result or the corresponding progress event) but are not yet
  aggregated into `IAgentRunDiagnostics`, `IDiagnosticsSink`, or
  `IAgentMetrics`. Deferred to later hardening scope, consistent with the
  identical G4 offload/rehydration limitation.
- **No direct NativeAOT execution of the hybrid profile.** Carried forward
  as the reason `AotStatus` is `Unverified` rather than `Verified`; existing
  hosted Harness AOT coverage compiles the graph but does not execute a
  hybrid-enabled agent under NativeAOT.
- **Public runtime configuration remains deferred.** No public API exists
  for selecting, tuning, or composing a hybrid profile; every policy input
  remains an internal construction-time value, per this leaf's explicit
  scope boundary.
- **Retention/deletion inherited from ADR-0006.** This leaf introduces no
  new retention or deletion behavior for workspace-backed artifacts
  referenced from compacted conversation history; ADR-0006's accepted
  no-delete limitation continues to apply unchanged.

## Local validation

```powershell
$env:NUGET_PACKAGES='G:\dev\caches\nuget\packages'
dotnet build src\NexusLabs.Foundry.MicrosoftAgentFramework\NexusLabs.Foundry.MicrosoftAgentFramework.csproj -c Debug
dotnet build src\NexusLabs.Foundry.MicrosoftAgentFramework.Tests\NexusLabs.Foundry.MicrosoftAgentFramework.Tests.csproj -c Debug
dotnet test src\NexusLabs.Foundry.MicrosoftAgentFramework.Tests\NexusLabs.Foundry.MicrosoftAgentFramework.Tests.csproj -c Debug --filter "FullyQualifiedName~HarnessContextObservabilityTests|FullyQualifiedName~HarnessContextDiagnosticsValidationTests"
dotnet test src\NexusLabs.Foundry.MicrosoftAgentFramework.Tests\NexusLabs.Foundry.MicrosoftAgentFramework.Tests.csproj -c Debug --filter "FullyQualifiedName~Harness"
dotnet test src\NexusLabs.Foundry.MicrosoftAgentFramework.Tests\NexusLabs.Foundry.MicrosoftAgentFramework.Tests.csproj -c Debug
dotnet build src\NexusLabs.Foundry.slnx -c Debug
```

Results (final measurement against the complete cumulative leaf, including
this direct-review pass):

- New/modified test classes in isolation: **26 passed, 0 failed.**
  (13 observability tests + 13 diagnostics-validation tests)
- Full Harness filter (`FullyQualifiedName~Harness`): **600 passed, 0 failed**
  (+31 over the #101 baseline of 569; no regressions).
- Full `NexusLabs.Foundry.MicrosoftAgentFramework.Tests` project: **2,169
  passed, 0 failed** (+31 over the #101 baseline of 2,138; no regressions).
- Full `src` solution build (`dotnet build src\NexusLabs.Foundry.slnx -c Debug`):
  succeeded, 0 errors, 4 pre-existing `CS0162` unreachable-code warnings in
  unrelated example apps (`DagRoutingApp.Agents`, `GeneratorCoexistenceApp`,
  `DevUIApp` twice) not touched by any change in this gate.

## Validation status and next permitted gates

- **Local:** complete — targeted new-test filters, full Harness filter, full
  project test suite, and full solution build all pass with zero regressions.
- **Review:** pending independent review.
- **Hosted CI:** pending PR — hosted build/test/package, NativeAOT, Harness
  NativeAOT, and documentation checks have not yet run.
- **Next permitted gates:** G6 (background agents, loop evaluation) and G7
  (test/AOT hardening, including the completed-run diagnostics aggregation
  this leaf explicitly deferred) per the dependency graph in `tasks.md`; G5
  itself is now cumulatively complete pending review and hosted CI.
