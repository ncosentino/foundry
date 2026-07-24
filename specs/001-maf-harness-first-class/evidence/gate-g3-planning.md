# Gate G3 Decision — Planning (Todo / AgentMode) Slice

## Decision

**PASS FOR INTERNAL SELECTED-PROVIDER PLANNING SLICE**

The MAF 1.15 / MEAI 10.6 selected-provider lane can advance one additional
stable capability slice: the upstream `TodoProvider` and `AgentModeProvider`
`AIContextProvider` implementations, each independently selectable. This gate
approves only that slice's internal composition, capability coherence, and
session-envelope state-key generalization evidence.

No public Foundry Harness API, `TodoCompletionLoopEvaluator`,
`CompletionMarkerLoopEvaluator`, background-task evaluators, any other
loop-evaluation behavior (G6), approval, skills, web-search, or G4/G5 work is
approved by this gate.

## Evidence identity

- G2 baseline: `57193a74bbfe2d9b383dacaefb7d9c237f73eea2`
  (`feat: establish Harness composition foundation (#83)`)
- G3 history baseline: `45f066df0091a727c1646f2a93d66a8a582b8196`
  (`feat: add Harness session continuity slice (#84)`)
- G3 planning branch: `harness/g3-planning`
- Local deterministic validation (this slice):
  - `HarnessPlanningProviderTests` in isolation: 19 passed, 0 failed.
  - Full Harness filter (`FullyQualifiedName~Harness`): 95 passed, 0 failed
    (76 pre-existing G2/G3-history tests + 19 G3 planning tests; no
    regressions).
  - Full `NexusLabs.Foundry.MicrosoftAgentFramework.Tests` project:
    1,664 passed, 0 failed.
- No hosted CI run was performed for this slice; no commit, push, or PR was
  made as part of this work.

## Files changed

- New production files:
  - `src/NexusLabs.Foundry.MicrosoftAgentFramework/Harness/Providers/HarnessPlanningProvidersPlugin.cs`
  - `src/NexusLabs.Foundry.MicrosoftAgentFramework/Harness/Providers/HarnessPlanningCompositionGuard.cs`
  - `src/NexusLabs.Foundry.MicrosoftAgentFramework/Harness/Providers/HarnessPlanningCompositionGuardResult.cs`
  - `src/NexusLabs.Foundry.MicrosoftAgentFramework/Harness/Providers/HarnessPlanningCompositionGuardStatus.cs`
  - `src/NexusLabs.Foundry.MicrosoftAgentFramework/Harness/Providers/IHarnessTodoAccessor.cs`
  - `src/NexusLabs.Foundry.MicrosoftAgentFramework/Harness/Providers/HarnessTodoAccessor.cs`
  - `src/NexusLabs.Foundry.MicrosoftAgentFramework/Harness/Providers/HarnessTodoItemSnapshot.cs`
  - `src/NexusLabs.Foundry.MicrosoftAgentFramework/Harness/Providers/IHarnessAgentModeAccessor.cs`
  - `src/NexusLabs.Foundry.MicrosoftAgentFramework/Harness/Providers/HarnessAgentModeAccessor.cs`
  - `src/NexusLabs.Foundry.MicrosoftAgentFramework/Harness/HarnessGuardedAgentServices.cs`
- Edited production files:
  - `src/NexusLabs.Foundry.MicrosoftAgentFramework/Harness/HarnessCompositionGuard.cs`
    (promoted the private `G2SupportedCapabilities` field to an `internal
    static readonly` field so the composition root can build a union instead
    of maintaining a second combinatorial static set)
  - `src/NexusLabs.Foundry.MicrosoftAgentFramework/Harness/HarnessProviderComposition.cs`
    (planning guard validation, `BuildSupportedCapabilities` union builder,
    `BuildProviderStateKeys` union/collision check, `AIContextProviders`
    wiring, generalized `sessionContinuityEnabled`/state-keys)
  - `src/NexusLabs.Foundry.MicrosoftAgentFramework/Harness/HarnessProviderCompositionRequest.cs`
    (added required `HarnessPlanningProvidersPlugin? PlanningProviders`)
  - `src/NexusLabs.Foundry.MicrosoftAgentFramework/Harness/HarnessProviderCompositionStatus.cs`
    (added `PlanningPluginUnexpected`, `TodoProviderRequired`,
    `TodoProviderUnexpected`, `AgentModeProviderRequired`,
    `AgentModeProviderUnexpected`, `ProviderStateKeyCollision`)
  - `src/NexusLabs.Foundry.MicrosoftAgentFramework/Harness/HarnessGuardedAgent.cs`
    (generalized provider-state keys and hides raw stateful providers behind
    binding-aware accessors; no envelope schema/version change)
- New test files:
  - `src/NexusLabs.Foundry.MicrosoftAgentFramework.Tests/Harness/HarnessPlanningProviderTests.cs`
  - `src/NexusLabs.Foundry.MicrosoftAgentFramework.Tests/Harness/HarnessQueuedFunctionCallChatClient.cs`
    (scripted `IChatClient` test double issuing a queued sequence of
    `FunctionCallContent` calls, needed because a single Todo add-then-complete
    scenario spans multiple internal function-invocation round trips)
  - `src/NexusLabs.Foundry.MicrosoftAgentFramework.Tests/Harness/HarnessCollidingChatHistoryProvider.cs`
    (minimal `ChatHistoryProvider` subclass that deliberately reports the
    `TodoProvider` state key, used only to prove the state-key collision guard
    fails closed)
- Edited test file:
  - `src/NexusLabs.Foundry.MicrosoftAgentFramework.Tests/Harness/HarnessCompositionTestFixture.cs`
    (added a 4th `CreateRequest` overload accepting `planningProviders`,
    forwarded as `null` from the three pre-existing overloads so every other
    test file's call sites are unchanged; added `CreatePlanningProfile` and
    `CreatePlanningProvidersPlugin` helpers)

`HarnessCapabilityResolver.cs` required **no changes**: `Todo` and
`AgentMode` were already defined as plain `Stable`, G3, `MafPackage`,
`DefaultEnabledInBundle=true`, `TrustBoundary.None`,
`AotStatus.Unverified`, `DiagnosticsStatus.Partial` capabilities with no
special per-capability coherence method (unlike `PerServiceHistory`'s
`ResolveHistoryCoherence`). The "focused planning profile/plugin coherence
guard" required by this task is `HarnessPlanningCompositionGuard`, operating
at the composition layer, not a resolver change.

## Gate evidence

| Criterion | Result | Evidence |
|---|---|---|
| Single composition root preserved | Pass | `HarnessProviderComposition.Compose` remains the only selected-provider composition root. No parallel composer or duplicate G2/history composition logic was created; the planning guard, capability union, and state-key union are additional steps inside the same method. |
| Todo and AgentMode independently selectable | Pass | `HarnessPlanningProvidersPlugin` takes two independently nullable providers; enabling one never instantiates the other. The guarded agent hides raw `TodoProvider`/`AgentModeProvider` services and exposes only the selected binding-aware `IHarnessTodoAccessor` / `IHarnessAgentModeAccessor`. Todo-only, mode-only, and combined tests assert unselected accessors remain absent. |
| Composed upstream providers via `AIContextProviders`, not reimplemented | Pass | `HarnessProviderComposition.Compose` sets `ChatClientAgentOptions.AIContextProviders = request.PlanningProviders?.AIContextProviders`, which contains only the actual upstream `TodoProvider`/`AgentModeProvider` instances supplied by the caller. No Foundry-owned todo/mode state machine exists. |
| No loop-evaluation behavior introduced (G6 boundary) | Pass | Neither `HarnessPlanningProvidersPlugin` nor `HarnessProviderComposition` reference `TodoCompletionLoopEvaluator`, `CompletionMarkerLoopEvaluator`, background-task evaluators, or any loop-evaluation type. The planning slice only wires `AIContextProviders`; the tool-invocation loop, message injection, and telemetry middleware chain are unchanged from G2/G3-history. |
| Capability set generalized without a combinatorial static set | Pass | `HarnessCompositionGuard.G2SupportedCapabilities` was promoted from `private` to `internal static readonly`. `HarnessProviderComposition.BuildSupportedCapabilities` unions it with `PerServiceHistory` (if a history plugin is present), `Todo` (if a `TodoProvider` is present), and `AgentMode` (if an `AgentModeProvider` is present) — a single union builder rather than a second (or third) hand-maintained `HashSet` literal per plugin combination. |
| G2-only guard overload and its rejection test unchanged | Pass | `HarnessCompositionGuard.Validate(chatClient, profile)` (2-arg) is untouched and still delegates to the 3-arg overload with `G2SupportedCapabilities`; `HarnessLoopOwnershipTests.Validate_LaterPhaseCapability_IsRejectedByG2Composition` continues to pass unmodified. |
| Focused planning coherence guard with precise failures | Pass | `HarnessPlanningCompositionGuard.Validate` reports `TodoProviderRequired`/`AgentModeProviderRequired` (capability selected without matching provider), `PlanningPluginUnexpected` (a plugin supplied while neither capability is selected), and `TodoProviderUnexpected`/`AgentModeProviderUnexpected` (a provider present while its capability is disabled — "plugin contributing an unselected provider"). Each is exercised by a dedicated fail-closed test asserting `result.Agent is null`. |
| State-key collisions rejected before agent construction | Pass | `HarnessProviderComposition.BuildProviderStateKeys` unions the history plugin's and planning plugin's already-canonicalized state keys and fails with `ProviderStateKeyCollision` *before* the execution-binding check, the composition guard, or `builder.BuildAIAgent(...)` run. Proven end-to-end by `Compose_CollidingCustomProviderStateKey_FailsClosedBeforeAgentIsBuilt`, which pairs a deliberately colliding custom `ChatHistoryProvider` (`HarnessCollidingChatHistoryProvider`, reporting the `TodoProvider` key) with an enabled `TodoProvider` and asserts no agent is built. |
| Provider state keys canonicalized ordinally | Pass | `HarnessPlanningProvidersPlugin`'s constructor rejects null/empty/whitespace/duplicate keys and sorts the union with `StringComparer.Ordinal`, mirroring `HarnessHistoryProviderPlugin`'s existing canonicalization. `BuildProviderStateKeys` re-validates the cross-plugin union the same way. |
| Session-envelope state-key binding generalized to the union of every enabled stateful provider | Pass | `HarnessGuardedAgent` now receives `providerStateKeysResult.Keys` (history ∪ planning) instead of only `HistoryProvider?.ProviderStateKeys`; the constructor parameter/field was renamed `historyProviderStateKeys` -> `providerStateKeys` for clarity. History persistence mode (`historyPersistenceMode`) remains its own separate field, untouched by the planning slice. |
| Planning-only agent uses the trusted session envelope | Pass | `sessionContinuityEnabled` generalized from `request.HistoryProvider is not null` to `request.HistoryProvider is not null \|\| request.PlanningProviders is not null`, because Todo/AgentMode store their state in `AgentSession.StateBag`, which only the trusted envelope path validates before create/serialize/deserialize. Proven by `DeserializeSession_FreshAgentUnderCurrentWorkspaceRestoresTodoAndModeState` (planning-only profile, no history plugin) restoring both Todo and AgentMode state across a freshly composed agent under the current authorized workspace. |
| G2 agent with no stateful plugin preserves its raw MAF session lifecycle | Pass | `Compose_NoHistoryNoPlanning_PreservesRawMafSessionLifecycle` (both `HistoryProvider` and `PlanningProviders` null) continues to pass unmodified, matching the pre-existing `Compose_NoHistoryPreservesExistingSessionLifecycleBehavior` in `HarnessHistoryProviderTests`. |
| Fail-closed restore on provider-key/profile mismatch | Pass | `DeserializeSession_ProviderStateKeyProfileMismatch_FailsClosed` tampers the envelope's `providerStateKeys` and asserts `HarnessGuardedAgent.ValidateEnvelope` throws `InvalidOperationException` mentioning "provider state keys", reusing the same generalized check the history slice already exercises. |
| Todo tool conformance (MAF 1.15) | Pass | `Compose_TodoAddThenComplete_ReflectsCompletionStateViaProvider` drives the real function-invocation loop through `todos_add` then `todos_complete`, then verifies completion through the binding-aware `IHarnessTodoAccessor`. |
| AgentMode conformance (MAF 1.15) | Pass | `Compose_AgentModeDefaultAndTransition_ConformsToProviderContract` verifies the configured default, transition to `focused`, and invalid-mode rejection through the binding-aware `IHarnessAgentModeAccessor`. |
| Raw stateful provider escape prevented | Pass | `HarnessGuardedAgent.GetService` blocks all `AIContextProvider` and `ChatHistoryProvider` types, `ChatClientAgentOptions`, `ChatOptions`, and disposable concrete providers. `Compose_PlanningAccessorsRejectExpiredExecutionContext` proves both safe accessors reject reads after scope expiry. |
| Todo reads return immutable snapshots | Pass | MAF 1.15 returns live mutable `TodoItem` objects. `HarnessTodoAccessor` copies each item into `HarnessTodoItemSnapshot` after binding validation, so callers cannot retain a mutable provider-state reference past the authorized call. |
| Combined state keys never collide by construction | Pass | `CreatePlanningProvidersPlugin_Combined_ExposesBothProvidersWithUnionKeys` asserts the canonical ordered union is exactly `["AgentModeProvider", "TodoProvider"]` (upstream fixed single-element keys per provider can never collide with each other); `Compose_TodoAndAgentMode_ExposesBothProviders` confirms combined composition succeeds. |
| Existing history-only behavior and counts remain green | Pass | Full Harness filter: 95 passed (76 pre-existing + 19 new), 0 failed, 0 regressions. Full project: 1,664 passed, 0 failed. |
| Strict G2 per-run options contract preserved | Pass | `HarnessGuardedAgent.EnsureSupported` is unchanged; every composed agent, including planning-enabled agents, still rejects any non-null `AgentRunOptions`. |
| Non-Azure deterministic execution | Pass | Scripted local `IChatClient` fixtures (`HarnessScriptedChatClient`, the new `HarnessQueuedFunctionCallChatClient`) compose and exercise the planning slice without Azure hosting or credentials. |

## Stop-condition evaluation

- G2 runtime order and the G2-only guard's rejection behavior remain intact: **yes**
- Todo and AgentMode remain independently selectable at every layer (plugin, guard, composition, envelope): **yes**
- No loop-evaluation type (`TodoCompletionLoopEvaluator`, `CompletionMarkerLoopEvaluator`, background-task evaluators) was introduced: **yes**
- State-key collisions are rejected before any agent is constructed: **yes**
- Session envelope generalizes cleanly to the union of enabled stateful providers, with history persistence mode kept separate: **yes**
- A planning-only agent uses the trusted session envelope; a fully G2 agent (no history, no planning) preserves its raw MAF session lifecycle: **yes**
- Restored state fails closed on provider-state-key/profile mismatch: **yes**
- Raw Todo/AgentMode/AIContextProvider services are hidden; binding-aware accessors fail after scope expiry: **yes**
- Ordinary G2 and G3-history selected-provider behavior (no planning requested) is unchanged: **yes**

The G3 planning stop condition is not triggered.

## Independent enablement matrix

| Todo capability | AgentMode capability | `TodoProvider` supplied | `AgentModeProvider` supplied | Result |
|---|---|---|---|---|
| Disabled | Disabled | absent | absent | `Success` (no planning; `PlanningProviders` is `null`) |
| Disabled | Disabled | present | any | `PlanningPluginUnexpected` |
| Enabled | Disabled | absent | any | `TodoProviderRequired` |
| Enabled | Disabled | present | present | `AgentModeProviderUnexpected` |
| Enabled | Disabled | present | absent | `Success` (Todo only; `agent.GetService<AgentModeProvider>()` is `null`) |
| Disabled | Enabled | any | absent | `AgentModeProviderRequired` |
| Disabled | Enabled | present | present | `TodoProviderUnexpected` |
| Disabled | Enabled | absent | present | `Success` (AgentMode only; `agent.GetService<TodoProvider>()` is `null`) |
| Enabled | Enabled | present | present | `Success` (both providers exposed; combined state keys `["AgentModeProvider", "TodoProvider"]`) |
| Enabled | Enabled | missing either | — | `TodoProviderRequired` / `AgentModeProviderRequired` (checked independently) |
| (any coherent combination above) | — | colliding custom provider state key present | — | `ProviderStateKeyCollision` (checked centrally in `HarnessProviderComposition`, independent of which capability/provider owns the colliding key) |

## State keys

- `TodoProvider.StateKeys` = `["TodoProvider"]` (fixed, upstream, not configurable).
- `AgentModeProvider.StateKeys` = `["AgentModeProvider"]` (fixed, upstream, not configurable).
- `HarnessPlanningProvidersPlugin.ProviderStateKeys` is the ordinal-sorted, deduplicated union of whichever of the above are present: `[]` is never valid (the plugin cannot be constructed with neither provider), `["AgentModeProvider"]`, `["TodoProvider"]`, or `["AgentModeProvider", "TodoProvider"]`.
- `HarnessProviderComposition.BuildProviderStateKeys` further unions the planning plugin's keys with the history plugin's keys (when both are present) and rejects the whole composition with `ProviderStateKeyCollision` if any key appears more than once across the two plugins, before an agent is built.
- The upstream default `"InMemoryChatHistoryProvider"` state-bag key (MAF's own baseline chat-history bookkeeping, present even with no explicit `ChatHistoryProvider` configured) is not a Harness-tracked provider state key and is not part of any union computed here.

## Trust/identity/session behavior

- `sessionContinuityEnabled` (the switch that activates the trusted `HarnessSessionEnvelope` path in `HarnessGuardedAgent`) is now `request.HistoryProvider is not null || request.PlanningProviders is not null`. A planning-only agent (no history plugin) therefore still validates `HarnessExecutionBinding.EnsureCurrent` before session create/serialize/deserialize and wraps its inner MAF session in the envelope, because Todo/AgentMode state lives in `AgentSession.StateBag` and must not be trusted from an unauthorized context.
- A fully G2 agent (`HistoryProvider` and `PlanningProviders` both `null`) is unaffected: `sessionContinuityEnabled` is `false`, and `HarnessGuardedAgent` delegates directly to the base MAF session lifecycle with no envelope wrapping, exactly as before this slice (`Compose_NoHistoryNoPlanning_PreservesRawMafSessionLifecycle`).
- `historyPersistenceMode` on the envelope remains a strictly history-owned field; the planning slice does not read or write it, and a planning-only agent always carries `HarnessHistoryPersistenceMode.NotApplicable`.
- Fail-closed restore behavior (identity, session id, schema version, persistence mode, and now the generalized provider-state-key union) is unchanged in shape; only the state-key source generalized from history-only to the cross-plugin union.

## AOT evidence status

`HarnessCapabilityResolver.Definitions` already recorded
`HarnessCapabilityAotStatus.Unverified` for both `HarnessCapability.Todo` and
`HarnessCapability.AgentMode` as part of the existing G3 capability table
(unchanged by this slice). That status reflects the capability *evidence
infrastructure* only. Consistent with the G2 and G3-history gates' "Direct AOT
proof" constraint, no new Harness AOT application was added or run in this
slice — the same gap previously recorded (current AOT jobs do not invoke
`HarnessProviderComposition`) still applies here and must be closed by the
later minimum Harness AOT application before profile promotion.

## Diagnostics status

`HarnessCapabilityResolver.Definitions` already recorded
`HarnessCapabilityDiagnosticsStatus.Partial` for both `Todo` and `AgentMode`
(unchanged by this slice). No new diagnostics/telemetry surface was added for
the planning providers beyond what the existing G2 telemetry middleware
already captures for the composed chat client as a whole; per-tool-call
diagnostics specific to `todos_add`/`todos_complete`/`mode_set`/`mode_get`
remain unverified pending dedicated evidence.

## API disposition

- `HarnessPlanningProvidersPlugin`, `HarnessPlanningCompositionGuard`,
  `HarnessPlanningCompositionGuardResult`, and
  `HarnessPlanningCompositionGuardStatus`, plus the Todo/AgentMode accessor
  interfaces, implementations, and Todo snapshot: internal candidates only,
  one type per file, no public Foundry API.
- No `Microsoft.Extensions.DependencyInjection`/Needlr references were added.
- No optional parameters or default interface members were introduced.
- No G4 workspace provider, G5 compaction/reducer, approval, skills, or
  web-search behavior was added or modified.
- No loop-evaluation type (`TodoCompletionLoopEvaluator`,
  `CompletionMarkerLoopEvaluator`, background-task evaluators) was added (G6
  boundary preserved).
- Public Foundry Harness API: not approved.
- Package publication: none from this gate.
- Ordinary non-Harness, non-history, and non-planning G2 behavior: unchanged.

## Next permitted work

Proceed with the next independently gated stable selected-provider slice
(tool approval, or another G3/G4 capability per the task sequence) or the
minimum Harness AOT application needed to close the AOT evidence gap recorded
by this and the prior two gates. `ServiceManaged` history persistence and any
`TodoCompletionLoopEvaluator`/`CompletionMarkerLoopEvaluator`/background-task
loop-evaluation behavior (G6) remain blocked pending their own
evidence-backed gate decisions; do not infer support or add runtime
negotiation without one.

## Subsequent G3 envelope evolution

The approval slice advances `HarnessSessionEnvelope` to schema version 2 by
adding the canonical enabled-capability set and activates the envelope for
approval-only profiles. Todo/AgentMode state-key binding is unchanged; the
additional field prevents restoring provider or approval state under a
different selected capability profile.
