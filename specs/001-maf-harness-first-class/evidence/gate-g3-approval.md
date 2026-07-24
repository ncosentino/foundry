# Gate G3 Decision — Tool-Approval Slice

## Decision

**PASS FOR INTERNAL SELECTED-PROVIDER TOOL-APPROVAL SLICE**

The MAF 1.15 / MEAI 10.6 selected-provider lane can advance one stable
capability slice: the three independent tool-approval capabilities
(`ApprovalResponseBinding`, `ApprovalNotRequiredBypassing`, `ToolAutoApproval`)
plus mandatory host reauthorization for standing ("always approve") tool
approvals. This gate approves only that slice's internal composition, guard
coherence, exactly-once/zero invocation, forgery/mismatch fail-closed
behavior, session serialize/restore of pending approval state, and progress
event evidence.

No public Harness composition/configuration API, loop evaluation, skills, web
search, workspace, compaction, or complete-bundle behavior is approved by this
gate. The four approval progress records are the only public surface introduced.

## Evidence identity

- G2 baseline: `57193a74bbfe2d9b383dacaefb7d9c237f73eea2`
  (`feat: establish Harness composition foundation (#83)`)
- G3 history slice: `45f066d` (`feat: add Harness session continuity slice (#84)`)
- G3 planning slice: `f714991` (`feat: add Harness planning providers slice (#85)`)
- G3 approval branch: `harness/g3-approval` (this work; no commit, push, or PR
  was made)
- Local deterministic validation (this slice):
  - `HarnessApprovalTests` in isolation: 35 passed, 0 failed.
  - Full Harness filter (`FullyQualifiedName~Harness`): 130 passed, 0 failed
    (95 pre-existing G2/G3-history/G3-planning tests + 35 G3 approval tests;
    no regressions).
  - Full `NexusLabs.Foundry.MicrosoftAgentFramework.Tests` project: 1,699
    passed, 0 failed (1,664 pre-existing + 35 new; includes an update to the
    pre-existing `ProgressEventCoverageTests.KnownConcreteEvents_MatchTheRegisteredCatalogue`
    regression lock to acknowledge the four new event types).
- All `dotnet build`/`dotnet test` commands were run with
  `$env:NUGET_PACKAGES='G:\dev\caches\nuget\packages'` set.
- No hosted CI run was performed for this slice; no commit, push, or PR was
  made as part of this work.

## Files changed

New files:
- `src/NexusLabs.Foundry.MicrosoftAgentFramework/Harness/Providers/HarnessApprovalPlugin.cs`
- `src/NexusLabs.Foundry.MicrosoftAgentFramework/Harness/Providers/HarnessApprovalCompositionGuard.cs`
- `src/NexusLabs.Foundry.MicrosoftAgentFramework/Harness/Providers/HarnessApprovalCompositionGuardResult.cs`
- `src/NexusLabs.Foundry.MicrosoftAgentFramework/Harness/Providers/HarnessApprovalCompositionGuardStatus.cs`
- `src/NexusLabs.Foundry.MicrosoftAgentFramework/Harness/Providers/HarnessApprovalHostValidator.cs`
- `src/NexusLabs.Foundry.MicrosoftAgentFramework/Harness/Providers/HarnessApprovalHostValidationContext.cs`
- `src/NexusLabs.Foundry.MicrosoftAgentFramework/Harness/Providers/HarnessApprovalHostValidationReason.cs`
- `src/NexusLabs.Foundry.MicrosoftAgentFramework/Progress/HarnessApprovalProgressEvents.cs`
- `src/NexusLabs.Foundry.MicrosoftAgentFramework.Tests/Harness/HarnessApprovalTests.cs`
- `specs/001-maf-harness-first-class/evidence/gate-g3-approval.md` (this file)

Modified files:
- `src/NexusLabs.Foundry.MicrosoftAgentFramework/Harness/HarnessProviderCompositionRequest.cs`
  (one required nullable `HarnessApprovalPlugin? ApprovalPlugin` argument)
- `src/NexusLabs.Foundry.MicrosoftAgentFramework/Harness/HarnessProviderCompositionStatus.cs`
  (the six independent approval guard statuses plus `ApprovalPluginUnexpected`)
- `src/NexusLabs.Foundry.MicrosoftAgentFramework/Harness/HarnessProviderComposition.cs`
  (approval guard call, builder-pipeline wiring, `ToolApprovalAgent` wrapping,
  `HarnessGuardedAgent` construction argument)
- `src/NexusLabs.Foundry.MicrosoftAgentFramework/Harness/HarnessGuardedAgent.cs`
  (`EnsureApprovalReauthorizedAsync`, approval progress reporting, `GetService`
  blocklist additions)
- `src/NexusLabs.Foundry.MicrosoftAgentFramework.Tests/Harness/HarnessCompositionTestFixture.cs`
  (`CreateApprovalProfile`, `CreateApprovalPlugin`, `CreateRequest` cascades
  accepting `approvalPlugin`/`progressAccessor`)
- `src/NexusLabs.Foundry.MicrosoftAgentFramework.Tests/ProgressEventCoverageTests.cs`
  (catalogue acknowledgement of the four new concrete `IProgressEvent` types)

Every existing call site of `HarnessProviderCompositionRequest` outside this
slice's own tests passes `approvalPlugin: null`, preserving prior G2/G3-history/
G3-planning behavior unchanged.

## Gate evidence

| Criterion | Result | Evidence |
|---|---|---|
| Single composition root preserved | Pass | `HarnessProviderComposition.Compose` remains the only selected-provider composition root. No parallel composer was added; approval wiring is additive branches inside the existing method, gated behind `HarnessApprovalCompositionGuard.Validate`. |
| Narrow, immutable approval plugin | Pass | `HarnessApprovalPlugin` is `internal sealed`, has a private constructor, exposes only four read-only properties (`ResponseBindingEnabled`, `NotRequiredBypassingEnabled`, `ToolApprovalOptions`, `HostValidator`), and is constructed exclusively through `HarnessApprovalPlugin.Create`, which enforces "at least one capability selected" and "ToolAutoApproval requires a host validator, and vice versa" before returning an instance. |
| One required nullable plugin argument, all call sites updated | Pass | `HarnessProviderCompositionRequest.ApprovalPlugin` is a single required (non-optional, no default) nullable parameter. Every pre-existing G2/G3-history/G3-planning call site (composition tests, fixture helpers) supplies `null` explicitly; the compiler enforces this because the parameter has no default. |
| Three capabilities treated independently; no implicit activation | Pass | `HarnessApprovalCompositionGuard.Validate` checks `ApprovalResponseBinding`, `ApprovalNotRequiredBypassing`, and `ToolAutoApproval` against the plugin with three separate, order-independent `if` blocks, each producing its own `Required`/`Unexpected` status. `Compose_NoApprovalCapabilitySelected_ForgedResponseWithGuessedCallIdStillInvokes` demonstrates that composing with `approvalPlugin: null` never implicitly enables `ApprovalResponseBinding`-style forgery protection — MAF's own unconditional `ApprovalRequiredAIFunction` marker detection still surfaces a request, but no Foundry or MAF response-binding protection is active unless explicitly selected. |
| `DisableApprovalResponseBinding`/`DisableApprovalNotRequiredFunctionBypassing` driven from effective profile state | Pass | `HarnessProviderComposition.Compose` sets `ChatClientAgentOptions.DisableApprovalResponseBinding = !responseBindingEnabled` and `DisableApprovalNotRequiredFunctionBypassing = !notRequiredBypassingEnabled` from the resolved capability profile, not from MAF's own defaults. Covered indirectly by every guard test (a capability enabled without the corresponding builder `.Use...` call would leave the MAF default disabled flags active, which the response-binding/forgery tests would fail to observe the intended binding behavior from). |
| `ToolApprovalAgent` added only when `ToolAutoApproval` is explicitly enabled with a coherent plugin | Pass | The `ToolApprovalAgent` wrap only occurs when `toolAutoApprovalEnabled` is true and `approvalPlugin.ToolApprovalOptions` is non-null (guaranteed coherent by `HarnessApprovalCompositionGuard`); `Compose_ToolAutoApprovalEnabledWithoutPlugin_FailsClosedWithoutAgent` and `Compose_ToolAutoApprovalSuppliedWhileDisabled_FailsClosedWithoutAgent` prove the guard rejects both directions of incoherence before any agent is built. |
| `HarnessGuardedAgent` remains the outermost surface; `ToolApprovalAgent` composed inside it, around the raw `ChatClientAgent` | Pass | `HarnessProviderComposition` resolves/configures the chat-client middleware pipeline (`UseApprovalResponseBinding`, `UseApprovalNotRequiredFunctionBypassing`, function invocation, telemetry) first, builds the raw `ChatClientAgent`, optionally wraps it with `ToolApprovalAgent`, and only then constructs `HarnessGuardedAgent` around the result — never the reverse. `Compose_ToolAutoApprovalEnabled_GetServiceNeverExposesRawApprovalSurface` confirms the outer surface hides every inner layer. |
| Approval middleware state is binding-guarded | Pass | Approval-enabled pipelines add an outer execution-binding client before approval response binding/bypassing, while retaining the inner binding client that guards every provider call. `ApprovalBinding_ContextMismatchDoesNotConsumePendingRequestState` proves a mismatched-context attempt cannot consume pending approval state and the same response succeeds once the original binding is restored. |
| Capability union generalized coherently; G2-only default guard unchanged | Pass | `HarnessCapabilityResolver`'s requested-capability set is extended per-test via an explicit `HashSet<HarnessCapability>` (mirroring the G3-planning precedent), not by changing shared defaults. `HarnessCompositionGuard.Validate`'s default two-argument overload and its pre-existing tests are untouched; approval-specific coherence is entirely owned by the new, separate `HarnessApprovalCompositionGuard`. |
| No raw approval surface, `ChatClientAgentOptions`, approval middleware, or mutable provider state through `GetService` | Pass | `HarnessGuardedAgent.GetService`'s blocklist rejects `ToolApprovalAgent`, `ToolApprovalAgentOptions`, `ChatClientAgentOptions`, `ChatClientAgent`, `ChatOptions`, and `IChatClient` even when `ToolAutoApproval` is composed in, verified directly by `Compose_ToolAutoApprovalEnabled_GetServiceNeverExposesRawApprovalSurface`. |
| Approved response invokes exactly once | Pass | `Compose_ApprovedResponse_InvokesApprovalRequiredToolExactlyOnce`: a real `ApprovalRequiredAIFunction`-wrapped tool is invoked exactly once after `approvalRequest.CreateResponse(true, null)` is submitted, verified via both an invocation counter and a `FunctionResultContent` in the response. |
| Rejected response invokes zero times | Pass | `Compose_RejectedResponse_InvokesApprovalRequiredToolZeroTimes`: `approvalRequest.CreateResponse(false, "not authorized")` produces zero tool invocations and a `FunctionResultContent` containing MAF's own `"Tool call invocation rejected."` text plus the caller-supplied reason. |
| Forged/mismatched response fails closed | Pass | Two complementary tests document the exact mechanism: (1) with `ApprovalResponseBinding` enabled, `Compose_ForgedUnboundResponse...` shows a response with a fabricated `RequestId` (but the real `ToolCall`/`CallId`) is silently dropped by `ApprovalResponseBindingChatClient` before `FunctionInvokingChatClient` ever sees it, which then throws `InvalidOperationException` because the original request remains unresolved — zero invocations. (2) without any approval capability selected, `Compose_NoApprovalCapabilitySelected_ForgedResponseWithGuessedCallIdStillInvokes` documents (does not endorse) that MAF's own `FunctionInvokingChatClient` resolves approval responses purely by `ToolCall.CallId`, not `RequestId`, so a forged `RequestId` reusing a real `CallId` still invokes — this is exactly why `ApprovalResponseBinding` exists as an independently selectable, non-default-on capability. |
| Session serialize/restore of pending approval state | Pass | `DeserializeSession_RestoresPendingApprovalRequestState_ThenApprovesExactlyOnce`: a pending approval request created under one composed agent/execution context is serialized, restored into an independently composed agent under a fresh (but identity-matching) execution context, and approved there, invoking the tool exactly once on the new agent's tool instance. |
| Approval-only session envelope | Pass | Any coherent approval plugin activates the trusted session envelope without requiring history/planning. Schema version 2 binds identity, session, persistence mode, provider keys, and the canonical enabled-capability set. `DeserializeSession_EnabledCapabilitiesMismatch_FailsClosed` rejects restore under a different approval profile. |
| Identity/session/workspace mismatch fails closed before approval decision | Pass | `DeserializeSession_MismatchedUserIdentity_FailsClosedBeforeApprovalStateIsRead` tampers the approval-only envelope's `userId` and fails before approval state is read. `EnsureApprovalReauthorized_IdentityMismatch_FailsClosedBeforeValidatorInvoked` proves the per-run reauthorization gate fails on an identity swap without invoking the host validator. |
| Standing approval requires host reauthorization after restore and on newly supplied always-approve content | Pass | `Compose_ToolAutoApproval_StandingApprovalGrant_ReauthorizesAndInvokesExactlyOnce`: turn 1 reauthorizes a continued session with reason `ContinuedSessionReauthorization`; turn 2 reauthorizes newly supplied always-approve content with reason `NewlySuppliedStandingApproval`; the validator runs twice and the tool once. |
| Standing approval rejection by host fails closed, zero invocations | Pass | `Compose_ToolAutoApproval_StandingApprovalDeclinedByHost_FailsClosedZeroInvocations`: a host validator that declines only the `NewlySuppliedStandingApproval` reason causes the always-approve round trip to throw, with zero tool invocations. |
| Standing approvals are reauthorized one at a time | Pass | `EnsureApprovalReauthorized_BatchedStandingApprovals_FailClosed` submits two always-approve responses in one run and proves Foundry rejects the batch before invoking the host validator or inner agent, preventing one tool's authorization from implicitly covering another. |
| No approval plugin/capability mismatch produces an agent | Pass | Seven dedicated guard tests (`Compose_ResponseBindingEnabledWithoutPlugin_FailsClosedWithoutAgent`, `Compose_NotRequiredBypassingEnabledWithoutPlugin_FailsClosedWithoutAgent`, `Compose_ToolAutoApprovalEnabledWithoutPlugin_FailsClosedWithoutAgent`, `Compose_PluginWhenNoCapabilityEnabled_FailsClosedWithoutAgent`, `Compose_ResponseBindingSuppliedWhileDisabled_FailsClosedWithoutAgent`, `Compose_NotRequiredBypassingSuppliedWhileDisabled_FailsClosedWithoutAgent`, `Compose_ToolAutoApprovalSuppliedWhileDisabled_FailsClosedWithoutAgent`) each assert `result.Agent is null` with the precise `HarnessProviderCompositionStatus` for that specific mismatch direction. |
| Outer guarded surface and strict no-run-options behavior remain intact | Pass | `Compose_ApprovalEnabled_RunWithNonNullOptions_ThrowsAndInvokesToolZeroTimes` shows a non-null `ChatClientAgentRunOptions` (including an attempted `ChatClientFactory` replacement) is still rejected before any model call for an approval-enabled composition, with zero underlying chat-client calls and zero tool invocations — matching the pre-existing G2 `EnsureSupported` contract exercised generically in `HarnessProviderCompositionTests`. |
| Structured progress events emitted once per accepted transition | Pass | Approval responses are reported only after the inner run accepts them; a forged response that fails binding emits no approved/rejected event. Streaming approval requests are deduplicated by `RequestId`. Approve, reject, forged, and standing-reauthorization tests assert event counts and correlations. |
| Only public MAF 1.15 seams used | Pass | Composition uses `ChatClientBuilderExtensions.UseApprovalResponseBinding`/`UseApprovalNotRequiredFunctionBypassing`, `ChatClientAgentOptions.DisableApprovalResponseBinding`/`DisableApprovalNotRequiredFunctionBypassing`, `ToolApprovalAgent`/`ToolApprovalAgentOptions`, `ApprovalRequiredAIFunction`, `ToolApprovalRequestContent`/`ToolApprovalResponseContent`/`AlwaysApproveToolApprovalResponseContent`, and `ToolApprovalRequestContentExtensions.CreateResponse`/`CreateAlwaysApproveToolResponse` — all public MAF/MEAI types. No internal MAF approval chat-client type (e.g. the internal `ApprovalResponseBindingChatClient`/`PerServiceCallChatHistoryPersistingChatClient`-style decorators) is referenced by name anywhere in production code; their behavior was verified only via decompilation research, never depended upon by identity. |

## Standing-approval reauthorization design (review-sensitive)

`HarnessGuardedAgent.EnsureApprovalReauthorizedAsync` invokes the required
host validator whenever `toolAutoApprovalEnabled` is true **and** either (a)
the inbound messages newly supply standing-approval content
(`AlwaysApproveToolApprovalResponseContent`, reason
`NewlySuppliedStandingApproval`), **or** (b) the call continues an existing
session (`session is not null`, reason `ContinuedSessionReauthorization`).

This is intentionally conservative: MAF 1.15 exposes no public API to inspect
whether a continued session's `ToolApprovalAgent`-owned state already
contains a previously recorded standing-approval rule
(`ToolApprovalAgent`'s internal `ToolApprovalState.Rules` collection is not
part of any public surface). Rather than silently trusting a restored or
continued session, Foundry re-runs the host validator on every continued-session
call while `ToolAutoApproval` is enabled — including calls that carry no
approval content at all — so a host-side authorization decision is always
freshly confirmed rather than assumed from persisted state. This is verified
directly by `EnsureApprovalReauthorized_ContinuedSession_RequiresHostReauthorization`
and, for the negative case (no session, no standing content), by
`EnsureApprovalReauthorized_NoSessionAndNoStandingContent_DoesNotInvokeValidator`.

The tradeoff is a host-validator call on every continued-session turn once
`ToolAutoApproval` is selected, even for turns unrelated to approvals. This is
documented, not hidden, and is the deliberate Foundry-owned mitigation for the
risk that MAF's own internal rule-matching (see below) could otherwise
silently reuse a standing grant across turns with no host visibility.

## CallId-vs-RequestId forgery finding (review-sensitive)

Decompilation of `FunctionInvokingChatClient.ExtractAndRemoveApprovalRequestsAndResponses`
established that MAF's own pairing of a `ToolApprovalResponseContent` to its
originating request is keyed purely by `ToolCall.CallId`. Correlation to a
`dictionary` keyed by `RequestId` only populates optional diagnostic fields on
the result (`Request`/`RequestMessage`); a `RequestId` that was never issued
does not stop the response from being accepted and invoked using the
response's own (potentially forged) `ToolCall`. `CallId` values are visible in
ordinary conversation history returned to any caller, so this is not a
meaningful barrier on its own.

This means `ApprovalResponseBinding` (MAF's `UseApprovalResponseBinding`
chat-client decorator, keyed by `RequestId` against session-scoped pending
state) is the **sole** forgery-protection layer among the three capabilities;
it is not a decorative addition to `ToolAutoApproval` or `ApprovalNotRequiredBypassing`.
`Compose_NoApprovalCapabilitySelected_ForgedResponseWithGuessedCallIdStillInvokes`
documents the undefended baseline precisely so this property is never assumed
to hold when `ApprovalResponseBinding` is not explicitly selected.

## Approval session-envelope binding (review-sensitive)

`HarnessGuardedAgent.sessionContinuityEnabled` is true when any history,
planning, or approval plugin is present. Approval-only profiles therefore use
the Foundry session envelope without requiring an unrelated history capability.
Schema version 2 adds the canonical enabled-capability set so opaque MAF
approval state cannot be restored under a different selected approval profile.
Identity, session, workspace, persistence mode, provider keys, and enabled
capabilities are validated before the inner MAF session is deserialized.

## Scoped-out test surface (review-sensitive)

`ToolApprovalAgent.RunCoreAsync` (decompiled) unwraps
`AlwaysApproveToolApprovalResponseContent` into a recorded
`ToolApprovalRule` **before** ever calling the inner agent for that same turn,
meaning a fresh, unrelated future request for the same tool name could, in
principle, be silently auto-approved by MAF's own internal rule-matching in a
third conversational turn, without any host check, purely inside
`ToolApprovalAgent`'s own internal state. Modeling this exact three-turn
scenario (plain text at one chat-client call index, then a brand-new function
call several turns later with a gap) was not practical with the existing
`HarnessQueuedFunctionCallChatClient` test double, which has no gap/skip
capability, so it was **deliberately scoped out** of this test file rather
than approximated with a misleading assertion.

This is an accepted, documented limitation of the test surface, not of the
production mitigation: Foundry's own `EnsureApprovalReauthorizedAsync` gate
re-validates every continued-session call while `ToolAutoApproval` is enabled
(see "Standing-approval reauthorization design" above) regardless of whatever
internal rule-matching MAF performs, and that gate is directly tested by the
2-turn round trip and the isolated `HarnessGuardedAgent`-construction tests in
this file. If a future slice needs to assert the exact 3rd-turn MAF-internal
behavior end-to-end, a chat-client test double with gap/skip support would
need to be added first.

## Progress event contract

Four new immutable `IProgressEvent` records were added to
`Progress/HarnessApprovalProgressEvents.cs`, following the existing base
shape (`Timestamp`, `WorkflowId`, `AgentId`, `ParentAgentId`, `Depth`,
`SequenceNumber`) used by every other concrete progress event in the
assembly:

| Event | Emitted when | Key fields |
|---|---|---|
| `HarnessApprovalRequestedEvent` | A `ToolApprovalRequestContent` surfaces in the composed agent's response. | `RequestId`, `ToolName` |
| `HarnessApprovalApprovedEvent` | An ordinary (non-standing) approval response approves the pending request. | `RequestId`, `ToolName` |
| `HarnessApprovalRejectedEvent` | An ordinary (non-standing) approval response rejects the pending request. | `RequestId`, `ToolName`, `Reason` |
| `HarnessApprovalStandingReauthorizedEvent` | The mandatory host reauthorization check runs for a standing/always-approve approval, whether granted or declined. | `ToolName` (nullable), `Granted` |

No MAF internal type is exposed through any of these records; all fields are
primitive/stable identifiers (`string`, `string?`, `bool`, `long`, `int`,
`DateTimeOffset`). Reporting is done via the existing
`IProgressReporterAccessor`/`ProgressReporter` seam already used by other
Harness slices; no new global or static state was introduced.
`ProgressEventCoverageTests.KnownConcreteEvents_MatchTheRegisteredCatalogue`
(a pre-existing regression lock enumerating every concrete `IProgressEvent`
type in the assembly) was updated to acknowledge all four new types as a
deliberate acknowledgement, per that test's own documented contract; no sink
or `PipelineRunExtensions` code required changes because none of them perform
exhaustive type-based dispatch over progress events.

## AOT evidence status

No new Harness AOT application was added or run in this slice. This is the
same accepted gap recorded in `gate-g3-history.md` and `gate-g3-planning.md`:
current AOT jobs do not invoke `HarnessProviderComposition`, so no direct AOT
execution proof exists for the approval composition path either. The new
`HarnessCapability.ApprovalResponseBinding`/`ApprovalNotRequiredBypassing`/`ToolAutoApproval`
capability definitions carry whatever static AOT status was already recorded
in `HarnessCapabilityResolver.Definitions` prior to this slice; this slice did
not modify capability AOT status metadata. Closing this gap requires the same
minimum Harness AOT application called out in the prior two G3 gates, still
outstanding.

## Diagnostics status

No new OpenTelemetry activity/metric instrumentation was added specifically
for approval. The existing telemetry composition (`OpenTelemetry` capability,
wired identically regardless of whether approval capabilities are selected)
is unchanged; approval-specific observability is carried exclusively through
the new structured progress events described above, which is a separate,
lighter-weight seam than the diagnostics pipeline and was the explicitly
requested mechanism (T036).

## API disposition

- `HarnessApprovalPlugin`, `HarnessApprovalCompositionGuard`
  (+`Result`/`Status`), `HarnessApprovalHostValidator`,
  `HarnessApprovalHostValidationContext`, `HarnessApprovalHostValidationReason`:
  all `internal`, one type per file, visible to the test project only via the
  assembly's existing `InternalsVisibleTo`. No public Foundry Harness API was
  added for approval composition or the host validator seam.
- `HarnessApprovalRequestedEvent`, `HarnessApprovalApprovedEvent`,
  `HarnessApprovalRejectedEvent`, `HarnessApprovalStandingReauthorizedEvent`:
  **public** sealed records, matching the existing public disposition of
  every other concrete `IProgressEvent` type in `Progress/`. Every public
  member on all four records has an XML doc comment. No MAF/MEAI internal
  type is referenced by any public member.
- No `Microsoft.Extensions.DependencyInjection`/Needlr references were added.
- No optional parameters or default interface members were introduced;
  `HarnessProviderCompositionRequest.ApprovalPlugin` is a single required
  (non-defaulted) nullable parameter.
- No G4 workspace provider, G5 compaction/reducer, todo/agent-mode (beyond
  what G3-planning already delivered), skills, or web-search behavior was
  added or modified.
- Public Foundry Harness API: not approved by this gate.
- Package publication: none from this gate.
- Ordinary non-Harness and non-approval-requesting G2/G3-history/G3-planning
  behavior: unchanged (all 95 pre-existing Harness tests continue to pass
  unmodified).

## Test counts

| Scope | Passed | Failed |
|---|---|---|
| `HarnessApprovalTests` only | 35 | 0 |
| Full Harness filter (`FullyQualifiedName~Harness`) | 130 | 0 |
| Full `NexusLabs.Foundry.MicrosoftAgentFramework.Tests` project | 1,699 | 0 |

## Stop-condition evaluation

- G2 runtime order, default two-argument `HarnessCompositionGuard.Validate`,
  and its existing tests remain intact and unmodified: **yes**
- Single selected-provider composition root preserved, no parallel composer
  or duplicated G2/history/planning logic: **yes**
- Each of the three approval capabilities is independently gated; MAF
  defaults never implicitly enable an unselected capability: **yes**
- `HarnessGuardedAgent` remains the outermost returned surface, with
  `ToolApprovalAgent` (when present) composed inside it around the raw
  `ChatClientAgent`: **yes**
- No raw `ToolApprovalAgent`, `ChatClientAgentOptions`, approval middleware,
  or mutable provider state is reachable through `GetService`: **yes**
- Approved response invokes exactly once; rejected invokes zero;
  invalid/forged/restored-untrusted response invokes zero: **yes**
- A restored or newly supplied standing approval never becomes trusted
  solely because it exists in messages/session state; a required host
  reauthorization delegate is invoked first: **yes**
- Multiple standing approvals in one run fail closed before host validation or
  rule persistence: **yes**
- Fails closed on identity/session/workspace mismatch, missing host
  validator, rejected standing reauthorization, forged/mismatched approval
  correlation, and restored pending approval state that cannot be validated: **yes**
- Only public MAF 1.15 seams are used; no internal approval chat-client type
  is depended upon by identity: **yes**
- Structured progress events are emitted once per requested/accepted approved/
  rejected/standing-reauthorized transition: **yes**
- Strict G2 per-run options contract (`AgentRunOptions` rejected) preserved
  for approval-enabled compositions: **yes**

The G3 approval stop condition is not triggered.

## Next permitted work

Proceed with the next independently gated stable selected-provider slice:
trusted skills composition or provider-dependent web search. Loop evaluation,
workspace, compaction, and the complete bundle remain in their later gates.
The two accepted gaps recorded above — no direct Harness AOT
execution proof, and the scoped-out third-turn MAF-internal standing-rule
re-approval integration test — remain open and should be revisited before any
public Harness API or approval profile promotion.
