# Gate G3 Decision — Skills Slice

## Decision

**PASS FOR INTERNAL SELECTED-PROVIDER SKILLS SLICE**

The MAF 1.15 / MEAI 10.6 selected-provider lane can advance one additional
stable capability slice: host-authored, in-memory/inline skills composed
through the upstream `AgentSkillsProvider` / `AgentInlineSkill` APIs. This
gate approves only that slice's internal composition, guard coherence,
file-backed-source rejection, explicit trust-policy control of MAF's three
skill-tool approval gates, session state-key union, and `GetService` hiding
behavior.

No public Harness composition/configuration API, web search, workspace
bridge, compaction, complete bundle, or loop evaluator behavior is approved by
this gate. No new public API was introduced by this slice: `HarnessSkillsPlugin`,
`HarnessSkillsTrustPolicy`, and the composition guard types are all `internal`.

## Evidence identity

- G2 baseline: `57193a74bbfe2d9b383dacaefb7d9c237f73eea2`
  (`feat: establish Harness composition foundation (#83)`)
- G3 history slice: `45f066d` (`feat: add Harness session continuity slice (#84)`)
- G3 planning slice: `f714991` (`feat: add Harness planning providers slice (#85)`)
- G3 approval slice: branch `harness/g3-approval` (no commit/push/PR at time of
  that slice)
- G3 skills slice: branch `harness/g3-skills` (this work; no commit, push, or
  PR was made)
- Local deterministic validation (this slice):
  - `HarnessSkillsTests` in isolation: 24 passed, 0 failed.
  - Full Harness filter (`FullyQualifiedName~Harness`): 154 passed, 0 failed
    (130 pre-existing G2/G3-history/G3-planning/G3-approval tests + 24 new
    G3 skills tests; no regressions).
  - Full `NexusLabs.Foundry.MicrosoftAgentFramework.Tests` project: 1,723
    passed, 0 failed (1,699 pre-existing + 24 new).
- All `dotnet build`/`dotnet test` commands were run with
  `$env:NUGET_PACKAGES='G:\dev\caches\nuget\packages'` set.
- No hosted CI run was performed for this slice; no commit, push, or PR was
  made as part of this work.
- Exact MAF 1.15 API shapes (tool name constants, private-method parameter
  names that become the public JSON-schema parameter names, `AgentInlineSkill`
  constructors and fluent `AddResource`/`AddScript` methods,
  `AgentSkillsProviderOptions`'s three `Disable*Approval` flags,
  `AgentSkillsProvider.StateKeys`) were confirmed via reflection against the
  real MAF 1.15 assemblies in a disposable scratch probe project, which was
  deleted before this evidence file was written and is not part of this
  slice's deliverable.

## Files changed

New files:
- `src/NexusLabs.Foundry.MicrosoftAgentFramework/Harness/Providers/HarnessSkillsPlugin.cs`
- `src/NexusLabs.Foundry.MicrosoftAgentFramework/Harness/Providers/HarnessSkillsTrustPolicy.cs`
- `src/NexusLabs.Foundry.MicrosoftAgentFramework/Harness/Providers/HarnessSkillsCompositionGuard.cs`
- `src/NexusLabs.Foundry.MicrosoftAgentFramework/Harness/Providers/HarnessSkillsCompositionGuardResult.cs`
- `src/NexusLabs.Foundry.MicrosoftAgentFramework/Harness/Providers/HarnessSkillsCompositionGuardStatus.cs`
- `src/NexusLabs.Foundry.MicrosoftAgentFramework.Tests/Harness/HarnessSkillsTests.cs` (24 tests)
- `src/NexusLabs.Foundry.MicrosoftAgentFramework.Tests/Harness/HarnessCollidingSkillsChatHistoryProvider.cs`
- `specs/001-maf-harness-first-class/evidence/gate-g3-skills.md` (this file)

Modified files:
- `src/NexusLabs.Foundry.MicrosoftAgentFramework/Harness/HarnessProviderCompositionRequest.cs`
  (one required nullable `HarnessSkillsPlugin? SkillsPlugin` argument, inserted
  between `ApprovalPlugin` and `Metrics`)
- `src/NexusLabs.Foundry.MicrosoftAgentFramework/Harness/HarnessProviderCompositionStatus.cs`
  (`SkillsPluginUnexpected`, `SkillsPluginRequired`, `SkillsApprovalCoherenceRequired`)
- `src/NexusLabs.Foundry.MicrosoftAgentFramework/Harness/HarnessProviderComposition.cs`
  (skills guard call + `MapSkillsStatus`, `BuildProviderStateKeys`/
  `BuildSupportedCapabilities` extended with `skillsPlugin`, `AIContextProviders`
  generalized into a `List<AIContextProvider>` union of planning + skills,
  `sessionContinuityEnabled` extended)
- `src/NexusLabs.Foundry.MicrosoftAgentFramework.Tests/Harness/HarnessCompositionTestFixture.cs`
  (`CreateSkillsProfile`, `CreateSkillsPlugin`, `CreateRequest` cascades
  accepting `skillsPlugin`)
- `src/NexusLabs.Foundry.MicrosoftAgentFramework.Tests/Harness/HarnessApprovalTests.cs`
  (one call site updated to name `skillsPlugin`/`progressAccessor` explicitly,
  since it previously relied on positional argument order that the new field
  insertion would otherwise silently break)

Every existing call site of `HarnessProviderCompositionRequest` outside this
slice's own tests passes `skillsPlugin: null`, preserving prior G2/G3-history/
G3-planning/G3-approval behavior unchanged. `HarnessCapability.Skills`'s
registration in `HarnessCapabilityResolver` (`AotStatus.Unverified`,
`DiagnosticsStatus.Partial`, `HarnessDeliveryPhase.G3`,
`HarnessCapabilityTrustBoundary.ExternalContent`) predates this slice and was
not modified; this slice only supplies the plugin/guard/composition wiring
that lets that pre-registered capability actually compose an agent.

## Gate evidence

| Criterion | Result | Evidence |
|---|---|---|
| Single composition root preserved | Pass | `HarnessProviderComposition.Compose` remains the only selected-provider composition root. Skills wiring is an additive branch inside the existing method (guard check, provider-state-key union, `AIContextProviders` union), gated behind `HarnessSkillsCompositionGuard.Validate`. No parallel composer, and no duplicated history/planning/approval logic was added — the skills guard reuses the existing `HarnessApprovalCompositionGuard`/approval-plugin coherence contract by requiring it to already hold, rather than re-implementing it (see "Approval coherence is delegated, not duplicated" below). |
| Narrow, immutable skills plugin | Pass | `HarnessSkillsPlugin` is `internal sealed`, has a private constructor, exposes only three read-only properties, and is constructed exclusively through `Create`, which requires non-empty inline skills, a defined two-value trust policy, and valid upstream provider state keys. |
| One required nullable plugin argument, all call sites updated | Pass | `HarnessProviderCompositionRequest.SkillsPlugin` is a single required (non-optional, no default) nullable positional parameter. Every pre-existing G2/G3-history/G3-planning/G3-approval call site (composition tests, fixture helpers, and the one previously-positional call site in `HarnessApprovalTests.cs`) supplies `null`/`skillsPlugin: null` explicitly; the compiler enforces this because the parameter has no default. |
| Only host-authored in-memory/inline skills accepted; file-backed sources rejected | Pass | `HarnessSkillsPlugin.Create`'s only accepted collection element type is `AgentInlineSkill`. `AgentFileSkill` has no public constructor (producible only by `AgentFileSkillsSource`, which `HarnessSkillsPlugin` never references), so the C# type system rejects file-backed skills at compile time. `HarnessSkillsPlugin_PublicSurface_NeverAcceptsFileBackedSkillTypes` reflects over every public/internal method and constructor parameter of `HarnessSkillsPlugin` and asserts none has a parameter type of `string`, `IEnumerable<string>`, `AgentFileSkill`, `AgentFileSkillsSource`, `AgentFileSkillsSourceOptions`, `AgentFileSkillScriptRunner`, or the base `AgentSkillsSource`. |
| No reimplementation of skill loading/resources/scripts | Pass | `HarnessSkillsPlugin.Create` constructs the upstream `AgentSkillsProvider` directly (`new AgentSkillsProvider(skills, options, loggerFactory)`) and performs no independent skill-loading, resource-serving, or script-execution logic of its own. `Compose_HostTrusted_LoadResourceAndScript_ExecuteDirectlyViaHostDelegates` drives the real `load_skill`/`read_skill_resource`/`run_skill_script` tools end to end through a composed agent and asserts on MAF's own function-result content, never a Foundry-owned substitute. |
| Explicit, non-implicit trust policy governs MAF's three approval gates | Pass | `HarnessSkillsPlugin.Create` sets `AgentSkillsProviderOptions.DisableLoadSkillApproval`/`DisableReadSkillResourceApproval`/`DisableRunSkillScriptApproval` to `true` if and only if the caller explicitly passed `HarnessSkillsTrustPolicy.HostTrusted`; selecting the `Skills` capability alone (with no plugin, or with an `ApprovalRequired` plugin) never disables them. `Compose_ApprovalRequired_LoadSkillSurfacesApprovalRequestAndExecutesOnceApproved` and `Compose_ApprovalRequired_RejectedResponse_InvokesLoadSkillZeroTimes` prove the gates stay active end to end for the `ApprovalRequired` policy; `Compose_HostTrusted_LoadResourceAndScript_ExecuteDirectlyViaHostDelegates` proves all three gates are bypassed only for `HostTrusted`. |
| Approval-required skills variant requires coherent approval capability/plugin | Pass | `HarnessSkillsCompositionGuard.Validate` requires `HarnessCapability.ApprovalResponseBinding` to be enabled and the supplied `HarnessApprovalPlugin.ResponseBindingEnabled` to be `true` whenever `HarnessSkillsTrustPolicy.ApprovalRequired` is selected, so a stray `ApprovalRequired` skills plugin cannot silently leave pending approval requests unresolvable. `Compose_ApprovalRequiredWithoutResponseBindingCapability_FailsClosed` proves the failure when the capability is not selected at all. |
| Approval coherence is delegated, not duplicated | Pass | `Compose_ApprovalRequiredWithResponseBindingCapabilityButNoApprovalPlugin_FailsClosedViaApprovalGuard` and `Compose_ApprovalRequiredWithIncoherentApprovalPlugin_FailsClosedViaApprovalGuard` demonstrate that when the `ApprovalResponseBinding` capability is selected but no coherent approval plugin is supplied, the pre-existing `HarnessApprovalCompositionGuard` (not a duplicated skills-local check) rejects the request first, with its own distinct `ApprovalResponseBindingRequired` status — the skills guard's coherence check only fires in the complementary case where the approval layer's own guard would otherwise pass (capability not selected / no plugin) but the skills trust policy still demands it. This proves no parallel approval-coherence logic was written for the skills slice. |
| Only one host-trusted policy variant is supported; other variants rejected | Pass | `HarnessSkillsTrustPolicy` is a closed two-member enum (`ApprovalRequired`, `HostTrusted`); there is no partial/mixed disable-flag combination reachable from `HarnessSkillsPlugin.Create` — the three `Disable*Approval` flags are derived as a single boolean (`trustPolicy == HostTrusted`), never set independently. `Compose_HostTrustedSkillsOnly_ActivatesNoUnselectedCapability` confirms host-trusted composition activates no unselected `Todo`/`AgentMode`/approval capability alongside it. |
| Skills composed through `ChatClientAgentOptions.AIContextProviders`; generalized union, not combinatorial static sets | Pass | `HarnessProviderComposition.Compose` builds a single `List<AIContextProvider>` that conditionally appends `PlanningProviders.AIContextProviders` and `SkillsPlugin.SkillsProvider` independently — there is one union code path regardless of which subset of {none, planning, skills, both} is present, not a per-combination static list. `Compose_NoSkills_PreservesPriorNonSkillsBehavior` proves the planning-only and neither-present paths are unaffected. |
| Stateful provider callbacks are binding-guarded | Pass | `HarnessGuardedAgent` validates the execution binding before entering any inner agent/provider callback and after completion, while the existing inner chat-client binding still validates every provider model call. Skills/planning/approval state cannot be touched by a run that begins outside the trusted context. |
| Provider state keys join the trusted schema-v2 envelope; null/empty/duplicate/cross-plugin collisions fail before construction | Pass | `BuildProviderStateKeys` unions history, planning, and skills provider state keys and rejects the whole request with `HarnessProviderCompositionStatus.ProviderStateKeyCollision` if the combined set contains a duplicate, before any agent is built. `HarnessSkillsPlugin.Create` independently validates its own provider's keys. `Compose_CollidingCustomProviderStateKey_FailsClosedBeforeAgentIsBuilt` proves a custom history provider colliding with `AgentSkillsProvider` is rejected before composition. |
| Raw skill/provider/options surface hidden by `GetService` | Pass | `HarnessGuardedAgent.GetService`'s pre-existing `typeof(AIContextProvider).IsAssignableFrom(serviceType)` blocklist check already generically covers `AgentSkillsProvider` (which extends `AIContextProvider`) — no new hiding code was required for this slice. `Compose_HostTrustedSkills_NeverExposesRawProviderThroughGetService` proves `GetService(typeof(AgentSkillsProvider))`, `GetService(typeof(AIContextProvider))`, `GetService(typeof(ChatClientAgentOptions))`, `GetService(typeof(ChatClientAgent))`, and `GetService(typeof(IChatClient))` all return `null` through the composed outer agent even when a host-trusted skills plugin is active. No public skill accessor was added; a binding-aware immutable surface was judged unnecessary for conformance since the composed agent's own `RunAsync` already exercises the skill tools end to end. |
| Session serialize/restore proven honestly, not fabricated | Pass | Reflection confirmed `AgentSkillsProvider.StateKeys` is a fixed constant (`["AgentSkillsProvider"]`), not session/instance-specific state — the provider is effectively stateless with respect to `AgentSession` serialize/deserialize. `DeserializeSession_FreshAgentUnderCurrentWorkspaceRestoresEnvelopeWithSkillsStateKey` therefore proves what is actually true: a session serialized under one host-trusted skills-composed agent restores cleanly under an independently composed fresh agent whose provider-state-key set includes `AgentSkillsProvider`, and the restored agent still executes `load_skill` successfully — without asserting any fabricated skill-specific conversational payload survived the round trip. This honesty is documented inline as a code comment at the test site. |
| Capability/profile mismatch, provider-state-key collision, identity/workspace mismatch fail closed | Pass | `DeserializeSession_EnabledCapabilityProfileMismatch_FailsClosed`, `DeserializeSession_ProviderStateKeyProfileMismatch_FailsClosed`, and `DeserializeSession_IdentityMismatch_FailsClosed` each tamper one dimension of the schema-v2 envelope (enabled-capability set, provider-state-key set, `userId`) and prove restoration fails before any skills-specific state is read, matching the pre-existing G2/G3-approval envelope-tamper pattern. |
| Strict no-run-options behavior preserved | Pass | `Compose_SkillsComposed_OtherRunOptions_AreRejectedBeforeModelCall` shows a non-null `ChatClientAgentRunOptions` is rejected before any model call for a skills-composed agent, with zero underlying chat-client calls — matching the pre-existing G2 `EnsureSupported` contract. |
| Existing 130 Harness tests and prior slices remain green | Pass | Full Harness filter: 154 passed, 0 failed (130 pre-existing + 24 new). Full MAF test project: 1,723 passed, 0 failed. |
| Only public MAF 1.15 seams used | Pass | Composition uses `AgentSkillsProvider`, `AgentInlineSkill`, `AgentSkillsProviderOptions` (`DisableLoadSkillApproval`/`DisableReadSkillResourceApproval`/`DisableRunSkillScriptApproval`), `AgentSkillsProvider.LoadSkillToolName`/`ReadSkillResourceToolName`/`RunSkillScriptToolName`, and `ChatClientAgentOptions.AIContextProviders` — all public MAF types. No internal MAF skills type is referenced by name anywhere in production code; exact tool/parameter names were confirmed only via reflection research against the shipped 1.15 assemblies, never depended upon by an internal type identity. |
| AOT / diagnostics status unchanged from pre-existing registration | Pass | `HarnessCapability.Skills`'s registration in `HarnessCapabilityResolver` (`HarnessCapabilityAotStatus.Unverified`, `HarnessCapabilityDiagnosticsStatus.Partial`, `HarnessDeliveryPhase.G3`) predates this slice and was not modified. This slice does not claim AOT verification or full diagnostics coverage for the Skills capability; it only supplies the composition wiring that makes the pre-registered capability actually usable. |
| No new public API | Pass | `HarnessSkillsPlugin`, `HarnessSkillsTrustPolicy`, `HarnessSkillsCompositionGuard`, `HarnessSkillsCompositionGuardResult`, and `HarnessSkillsCompositionGuardStatus` are all `internal`. `HarnessProviderCompositionRequest.SkillsPlugin` and the three new `HarnessProviderCompositionStatus` values are additive members of pre-existing `internal` types. No new public type, member, or overload was added to the assembly's public surface. |

## Trust boundary and threat model (review-sensitive)

Skills is registered with `HarnessCapabilityTrustBoundary.ExternalContent`
(pre-existing, unmodified by this slice) because the tool results returned by
`load_skill`/`read_skill_resource`/`run_skill_script` — skill instructions,
resource content, and script output — become part of the model's context and
are indistinguishable, once ingested, from any other externally-sourced
content the model might act on. G3 narrows this further: only host-authored
`AgentInlineSkill` instances are accepted (skills are defined by delegates and
literal strings supplied directly by the calling code, not fetched from any
external source at runtime), so the "external" risk this boundary flags is
about the *content a skill's resources/scripts might return at call time* (a
host-authored delegate could, in principle, read live external state), not
about *skill discovery* — which this slice eliminates entirely by rejecting
file-backed sources.

Because the actual risk is about what a skill's resources/scripts might do or
return, not how the skill was declared, the explicit trust-policy split exists
so callers must actively choose:

- `HarnessSkillsTrustPolicy.ApprovalRequired` (the conservative policy a caller
  must select explicitly): MAF's own per-call approval gates
  for all three skill tools stay enabled, and — because those approval
  requests need a resolution path — this slice additionally requires the
  `ApprovalResponseBinding` capability and a coherent `HarnessApprovalPlugin`
  to be composed alongside it (`HarnessSkillsCompositionGuard`).
- `HarnessSkillsTrustPolicy.HostTrusted`: the caller affirmatively asserts
  that every supplied inline skill's resources/scripts are trusted host code
  with no untrusted external content risk, and only then are MAF's three
  approval gates disabled.

Selecting the `Skills` capability by itself — with no plugin, or with a
plugin that does not explicitly request `HostTrusted` — never disables MAF's
default approval behavior. This is the concrete meaning of "no silent default
override" for this slice, and is directly exercised by
`Compose_ApprovalRequired_LoadSkillSurfacesApprovalRequestAndExecutesOnceApproved`
(gates active, approval flow works end to end) versus
`Compose_HostTrusted_LoadResourceAndScript_ExecuteDirectlyViaHostDelegates`
(gates bypassed only under the explicit `HostTrusted` policy).

## Approval-coherence delegation design (review-sensitive)

`HarnessSkillsCompositionGuard.Validate` deliberately does **not**
re-implement `HarnessApprovalCompositionGuard`'s own capability/plugin
coherence rules (e.g. "a plugin claiming `ResponseBindingEnabled` while the
capability is not selected"). `HarnessProviderComposition.Compose` runs the
approval guard first; if the approval layer's own contract is violated, that
guard's own distinct status (e.g. `ApprovalResponseBindingRequired`) is
returned before the skills guard ever executes. The skills guard's own
coherence check only adds the one requirement the approval guard cannot know
about on its own: that an `ApprovalRequired` skills plugin specifically
needs `ApprovalResponseBinding` (not `ApprovalNotRequiredBypassing` or
`ToolAutoApproval` alone) selected and coherent, because the skill-tool
approval requests it produces are ordinary per-call `ToolApprovalRequestContent`
flows that only `ApprovalResponseBinding` protects against request-forgery
for. This ordering and division of responsibility is proven by
`Compose_ApprovalRequiredWithResponseBindingCapabilityButNoApprovalPlugin_FailsClosedViaApprovalGuard`
and
`Compose_ApprovalRequiredWithIncoherentApprovalPlugin_FailsClosedViaApprovalGuard`
(both fail via the approval guard, not the skills guard) alongside
`Compose_ApprovalRequiredWithoutResponseBindingCapability_FailsClosed` (fails
via the skills guard specifically, in the one case the approval guard cannot
catch: capability simply not selected at all).

## Session envelope / state-key union design (review-sensitive)

`HarnessSkillsPlugin.ProviderStateKeys` exposes the upstream
`AgentSkillsProvider.StateKeys` (confirmed via reflection to be the fixed
constant `["AgentSkillsProvider"]` — not session- or instance-specific),
sorted and de-duplicated. `HarnessProviderComposition.BuildProviderStateKeys`
unions this with any history/planning provider state keys into one
`distinctCount != keys.Count` collision check, generalizing cleanly across
{history, planning, skills} presence combinations rather than special-casing
each pairing. `HarnessGuardedAgent.sessionContinuityEnabled` now also
considers `SkillsPlugin is not null`, so a skills-only composition (no
history, no planning, no approval) still uses the trusted schema-v2 session
envelope rather than silently falling back to unmanaged MAF session
serialization.

Because `AgentSkillsProvider.StateKeys` is a fixed constant rather than
instance/session data, this slice cannot honestly claim to prove
skill-specific conversational state survives a serialize/deserialize round
trip — there is no such state to prove. `DeserializeSession_FreshAgentUnderCurrentWorkspaceRestoresEnvelopeWithSkillsStateKey`
proves instead the property that actually holds: the schema-v2 envelope
(identity, workspace, provider-state-key set including `AgentSkillsProvider`,
enabled-capability set) round-trips correctly under a fresh, independently
composed agent, and that agent's `load_skill` tool still functions afterward.

## Scoped-out surface (review-sensitive)

Consistent with the task's explicit scope boundary, this slice does not
touch: web search, the workspace bridge, compaction, the complete session
bundle, loop evaluators, or any new public API. `AgentFileSkill`,
`AgentFileSkillsSource`, raw file paths, `AgentFileSkillScriptRunner`, and any
ambient filesystem skill-discovery source are rejected by construction (see
"Only host-authored in-memory/inline skills accepted" above) and remain
deferred to a future G4 slice, where they would need to be reconciled with
the already-deferred `FileAccess`/`FileMemory` capabilities rather than
introduced ad hoc here.

## Test inventory (T037)

24 `[Fact]` tests in `HarnessSkillsTests.cs`:

| Area | Tests |
|---|---|
| Plugin construction guards | `CreateSkillsPlugin_NoSkillsSupplied_FailsClosed`, `CreateSkillsPlugin_NullSkillEntry_FailsClosed`, `CreateSkillsPlugin_UnknownTrustPolicy_FailsClosed`, `CreateSkillsPlugin_HostTrusted_ExposesProviderAndCanonicalStateKey`, `CreateSkillsPlugin_ApprovalRequired_ExposesProviderAndCanonicalStateKey` |
| File-backed-type rejection | `HarnessSkillsPlugin_PublicSurface_NeverAcceptsFileBackedSkillTypes` |
| Capability/plugin symmetry fail-closed | `Compose_SkillsEnabledWithoutPlugin_FailsClosedWithoutAgent`, `Compose_PluginWhenSkillsCapabilityNotEnabled_FailsClosedWithoutAgent` |
| Trust-policy/approval coherence | `Compose_ApprovalRequiredWithoutResponseBindingCapability_FailsClosed`, `Compose_ApprovalRequiredWithResponseBindingCapabilityButNoApprovalPlugin_FailsClosedViaApprovalGuard`, `Compose_ApprovalRequiredWithIncoherentApprovalPlugin_FailsClosedViaApprovalGuard`, `Compose_ApprovalRequiredWithCoherentApprovalPlugin_Succeeds` |
| Provider-state-key collision | `Compose_CollidingCustomProviderStateKey_FailsClosedBeforeAgentIsBuilt` |
| Strict no-run-options | `Compose_SkillsComposed_OtherRunOptions_AreRejectedBeforeModelCall` |
| Host-trusted conformance / no unselected capability | `Compose_HostTrustedSkillsOnly_ActivatesNoUnselectedCapability`, `Compose_HostTrusted_LoadResourceAndScript_ExecuteDirectlyViaHostDelegates` |
| Approval-required end-to-end flow | `Compose_ApprovalRequired_LoadSkillSurfacesApprovalRequestAndExecutesOnceApproved`, `Compose_ApprovalRequired_RejectedResponse_InvokesLoadSkillZeroTimes` |
| `GetService` hiding | `Compose_HostTrustedSkills_NeverExposesRawProviderThroughGetService` |
| Session serialize/deserialize honesty | `DeserializeSession_FreshAgentUnderCurrentWorkspaceRestoresEnvelopeWithSkillsStateKey` |
| Envelope tamper fail-closed | `DeserializeSession_EnabledCapabilityProfileMismatch_FailsClosed`, `DeserializeSession_ProviderStateKeyProfileMismatch_FailsClosed`, `DeserializeSession_IdentityMismatch_FailsClosed` |
| G2 regression | `Compose_NoSkills_PreservesPriorNonSkillsBehavior` |

## Validation commands run

```powershell
$env:NUGET_PACKAGES='G:\dev\caches\nuget\packages'
dotnet build src\NexusLabs.Foundry.MicrosoftAgentFramework.Tests\NexusLabs.Foundry.MicrosoftAgentFramework.Tests.csproj
dotnet test src\NexusLabs.Foundry.MicrosoftAgentFramework.Tests\NexusLabs.Foundry.MicrosoftAgentFramework.Tests.csproj --filter "FullyQualifiedName~HarnessSkillsTests"
dotnet test src\NexusLabs.Foundry.MicrosoftAgentFramework.Tests\NexusLabs.Foundry.MicrosoftAgentFramework.Tests.csproj --filter "FullyQualifiedName~Harness"
dotnet test src\NexusLabs.Foundry.MicrosoftAgentFramework.Tests\NexusLabs.Foundry.MicrosoftAgentFramework.Tests.csproj
```

Results: 24/24, 154/154, 1723/1723 — all passed, 0 failed, 0 skipped.

No commit, push, or pull request was made as part of this work, per explicit
instruction. All changes remain local on `harness/g3-skills`.
