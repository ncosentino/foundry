# Gate G4 Decision — Workspace Bridge, Artifact Offload, and Rehydration Observability

## Decision

**PASS FOR THE CUMULATIVE G4 WORKSPACE-AUTHORITY SLICE, INCLUDING THIS LEAF'S
OFFLOAD/REHYDRATION OBSERVABILITY WORK — PENDING HOSTED CI CONFIRMATION ON
PR #96.**

Gate G4 delivers, and this document approves as a single cumulative record,
the workspace bridge and eager tool-result offload/rehydration mechanism
together with this leaf's privacy-safe structured observability over both
decision directions:

1. **Workspace-backed `AgentFileStore` bridge** — an internal adapter from one
   authorized `IWorkspace` to MAF's `AgentFileStore` surface, exposing only
   the operation subset proven feasible by prior evidence and failing closed
   on everything else.
2. **Artifact references and rehydration** — content-addressed
   `artifact://sha256/...` references and an explicit, binding/digest/budget
   validated rehydration mechanism that never injects unresolved content.
3. **Eager tool-result offload** — one shared, caller-agnostic
   inline/offload decision transform used identically by the iterative loop
   and the selected-provider composition, with content-addressed fail-closed
   writes and an explicit partial-commit recovery outcome.
4. **Offload/rehydration observability (this leaf)** — a privacy-safe
   structured diagnostics snapshot and two public progress events reporting
   every offload decision (including `Inline`) and every explicit rehydration
   decision, wired through the existing progress-reporter accessor seam with
   no new ambient singleton and no optional parameters.

`IWorkspace` remains the sole authoritative store for bulk artifact bytes
across all four items. The `AgentFileStore` bridge is confirmed, by
construction and by this leaf's tests, to be a partial adapter over that same
authorized workspace — never a second authority and never the path either the
offload transform or the rehydration mechanism uses to reach workspace bytes.
Conversation compaction policy remains explicitly out of scope for this gate
and for ADR-0006.

No public Harness composition/configuration API, complete session bundle, or
public runtime configuration is approved by this gate. The only public API
surface introduced across G4 is the diagnostics/progress observability
contract from this leaf: four enums, one diagnostics record, and two
`IProgressEvent` records. Every offload/rehydration mechanism, request,
result, outcome, and status type remains `internal`.

## Evidence identity

Cumulative history on top of the G3 foundation gate (`gate-g3.md`):

| Item | Commit / branch | PR | New tests (measured) | Cumulative Harness tests | Cumulative project tests |
|---|---|---|---|---|---|
| G3 final (baseline) | `01adeea5f9cdde82390ac45368156d5efd075edc` | [#90](https://github.com/ncosentino/foundry/pull/90) | — | 170 | 1,739 |
| Offload lifecycle evidence (docs only) | `25d6e581` | [#92](https://github.com/ncosentino/foundry/pull/92) | 0 | 170 | 1,739 |
| Workspace-backed `AgentFileStore` bridge | `67452df0` | [#93](https://github.com/ncosentino/foundry/pull/93) | 40 | 210 | 1,779 |
| Artifact references and rehydration | `ded50c63` | [#94](https://github.com/ncosentino/foundry/pull/94) | 47 | 257 | 1,826 |
| Eager tool-result offload | `ccc88654` | [#95](https://github.com/ncosentino/foundry/pull/95) | 19 | 276 | 1,845 |
| **Offload/rehydration observability (current leaf)** | `a569c67f` | [#96](https://github.com/ncosentino/foundry/pull/96) | 29 | **305** | **1,874** |

- Current leaf implementation head: `a569c67f` on
  `harness/g4-observability`, based on `ccc88654` (PR #95).
- Per-PR new-test counts above are measured from actual `dotnet test`
  cumulative Harness-filter deltas at each commit (using a disposable
  `git worktree` checkout per commit, discarded afterward), not counted from
  source `[Fact]`/`[Theory]` attribute occurrences, so `[Theory]` cases with
  multiple `[InlineData]` rows are represented accurately.
- The current leaf is published as PR #96. Hosted CI evidence for PRs
  #92-#95 already exists from their own merges; hosted evidence for this
  leaf remains pending and this gate record must be refreshed with the
  resulting run links before the gate is finalized.
- All `dotnet build`/`dotnet test` commands for this leaf were run with
  `$env:NUGET_PACKAGES='G:\dev\caches\nuget\packages'` set.

## Files changed (current leaf — offload/rehydration observability)

New files:
- `src/NexusLabs.Foundry.MicrosoftAgentFramework/Diagnostics/HarnessArtifactOperationCategory.cs`
- `src/NexusLabs.Foundry.MicrosoftAgentFramework/Diagnostics/HarnessArtifactOutcomeCategory.cs`
- `src/NexusLabs.Foundry.MicrosoftAgentFramework/Diagnostics/HarnessArtifactContentCategory.cs`
- `src/NexusLabs.Foundry.MicrosoftAgentFramework/Diagnostics/HarnessArtifactDecisionReason.cs`
- `src/NexusLabs.Foundry.MicrosoftAgentFramework/Diagnostics/HarnessArtifactDiagnostics.cs`
- `src/NexusLabs.Foundry.MicrosoftAgentFramework/Progress/HarnessArtifactOffloadDecisionEvent.cs`
- `src/NexusLabs.Foundry.MicrosoftAgentFramework/Progress/HarnessArtifactRehydrationDecisionEvent.cs`
- `src/NexusLabs.Foundry.MicrosoftAgentFramework/Progress/IProgressReporterContext.cs`
  — narrow internal interface exposing `ParentAgentId` only, added during review to
  fix parent correlation without adding a member to public `IProgressReporter`.
  Implemented by `ProgressReporter`, `ChannelProgressReporter`, and
  `ChannelProgressReporter`'s private nested `ChannelChildReporter`.
- `src/NexusLabs.Foundry.MicrosoftAgentFramework.Tests/Harness/HarnessArtifactObservabilityTests.cs` (29 tests)
- `docs/adr/adr-0006-hybrid-context-and-workspace-authority.md`
- `specs/001-maf-harness-first-class/evidence/gate-g4.md` (this file)

Modified files:
- `src/NexusLabs.Foundry.MicrosoftAgentFramework/Tools/HarnessToolResultOffloadOutcome.cs`
  — added a `Diagnostics` property; every factory now builds a matching
  `HarnessArtifactDiagnostics.ForOffload(...)` snapshot.
- `src/NexusLabs.Foundry.MicrosoftAgentFramework/Tools/HarnessToolResultOffloadRequest.cs`
  — added a required trailing `IProgressReporterAccessor? ProgressAccessor`
  parameter (no optional parameter; every call site updated explicitly).
- `src/NexusLabs.Foundry.MicrosoftAgentFramework/Tools/HarnessToolResultOffloadTransform.cs`
  — split the single `Transform` method into `Transform` → `Decide` +
  `Report`; `Report` emits exactly one `HarnessArtifactOffloadDecisionEvent`
  per completed decision when an accessor with an active scope is present.
- `src/NexusLabs.Foundry.MicrosoftAgentFramework/Progress/ProgressReporter.cs`
  — implements the new internal `IProgressReporterContext`, exposing its
  already-correctly-threaded `_parentAgentId` field explicitly.
- `src/NexusLabs.Foundry.MicrosoftAgentFramework/Progress/ChannelProgressReporter.cs`
  — fixed two accepted-review bugs: (1) the constructor's `parentAgentId`
  parameter was accepted but never stored or used, so every
  `ChannelProgressReporter` reported `ParentAgentId: null` regardless of the
  value the caller supplied; (2) the private nested `ChannelChildReporter`
  always computed `Depth` from the outermost root's depth and exposed no
  `ParentAgentId` at all, so a nested child (a child-of-a-child) reported the
  wrong depth and lost its immediate parent. Both reporters now implement the
  new internal `IProgressReporterContext`.
- `src/NexusLabs.Foundry.MicrosoftAgentFramework/Diagnostics/HarnessArtifactContentCategory.cs`
  — doc-comment corrections: `ToolResult`'s and `RecoverableContextSegment`'s
  summaries no longer claim rehydration always uses `ToolResult`.
- `src/NexusLabs.Foundry.MicrosoftAgentFramework/Harness/Context/HarnessArtifactRehydrationRequest.cs`
  — doc-comment cleanup only (task/gate-ID reference removed).
- `src/NexusLabs.Foundry.MicrosoftAgentFramework/Harness/Context/HarnessArtifactRehydrationResult.cs`
  — added a `Diagnostics` property; `NotResolved`/`Resolved` factories now
  require it; doc-comment cleanup.
- `src/NexusLabs.Foundry.MicrosoftAgentFramework/Harness/Context/HarnessArtifactRehydration.cs`
  — added a required nullable `IProgressReporterAccessor? progressReporterAccessor`
  constructor parameter; split `Rehydrate` into `Rehydrate` → `Decide` +
  `BuildDiagnostics` + `Report`, mirroring the offload transform's pattern; a
  deterministic `switch` expression maps each `HarnessArtifactResolutionStatus`
  to its `(HarnessArtifactOutcomeCategory, HarnessArtifactDecisionReason)`
  pair — never derived from a human-readable evidence string; doc-comment
  cleanup.
- `src/NexusLabs.Foundry.MicrosoftAgentFramework/Harness/Context/HarnessArtifactIdentity.cs`,
  `HarnessArtifactReference.cs`, `HarnessArtifactRecoverableContextSegment.cs`,
  `HarnessArtifactRehydrationRequestSource.cs`, `HarnessArtifactResolution.cs`,
  `HarnessArtifactResolutionStatus.cs`, `HarnessArtifactResolver.cs`
  — doc-comment cleanup only: removed task/gate-ID references (`T020`, `T041`,
  `T050`, `T052`, `T053`, `G4`, `G5`) while preserving the durable behavioral
  intent each comment was describing; no behavior change.
- `src/NexusLabs.Foundry.MicrosoftAgentFramework/Harness/HarnessProviderComposition.cs`
  — the offload call site now passes `request.ProgressAccessor` (from
  `HarnessProviderCompositionRequest.ProgressAccessor`) as the trailing
  argument.
- `src/NexusLabs.Foundry.MicrosoftAgentFramework/Iterative/IterativeAgentLoop.cs`
  — the offload call site now passes its existing `_progressReporterAccessor`
  field as the trailing argument.
- `src/NexusLabs.Foundry.MicrosoftAgentFramework.Tests/Harness/HarnessEagerOffloadTests.cs`
  — fixed 3 direct `HarnessToolResultOffloadOutcome`/`Request` constructions
  and the `CreateRequest` test helper (optional trailing `progressAccessor`
  parameter — acceptable for a test-only helper) for the new constructor
  shape.
- `src/NexusLabs.Foundry.MicrosoftAgentFramework.Tests/Harness/HarnessWorkspaceCancellationTests.cs`
  — fixed all 7 direct `HarnessToolResultOffloadRequest` constructions with
  an explicit trailing `ProgressAccessor: null`.
- `src/NexusLabs.Foundry.MicrosoftAgentFramework.Tests/Harness/HarnessArtifactTestFixture.cs`
  — added progress-accessor-aware `Create` overloads (via overloading, not
  optional parameters) threading an accessor into `HarnessArtifactRehydration`'s
  new constructor parameter.
- `src/NexusLabs.Foundry.MicrosoftAgentFramework.Tests/ProgressEventCoverageTests.cs`
  — added both new event type names to the hard-coded catalogue of every
  known `IProgressEvent` implementation.

Every existing call site of `HarnessToolResultOffloadRequest` outside this
leaf's own new test file passes an explicit `ProgressAccessor` value (either
the real accessor at a production call site, or `null`/a fixture-supplied
value in tests) — no call site was left uncompiled or defaulted.

## One-top-level-type-per-file disposition

The issue names a plural `Diagnostics/HarnessArtifactDiagnostics.cs` file for
"privacy-safe artifact diagnostics." The repository convention is one
top-level C# type per file. This leaf honors that convention by splitting the
diagnostics contract across five separate files under `Diagnostics/`:
`HarnessArtifactDiagnostics.cs` (the diagnostics record itself) plus four
single-enum files (`HarnessArtifactOperationCategory.cs`,
`HarnessArtifactOutcomeCategory.cs`, `HarnessArtifactContentCategory.cs`,
`HarnessArtifactDecisionReason.cs`) that the record depends on. The two
public progress events likewise each occupy their own file under
`Progress/`. No file in this leaf contains more than one top-level type.

## Workspace authority (cumulative)

`IWorkspace` is the only store ever read or written for artifact bytes across
the bridge, the offload transform, and the rehydration mechanism:

- The `AgentFileStore` bridge (`WorkspaceAgentFileStore`) wraps one authorized
  `IWorkspace` per instance and never introduces its own storage.
- `HarnessToolResultOffloadTransform` reads and writes the bound
  `HarnessExecutionBinding.Workspace` directly — never through the bridge.
- `HarnessArtifactResolver` (rehydration's sole content-access path) likewise
  reads the bound `IWorkspace` directly — never through the bridge.

No component in G4 treats the `AgentFileStore` bridge as a second source of
truth for artifact bytes; this is confirmed both by construction (the bridge
type is never referenced from the offload or rehydration code paths) and is
the exact framing ADR-0006 makes durable.

## Operation mapping / unsupported semantics (`AgentFileStore` bridge, cumulative from PR #93)

| `AgentFileStore` operation | Bridge behavior |
|---|---|
| `WriteAsync` | Supported — ordinary write via `TryWriteFile` only; never claims compare-exchange/CAS semantics. |
| `ReadAsync` | Supported, conditional — returns content on success; maps a classified missing-file failure to `null`; every other failure propagates unchanged. |
| `DeleteAsync` | **Permanently unsupported** — `IWorkspace` has no delete operation. |
| `ListChildrenAsync` | Supported with limits — derived from a full scan of `IWorkspace.GetFilePaths()` filtered in memory; the returned-entry cap bounds the result, not the O(total workspace files) scan cost. |
| `FileExistsAsync` | Supported — canonicalizes and fails closed on an invalid path. |
| `SearchAsync` | **Permanently unsupported** — `IWorkspace` cannot inspect size or bound a read before allocating full content. |
| `CreateDirectoryAsync` | A validated no-op — `IWorkspace` has no directory-as-object concept; empty directories created this way are not observable through `ListChildrenAsync`/`SearchAsync`. |

Cancellation is inherently limited: `IWorkspace` is synchronous, so the bridge
cannot interrupt an already-running workspace call and never fabricates
interruption with `Task.Run`.

## Offload decision status matrix (this leaf's observability contract)

| Outcome | Reason | Reference ID present | Emitted event field notes |
|---|---|---|---|
| `Inline` | `BelowThreshold` | No | Observed bytes ≤ configured threshold. |
| `Inline` | `RecoverableSegmentBypass` | No | A rehydrated recoverable context segment always bypasses the threshold. |
| `Offloaded` | `ThresholdExceeded` | Yes | Fresh content-addressed write; observed bytes > threshold. |
| `ExistingReference` | `ExistingContentMatch` | Yes | Matching digest already present; no write performed. |
| `Failed` | `NoAuthorizedWorkspace` | No | No execution binding, no bound workspace, or no context accessor. |
| `Failed` | `WorkspaceReadFailed` | No | Reading existing content at the content-addressed path failed. |
| `Failed` | `ContentAddressMismatch` | No | Existing content's digest did not match the expected digest. |
| `Failed` | `WorkspaceWriteFailed` | No | Writing fresh content to the workspace failed. |
| `RecoveryRequired` | `CanceledAfterWrite` | No | Write succeeded; the token was canceled before the reference could be committed. |
| `RecoveryRequired` | `CheckpointFailed` | No | Write succeeded; the configured post-write checkpoint threw before the reference could be committed. |

## Rehydration decision status matrix (this leaf's observability contract)

| Outcome (`HarnessArtifactResolutionStatus`) | Reason | Observed bytes | Content category |
|---|---|---|---|
| `Resolved` | `DigestVerified` | Always present. | `RecoverableContextSegment` |
| `Stale` | `DigestMismatch` | Present (the mismatched content was read). | `RecoverableContextSegment` |
| `Missing` | `Missing` | `null` — the workspace path was never read. | `RecoverableContextSegment` |
| `Unauthorized` | `OwnerMismatch` | `null` — the workspace was never read. | `RecoverableContextSegment` |
| `OverBudget` | `BudgetExceeded` | Present (content was read to measure it against the budget). | `RecoverableContextSegment` |

Every rehydration outcome's diagnostics `Content` is `RecoverableContextSegment`
— corrected during review from an earlier draft that hard-coded `ToolResult`
for all decisions; `ToolResult` describes the *offload* seam's content only.

The mapping from `HarnessArtifactResolutionStatus` to
`(HarnessArtifactOutcomeCategory, HarnessArtifactDecisionReason)` is a
deterministic `switch` expression in `HarnessArtifactRehydration.BuildDiagnostics`
— never derived by parsing `HarnessArtifactResolution`'s human-readable
evidence text.

## Trust, isolation, and partial commits (cumulative)

- Every offload and rehydration operation revalidates the trusted
  `HarnessExecutionBinding` against the current ambient execution context
  before touching the workspace (`EnsureCurrent`), consistent with ADR-0005.
- Rehydration additionally compares the reference's recorded owner
  (user/orchestration/session) against the current binding before ever
  reading the workspace; a mismatch returns `Unauthorized` without a read.
- The offload transform's partial-commit window (write succeeded, reference
  not yet committed) is never silently reclassified as `Failed`; it is always
  `RecoveryRequired` with bounded path/digest retry metadata, so a retry
  against identical content resolves via `ExistingReference` without
  re-writing.
- `HarnessWorkspaceIsolationTests.cs` (PR #93) and
  `HarnessWorkspaceCancellationTests.cs` (PR #95) remain the primary
  cumulative coverage for isolation and partial-commit behavior; this leaf
  adds no new isolation semantics, only observability over the existing ones.

## Observability and privacy (this leaf)

- **Contract.** `HarnessArtifactDiagnostics` carries: operation category
  (`Offload`/`Rehydration`), outcome category (10 values across two disjoint
  families), content category (`ToolResult`/`RecoverableContextSegment`),
  reason category (15 values, 10 offload + 5 rehydration), observed UTF-8
  byte size (nullable for rehydration outcomes that never read content),
  configured threshold/budget (always known — a caller-supplied input, never
  an observation), and a bounded `artifact://sha256/...` reference identity
  when one is available. Every rehydration diagnostics snapshot's `Content`
  is always `RecoverableContextSegment` — never `ToolResult` — because a
  rehydrated body is, by definition, a recovered context segment, not a fresh
  tool result; `ToolResult` is used only by offload diagnostics.
- **Factory visibility and canonical reference-ID enforcement.**
  `ForOffload`/`ForRehydration` are `internal`, not public arbitrary-input
  factories — a caller cannot construct an inconsistent snapshot from outside
  this assembly. Both factories additionally require any non-null
  `referenceId` to be exactly the canonical `artifact://sha256/` prefix
  followed by 64 lowercase hex characters (`HarnessArtifactIdentity`'s own
  digest shape), rejecting malformed, wrong-prefix, or uppercase-hex input via
  `ArgumentException`. `ForOffload`'s `Offloaded`/`ExistingReference`
  outcomes require a non-null canonical reference; every other offload
  outcome (`Inline`/`Failed`/`RecoveryRequired`) requires `null`.
  `ForRehydration` requires a non-null canonical reference for every
  rehydration outcome (the reference being resolved is always known upfront).
  Covered by dedicated invalid/mismatch tests in
  `HarnessArtifactObservabilityTests`.
- **Family validation.** `ForOffload`/`ForRehydration` each reject an
  outcome/reason from the wrong family via `ArgumentOutOfRangeException`, so
  a snapshot can never mix the two decisions' state machines.
- **Exactly-once emission.** `HarnessToolResultOffloadTransform.Report` and
  `HarnessArtifactRehydration.Report` each emit exactly one event per
  completed decision, including `Inline`, and never emit when the underlying
  call throws before reaching a decision (pre-canceled token, stale binding).
- **No new ambient singleton.** Both mechanisms accept a required nullable
  `IProgressReporterAccessor?` at their existing construction/request seam
  (`HarnessToolResultOffloadRequest.ProgressAccessor`,
  `HarnessArtifactRehydration`'s constructor). When the accessor is `null`,
  or non-`null` with no active scope, ordinary behavior is fully preserved:
  nothing is reported and nothing throws.
- **Correlation.** Both progress events carry `WorkflowId`, `AgentId`,
  `Depth`, `ParentAgentId`, and `SequenceNumber` read directly from the
  current `IProgressReporter` (matching the existing `HarnessApproval*`
  progress-event pattern). `ParentAgentId` is populated from a new narrow
  internal `IProgressReporterContext.ParentAgentId` property — implemented by
  `ProgressReporter`, `ChannelProgressReporter`, and `ChannelProgressReporter`'s
  private nested `ChannelChildReporter` — rather than being hard-coded to
  `null`. A reporter that does not implement `IProgressReporterContext` (an
  arbitrary custom `IProgressReporter` implementation) falls back to `null`
  safely; this never throws. `IProgressReporter` itself gains no new public
  member. Child and nested-child reporter coverage
  (`HarnessArtifactObservabilityTests`) proves `AgentId`, `ParentAgentId`,
  `Depth`, and a shared, monotonically increasing global `SequenceNumber`
  across a root reporter, its child, and a nested child-of-a-child, for both
  the offload and rehydration seams.
- **Tool name / call ID.** Deliberately omitted from both progress events.
  Neither is needed for FR-031 correlation once `SequenceNumber` and
  `WorkflowId` are present, and omitting them removes an entire class of
  potential unbounded-identifier leakage; the existing bounded
  `HarnessToolResultOffloadOutcome.Evidence` string (already truncated to a
  fixed maximum identifier length) remains the sole place a tool name/call ID
  fragment can appear, and it is `internal`, not part of this leaf's public
  progress/diagnostics surface.
- **Never present anywhere in the public surface:** artifact body, serialized
  raw result, workspace path, owner user/orchestration/session identity, or a
  raw exception message. Confirmed by `HarnessArtifactObservabilityTests`'
  reflection-based assertions over every public string property (recursing
  into nested `Diagnostics`) and each record's default `ToString()`,
  including a dedicated case that injects a unique `IOException` message via
  `FakeWorkspace.WriteFileOverride` and asserts it never leaks. The privacy
  test that exercises only the `Offloaded` outcome is named
  `OffloadDiagnostics_OffloadedOutcome_NeverContainRawContent_WorkspacePath_OwnerIds_OrExceptionMessage`
  — renamed during review from a prior `...AcrossEveryOutcome...` name that
  overstated its own coverage (it exercises exactly one outcome, not every
  outcome).
- **Diagnostics/progress identity.** The exact same
  `HarnessArtifactDiagnostics` instance attached to an internal offload
  outcome or rehydration result is the instance carried by its corresponding
  progress event — asserted directly by reference/value equality in tests,
  not merely by structural equality.

## Public API disposition

The only public API surface introduced by this leaf (and by G4 overall) is:

| Type | Kind | Namespace |
|---|---|---|
| `HarnessArtifactOperationCategory` | enum | `NexusLabs.Foundry.MicrosoftAgentFramework.Diagnostics` |
| `HarnessArtifactOutcomeCategory` | enum | `NexusLabs.Foundry.MicrosoftAgentFramework.Diagnostics` |
| `HarnessArtifactContentCategory` | enum | `NexusLabs.Foundry.MicrosoftAgentFramework.Diagnostics` |
| `HarnessArtifactDecisionReason` | enum | `NexusLabs.Foundry.MicrosoftAgentFramework.Diagnostics` |
| `HarnessArtifactDiagnostics` | sealed record | `NexusLabs.Foundry.MicrosoftAgentFramework.Diagnostics` |
| `HarnessArtifactOffloadDecisionEvent` | sealed record : `IProgressEvent` | `NexusLabs.Foundry.MicrosoftAgentFramework.Progress` |
| `HarnessArtifactRehydrationDecisionEvent` | sealed record : `IProgressEvent` | `NexusLabs.Foundry.MicrosoftAgentFramework.Progress` |

Every public member of every type above carries complete XML documentation
(summary, and `<param>`/`<exception>` where applicable). None of the seven
types introduces an optional parameter or a default interface member. No
public runtime configuration type and no neutral cross-provider Foundry
abstraction is introduced — every configuration input (thresholds, budgets,
policies) remains on the existing `internal` request/policy types. All other
G4 mechanism, request, result, outcome, and status types
(`HarnessToolResultOffloadTransform`, `HarnessToolResultOffloadOutcome`,
`HarnessToolResultOffloadRequest`, `HarnessArtifactRehydration`,
`HarnessArtifactRehydrationRequest`, `HarnessArtifactRehydrationResult`,
`HarnessArtifactResolver`, `HarnessArtifactResolution`,
`WorkspaceAgentFileStore`, and their supporting status/policy types) remain
`internal`, unchanged from PRs #93-#95.

`HarnessArtifactDiagnostics.ForOffload` and `.ForRehydration` are `internal`,
not public arbitrary-input factories — the public record's data can only ever
be produced by this assembly's own offload/rehydration code paths, per the
canonical reference-ID and outcome/reason family validation described above.
The new `IProgressReporterContext` (`Progress/IProgressReporterContext.cs`)
is likewise `internal`; it does not add a member to the public
`IProgressReporter` interface, and no other public runtime configuration is
introduced by the parent-correlation fix.

`IAgentRunDiagnostics`, `IDiagnosticsSink`, and `IAgentMetrics` are
deliberately **not** expanded by this leaf. The `HarnessArtifactDiagnostics`
snapshot attached to an offload outcome or rehydration result is available
for a test or a caller holding that outcome/result directly, and identically
through the corresponding progress event; aggregating either into a
completed-run diagnostics summary, a sink, or a metrics surface is out of
scope here and is recorded below as G7 hardening scope, to avoid a duplicate
writer for the same underlying decision.

## Accepted limitations (cumulative)

- **Full-body read before budget check.** `IWorkspace` exposes no size
  metadata, so `HarnessArtifactResolver.Resolve` must read a candidate
  artifact's full content before it can compare its size against the
  caller's budget; the budget check cannot short-circuit the read.
- **`ListChildrenAsync` is O(total workspace files).** The bridge derives its
  result from a full scan of `IWorkspace.GetFilePaths()`; the
  `maximumListEntries` cap bounds only the returned result, not the
  underlying enumeration cost.
- **Synchronous cancellation limits.** `IWorkspace` is synchronous, so the
  bridge, the offload transform, and the rehydration resolver can only check
  a `CancellationToken` between synchronous steps — never interrupt one
  already in progress — and never fabricate interruption with `Task.Run`.
- **No delete or retention policy.** Neither `IWorkspace` nor any G4 mechanism
  exposes a delete operation; a workspace accumulates every distinct
  offloaded digest indefinitely under G4 alone.
- **`AIFunctionFactory` type erasure constraint.** Generated tool wrappers
  compose through Foundry's existing generated-function provider and MEAI's
  `AIFunctionFactory`; this constrains how a raw tool result reaches the
  offload transform and is an existing, unchanged constraint from prior
  gates, not one introduced by this leaf.
- **No run-diagnostics aggregation yet.** As recorded above, the structured
  offload/rehydration diagnostics are inspectable per-decision (via the
  attached outcome/result or the corresponding progress event) but are not
  yet aggregated into `IAgentRunDiagnostics`, `IDiagnosticsSink`, or
  `IAgentMetrics`. This aggregation is deferred to G7 hardening scope.
- **No direct Harness AOT execution proof** for this leaf's observability
  types specifically (carried forward from G2/G3/prior G4 items; still open).

## Local validation

```powershell
$env:NUGET_PACKAGES='G:\dev\caches\nuget\packages'
dotnet build src\NexusLabs.Foundry.MicrosoftAgentFramework\NexusLabs.Foundry.MicrosoftAgentFramework.csproj
dotnet build src\NexusLabs.Foundry.MicrosoftAgentFramework.Tests\NexusLabs.Foundry.MicrosoftAgentFramework.Tests.csproj
dotnet test src\NexusLabs.Foundry.MicrosoftAgentFramework.Tests\NexusLabs.Foundry.MicrosoftAgentFramework.Tests.csproj --filter "FullyQualifiedName~HarnessArtifactObservabilityTests"
dotnet test src\NexusLabs.Foundry.MicrosoftAgentFramework.Tests\NexusLabs.Foundry.MicrosoftAgentFramework.Tests.csproj --filter "FullyQualifiedName~Harness"
dotnet test src\NexusLabs.Foundry.MicrosoftAgentFramework.Tests\NexusLabs.Foundry.MicrosoftAgentFramework.Tests.csproj
dotnet build src\NexusLabs.Foundry.slnx --no-restore
```

Results:
- `HarnessArtifactObservabilityTests` in isolation: **29 passed, 0 failed.**
- Full Harness filter (`FullyQualifiedName~Harness`): **305 passed, 0
  failed** (276 pre-existing PR #92-#95 tests + 29 new; no regressions).
- Full `NexusLabs.Foundry.MicrosoftAgentFramework.Tests` project: **1,874
  passed, 0 failed** (1,845 pre-existing + 29 new).
- Full `src` solution build (`dotnet build src\NexusLabs.Foundry.slnx --no-restore`):
  succeeded, 0 errors, 3 pre-existing `CS0162` unreachable-code warnings in
  unrelated example apps (`DagRoutingApp.Agents`, `DevUIApp`) not touched by
  this leaf.

Per-PR new-test and cumulative counts in the "Evidence identity" table above
were independently re-measured for this gate document via disposable `git
worktree` checkouts of `25d6e581`, `67452df0`, `ded50c63`, and `ccc88654`
(each removed immediately after measurement; the primary working tree was
never used for those historical measurements).

## Accepted review fixes applied to this leaf

A self-review pass against the first draft of this leaf accepted and applied
the following fixes before this gate document was finalized:

1. **Parent correlation.** Fixed the hard-coded `ParentAgentId: null` at both
   artifact-event writers by adding a narrow internal
   `IProgressReporterContext` (one file, one type), implemented by
   `ProgressReporter`, `ChannelProgressReporter`, and
   `ChannelProgressReporter`'s private nested `ChannelChildReporter` — with no
   new member on public `IProgressReporter`. Along the way this also fixed
   two pre-existing `ChannelProgressReporter` bugs: its constructor's
   `parentAgentId` parameter was accepted but silently discarded, and its
   nested child reporter computed `Depth` from the outermost root instead of
   its own immediate parent, so a nested child-of-a-child reported the wrong
   depth and lost its immediate parent identity. Child and nested-child
   coverage was added proving `AgentId`, `ParentAgentId`, `Depth`, and a
   shared global `SequenceNumber` for both the offload and rehydration seams.
2. **Internal, validated diagnostics factories.** `ForOffload`/`ForRehydration`
   are `internal`, not public arbitrary-input factories, and both now enforce
   a canonical `artifact://sha256/{64-lowercase-hex}` reference-ID shape.
   `Offloaded`/`ExistingReference` require a non-null canonical reference;
   `Inline`/`Failed`/`RecoveryRequired` require `null`; every rehydration
   outcome requires a non-null canonical reference. Invalid/mismatch cases
   (null where required, malformed shape, wrong prefix, uppercase hex, a
   reference on an outcome that must not carry one) are covered by dedicated
   tests.
3. **Rehydration content category.** Fixed to `RecoverableContextSegment` for
   every rehydration outcome — an earlier draft incorrectly hard-coded
   `ToolResult` (the offload seam's category) for rehydration diagnostics
   too. Tests, this document, and ADR-0006 were all updated to reflect the
   correct category.
4. **Test naming accuracy.** Renamed
   `OffloadDiagnostics_AcrossEveryOutcome_...` to
   `OffloadDiagnostics_OffloadedOutcome_...` because the test body only ever
   exercises the `Offloaded` outcome, not every outcome.
5. **Gate wording.** This document's Decision heading states that the PASS is
   pending hosted CI confirmation on PR #96 (see "Review disposition" below).
6. **Comment cleanup.** Removed task/gate-ID references (`T020`, `T041`,
   `T050`, `T052`, `T053`, `G4`, `G5`) and implementation-chronology framing
   from XML docs/comments in every `Harness/Context` artifact class and
   `Tools` offload class touched or directly documented by this leaf, while
   preserving the durable behavioral intent each comment described. No
   production behavior changed as part of this cleanup.
7. **Preserved constraints.** No-accessor/no-active-scope behavior remains a
   no-op that never throws; no duplicate progress writer was introduced for
   the same decision; no public runtime configuration type was introduced;
   `IAgentRunDiagnostics`, `IDiagnosticsSink`, and `IAgentMetrics` were not
   touched; and every public XML doc comment remains complete.

## Review disposition

- Author self-review: complete (this document).
- Independent AI correctness and architecture review: complete; both blocking
  findings were adopted and revalidated locally.
- GitHub maintainer review: pending on PR #96.
- Hosted CI (build/test/package, standard NativeAOT, Harness NativeAOT,
  documentation): **pending PR #96.** PRs #92-#95's own hosted
  runs already passed at their respective merges; this leaf's hosted
  evidence will be produced when it is committed and opened, and this gate
  record should be refreshed with the resulting run links at that time.

## Next permitted work

The workspace bridge, artifact references, rehydration, eager offload, and
offload/rehydration observability (T041-T056) are now complete. Proceed with:

- **Gate G5** (`gate-g5.md`, T057-T071): experimental hybrid context and
  compaction policy, explicitly gated as experimental/opt-in — this is the
  first point at which a compaction decision (deliberately deferred by
  ADR-0006 and this gate) may be made.
- **Gate G6** (`gate-g6.md`, T072-T080): the optional complete Harness
  bundle, including any first public Harness API surface decision.
- **Gate G7** (`gate-g7.md`, T081-T094): AOT, analyzer, testing, and
  documentation hardening — including the run-diagnostics aggregation for
  offload/rehydration decisions deferred above, and closing the direct
  Harness AOT execution gap accepted above.
