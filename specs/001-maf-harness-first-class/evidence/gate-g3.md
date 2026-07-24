# Gate G3 Decision — Final Cumulative Slice Gate

## Decision

**PASS FOR ALL FIVE INTERNAL SELECTED-PROVIDER G3 CAPABILITY SLICES**

The MAF 1.15 / MEAI 10.6 selected-provider lane has now delivered, and this
gate approves as a single cumulative record, all five G3 stable capability
slices defined in Phase 3 of `tasks.md`:

1. **Session continuity** — per-service-call chat history persistence
   (`HarnessHistoryProviderPlugin`).
2. **Planning** — upstream `TodoProvider`/`AgentModeProvider` composition
   (`HarnessPlanningProvidersPlugin`).
3. **Tool approval** — the three independent approval capabilities
   (`ApprovalResponseBinding`, `ApprovalNotRequiredBypassing`,
   `ToolAutoApproval`) via `HarnessApprovalPlugin`.
4. **Skills** — host-authored, in-memory/inline skills via
   `HarnessSkillsPlugin`, with explicit trust-policy control of MAF's three
   skill-tool approval gates.
5. **Web search** — the provider-dependent `WebSearch` capability composing
   exactly one public MEAI `HostedWebSearchTool` marker via
   `HarnessWebSearchPlugin` (this slice, T039-T040).

Each slice was, and remains, independently enabled, independently observable,
independently tested, and does not implicitly activate another capability —
satisfying Phase 3's per-slice checkpoint for all five. `HarnessProviderComposition.Compose`
remains the single selected-provider composition root across all five slices;
no parallel composer was ever introduced, and no slice duplicated another
slice's guard logic.

No public Harness composition/configuration API, workspace bridge, compaction,
complete session bundle, or loop evaluator behavior is approved by this gate.
All plugins, guards, results, and statuses remain `internal`. The approval slice
did add four documented public `IProgressEvent` records; those progress records
are the only public API introduced by G3.

## Evidence identity

Cumulative slice history on top of the G2 foundation gate (`gate-g2.md`):

| Slice | Commit / branch | PR | New tests | Cumulative Harness tests | Cumulative project tests |
|---|---|---|---|---|---|
| G2 foundation (baseline) | `57193a74bbfe2d9b383dacaefb7d9c237f73eea2` | [#83](https://github.com/ncosentino/foundry/pull/83) | — | 47 | 1,616 |
| History | `45f066d` | [#84](https://github.com/ncosentino/foundry/pull/84) | 29 | 76 | 1,645 |
| Planning | `f714991` | [#85](https://github.com/ncosentino/foundry/pull/85) | 19 | 95 | 1,664 |
| Approval | `2974fc9` | [#87](https://github.com/ncosentino/foundry/pull/87) | 35 | 130 | 1,699 |
| Skills | `556a458` | [#88](https://github.com/ncosentino/foundry/pull/88) | 24 | 154 | 1,723 |
| Web search (this work) | branch `harness/g3-web`, based on `556a458` (no commit, push, or PR was made) | — | 16 | 170 | 1,739 |

- Current branch `harness/g3-web` HEAD: `556a458d707917882cf40ba7416531aa380e61fa`
  (identical to the Skills slice merge commit; all web-search changes remain
  uncommitted working-tree edits, per explicit instruction).
- Local deterministic validation for the web-search slice specifically:
  - `HarnessWebSearchCapabilityTests` in isolation: **16 passed, 0 failed**.
  - Full Harness filter (`FullyQualifiedName~Harness`): **170 passed, 0
    failed** (154 pre-existing G2/G3-history/G3-planning/G3-approval/G3-skills
    tests + 16 new web-search tests; no regressions).
  - Full `NexusLabs.Foundry.MicrosoftAgentFramework.Tests` project: **1,739
    passed, 0 failed** (1,723 pre-existing + 16 new).
  - Full `src` solution build (`dotnet build src`): succeeded with 0 errors.
    Pre-existing generated-code `CS0162` warnings in unrelated examples vary
    with clean versus incremental build order and are unchanged by this work.
- All `dotnet build`/`dotnet test` commands were run with
  `$env:NUGET_PACKAGES='G:\dev\caches\nuget\packages'` set.
- No hosted CI run was performed for the web-search slice; no commit, push,
  or PR was made as part of this work, matching the approval and skills
  slices before it.
- Exact MAF 1.15 / MEAI 10.6 API shapes for this slice
  (`HostedWebSearchTool`'s two public constructors, its fixed default
  `Name` value of `"web_search"`, its direct `AITool` base, and that
  `AIFunction`/`HostedWebSearchTool` are disjoint `AITool` subtypes) were
  confirmed via reflection against the real MAF/MEAI assemblies in a
  disposable scratch probe project (`C:\dev\scratch\websearchprobe`), which
  was deleted before this evidence file was written and is not part of this
  slice's deliverable.

## Files changed (web search slice, T039-T040)

New files:
- `src/NexusLabs.Foundry.MicrosoftAgentFramework/Harness/Providers/HarnessWebSearchPlugin.cs`
- `src/NexusLabs.Foundry.MicrosoftAgentFramework/Harness/Providers/HarnessWebSearchCompositionGuard.cs`
- `src/NexusLabs.Foundry.MicrosoftAgentFramework/Harness/Providers/HarnessWebSearchCompositionGuardResult.cs`
- `src/NexusLabs.Foundry.MicrosoftAgentFramework/Harness/Providers/HarnessWebSearchCompositionGuardStatus.cs`
- `src/NexusLabs.Foundry.MicrosoftAgentFramework.Tests/Harness/HarnessWebSearchCapabilityTests.cs` (16 tests)
- `specs/001-maf-harness-first-class/evidence/gate-g3.md` (this file — the
  final cumulative G3 gate required by T040)

Modified files:
- `src/NexusLabs.Foundry.MicrosoftAgentFramework/Harness/HarnessProviderCompositionRequest.cs`
  (one required nullable `HarnessWebSearchPlugin? WebSearchPlugin` argument,
  inserted between `SkillsPlugin` and `Metrics`)
- `src/NexusLabs.Foundry.MicrosoftAgentFramework/Harness/HarnessProviderCompositionStatus.cs`
  (`WebSearchPluginUnexpected`, `WebSearchPluginRequired`,
  `WebSearchToolNameCollision`, `WebSearchToolTypeCollision`)
- `src/NexusLabs.Foundry.MicrosoftAgentFramework/Harness/HarnessProviderComposition.cs`
  (web-search guard call + `MapWebSearchStatus`; `ChatOptions.Tools`
  generalized into a `List<AITool>` that conditionally appends the hosted
  marker after the generated functions; `BuildSupportedCapabilities` extended
  with `webSearchPlugin`. Deliberately **not** touched: `BuildProviderStateKeys`'s
  parameter list and `sessionContinuityEnabled`'s OR-condition — see "Session
  envelope / state-key non-participation design" below.)
- `src/NexusLabs.Foundry.MicrosoftAgentFramework.Tests/Harness/HarnessCompositionTestFixture.cs`
  (`CreateWebSearchProfile`, `CreateWebSearchPlugin`, one new deepest
  `CreateRequest` overload accepting `webSearchPlugin`; the pre-existing
  `(..., skillsPlugin, progressAccessor)` overload signature is unchanged and
  forwards `webSearchPlugin: null` internally, so no other test file needed
  any call-site change)
- `src/NexusLabs.Foundry.MicrosoftAgentFramework.Tests/Harness/HarnessScriptedChatClient.cs`
  (added a `LastOptions` capture property so tests can assert on the exact
  composed `ChatOptions.Tools` shape; purely additive, no existing test
  behavior changed)

Every existing call site of `HarnessProviderCompositionRequest` outside this
slice's own tests passes `webSearchPlugin: null` (via the unchanged, forwarded
fixture overload), preserving prior G2/G3-history/G3-planning/G3-approval/
G3-skills behavior unchanged. `HarnessCapability.WebSearch`'s registration in
`HarnessCapabilityResolver` (`ProviderDependent`, required
`HarnessProviderCapability.HostedWebSearch`, `HarnessCapabilityTrustBoundary.ExternalContent`,
`HarnessCapabilityAotStatus.Unverified`, `HarnessCapabilityDiagnosticsStatus.Partial`,
`HarnessDeliveryPhase.G3`) predates this slice and was **not** modified; this
slice only supplies the plugin/guard/composition wiring that lets that
pre-registered, provider-dependent capability actually compose an agent.

## Cumulative capability matrix

| Capability | Selection mechanism | Independently enabled? | Provider evidence required? | No-op / deferred baseline preserved? | Slice gate |
|---|---|---|---|---|---|
| `PerServiceHistory` (`InMemory`, caller-persisted `Serialized`, caller-supplied `DurableProvider`; `ServiceManaged` deferred) | `HarnessHistoryProviderPlugin` | Yes — selecting/omitting history does not touch planning/approval/skills/web search | Provider-specific evidence is still required before `ServiceManaged` can advance | Yes — `HarnessHistoryPersistenceMode.NotApplicable` / no plugin reproduces pre-history G2 behavior | `gate-g3-history.md` |
| `Todo` | `HarnessPlanningProvidersPlugin.TodoProvider` | Yes — independent of `AgentMode` within the same plugin | No | Yes — `TodoProvider: null` omits the capability | `gate-g3-planning.md` |
| `AgentMode` | `HarnessPlanningProvidersPlugin.AgentModeProvider` | Yes — independent of `Todo` within the same plugin | No | Yes — `AgentModeProvider: null` omits the capability | `gate-g3-planning.md` |
| `ApprovalResponseBinding` | `HarnessApprovalPlugin.ResponseBindingEnabled` | Yes | No | Yes | `gate-g3-approval.md` |
| `ApprovalNotRequiredBypassing` | `HarnessApprovalPlugin.NotRequiredBypassingEnabled` | Yes | No | Yes | `gate-g3-approval.md` |
| `ToolAutoApproval` | `HarnessApprovalPlugin.ToolApprovalOptions` | Yes | No | Yes | `gate-g3-approval.md` |
| `Skills` (two trust-policy variants: `HostTrusted`, `ApprovalRequired`) | `HarnessSkillsPlugin` | Yes — composes independently of history/planning/approval/web search; `ApprovalRequired` variant additionally requires `ApprovalResponseBinding` coherence (delegated to the existing approval guard, not duplicated) | No | Yes — no plugin omits the capability | `gate-g3-skills.md` |
| `WebSearch` (provider-dependent) | `HarnessWebSearchPlugin` | Yes — composes independently of history/planning/approval/skills; contributes no `AIContextProvider`, provider state key, or session envelope trigger | **Yes** — `HarnessProviderCapability.HostedWebSearch` host evidence is authoritative; without it the capability remains `Deferred`/non-executable | Yes — no plugin, or capability not requested/`Deferred`, preserves the pre-web-search baseline | This cumulative gate |

`WebSearch` is the only G3 capability in this matrix that is
provider-dependent: every other G3 capability in the table above is a
host-composition choice with no required `HarnessProviderCapability`
evidence. This asymmetry is intentional and pre-dates this slice (`HarnessCapabilityResolver`'s
`ProviderDependent` registration for `WebSearch` was added at G2 and never
modified); this slice's contribution is solely the composition wiring that
makes the pre-registered, evidence-gated capability usable.

## Independent enablement / no implicit activation evidence

- `Compose_WebSearchEnabled_AppendsExactlyOneHostedWebSearchToolMarker`
  additionally asserts `agent.GetService<TodoProvider>()`,
  `agent.GetService<AgentModeProvider>()`,
  `agent.GetService<IHarnessTodoAccessor>()`,
  `agent.GetService<IHarnessAgentModeAccessor>()`, and
  `agent.GetService<AgentSkillsProvider>()` are all `null` when only
  `WebSearch` is composed — proving composing web search alone activates no
  planning/skills capability.
- `Compose_NoWebSearch_PreservesPriorNonWebSearchBehavior` proves the
  pre-web-search G2 baseline composition (no history, planning, approval,
  skills, or web-search plugin) still succeeds identically.
- Symmetric evidence exists for each earlier slice in its own gate document
  (e.g. `Compose_HostTrustedSkillsOnly_ActivatesNoUnselectedCapability` for
  Skills, `Compose_ApprovalRequiredWithCoherentApprovalPlugin_Succeeds`
  alongside the approval-only regression test for Approval, etc.) — this
  cumulative gate does not repeat those individual assertions verbatim but
  references them as already-passing, unmodified tests (all still green in
  the 170-test Harness run recorded above).
- `Compose_WebSearchEnabledWithoutPlugin_FailsClosedWithoutAgent`,
  `Compose_PluginWhenWebSearchNotRequested_FailsClosedWithoutAgent`, and
  `Compose_PluginWhenWebSearchDeferredWithoutProviderEvidence_FailsClosedWithoutAgent`
  together prove the full fail-closed symmetry matrix for the one
  provider-dependent capability: capability without plugin fails,
  plugin without capability fails, and — the case unique to `WebSearch` among
  all five slices — a plugin supplied while the capability is merely
  requested-but-`Deferred` (no host-supplied `HostedWebSearch` evidence) fails
  exactly as if the capability had never been requested at all.

## Trust, session-state, public API, AOT, diagnostics, and provider-capability status (cumulative)

| Dimension | Status | Notes |
|---|---|---|
| Trust boundary | `HarnessCapabilityTrustBoundary.ExternalContent` for `Skills` and `WebSearch` (pre-existing registrations, unmodified); other G3 capabilities carry their own pre-existing boundaries recorded in their slice gates | `WebSearch` is `ExternalContent` because a live web search's results become part of the model's context indistinguishably from any other externally sourced content — this slice does not change or attempt to mitigate that; it only composes the marker |
| Session-state participation | `PerServiceHistory`, `Todo`/`AgentMode` (via `AIContextProvider` state), `Skills` all contribute provider state keys and extend `sessionContinuityEnabled`; **`WebSearch` deliberately does not** | `HostedWebSearchTool` is a stateless marker (confirmed via reflection: no `AgentSession.StateBag` interaction, not an `AIContextProvider`) — `Compose_WebSearchOnly_NoProviderStateKeysAndNoSessionEnvelopeActivation` proves a WebSearch-only composed agent's serialized session contains none of `providerStateKeys`/`enabledCapabilities`/`schemaVersion`, i.e. the trusted schema-v2 envelope is never entered for this capability alone |
| Public API | No public composition/configuration API; four approval progress records added | Every plugin/guard/result/status type is `internal`. `HarnessApprovalRequestedEvent`, `HarnessApprovalApprovedEvent`, `HarnessApprovalRejectedEvent`, and `HarnessApprovalStandingReauthorizedEvent` are documented public `IProgressEvent` records; no other G3 public API was added. |
| AOT status | `Unverified` for `WebSearch` (pre-existing registration, unmodified); other G3 capabilities carry their own pre-existing AOT status | This slice does not claim AOT verification for `WebSearch`; no direct Harness AOT execution proof exists yet for any G3 selected-provider slice (an accepted, carried-forward gap — see "Accepted/deferred items" below) |
| Diagnostics status | `Partial` for `WebSearch` (pre-existing registration, unmodified) | Composition-level diagnostics (guard rejection reasons, capability evidence) are observable; end-to-end hosted-provider web-search telemetry is out of scope for this slice and was never claimed |
| Provider-capability status | `WebSearch` is the only G3 capability requiring host-supplied provider capability evidence (`HarnessProviderCapability.HostedWebSearch`); resolved authoritatively by the pre-existing `HarnessCapabilityResolver`, never inferred by this slice | `Profile_WebSearchRequestedWithoutProviderEvidence_RemainsDeferredNonExecutable` / `Profile_WebSearchRequestedWithProviderEvidence_ResolvesEnabled` confirm resolver behavior through this slice's own fixture helper; `HarnessCapabilityProfileTests.Resolve_ProviderDependentWithoutEvidence_DefersIt`/`Resolve_ProviderDependentWithEvidence_EnablesIt` (pre-existing, unmodified) confirm it directly against the resolver |

## No-auto-detection structural evidence (review-sensitive)

`HarnessWebSearchPlugin`, `HarnessWebSearchCompositionGuard`,
`HarnessWebSearchCompositionGuardResult`, and
`HarnessWebSearchCompositionGuardStatus` contain no member (constructor
parameter, method parameter or return type, property, or field) of type
`ChatClientMetadata`, no `Delegate`-derived member, and no member name
containing `providername` or `modelname` (case-insensitive, underscore-
normalized) — verified by
`HarnessWebSearchPlugin_and_Guard_NeverReferenceProviderNameOrChatClientMetadata`,
which reflects over the full declared member surface of all four types.
Whether `HarnessCapability.WebSearch` is `Enabled` is decided exclusively by
`HarnessCapabilityResolver` from host-supplied
`HarnessProviderCapability.HostedWebSearch` evidence passed into
`HarnessCapabilityResolutionRequest.ProviderCapabilities` — there is no
runtime code path in this slice that inspects a provider or model name,
queries `ChatClientMetadata`, or otherwise infers capability from the
underlying `IChatClient`'s identity.

## Collision-guard design (review-sensitive)

`HarnessWebSearchCompositionGuard.Validate` performs two responsibilities in
one pass, mirroring the capability-enabled/plugin-supplied symmetry check
shared by every other single-plugin guard in this codebase (Approval, Skills):

1. **Capability/plugin symmetry** — a supplied plugin while `WebSearch` is
   not `Enabled` (never requested, or requested but `Deferred` due to missing
   provider evidence) is `WebSearchPluginUnexpected`; an `Enabled` capability
   without a supplied plugin is `WebSearchPluginRequired`.
2. **Name/type collision against generated tools** — before any agent is
   built, the guard checks every generated `AIFunction`'s `Name` against the
   hosted marker's `Name` (`"web_search"` by MEAI's own default), and
   defensively checks `GetType()` equality against the marker's exact runtime
   type. `Compose_GeneratedToolNameCollidesWithHostedMarker_FailsClosedBeforeAgentIsBuilt`
   proves the real, reachable name-collision scenario
   (`WebSearchToolNameCollision`). The type-collision branch
   (`WebSearchToolTypeCollision`) is retained as defensive dead code — proven
   structurally unreachable today by
   `HostedWebSearchTool_and_AIFunction_AreDisjointTypes_TypeCollisionIsUnreachable`,
   since every generated function resolved by
   `HarnessGeneratedToolResolution` is an `AIFunction`, and `AIFunction`/
   `HostedWebSearchTool` are disjoint `AITool` subtypes.

`HarnessProviderComposition.Compose` appends the hosted marker to
`ChatOptions.Tools` after the generated functions in a single
`List<AITool>` construction — there is exactly one hosted marker addition
path and no second function-invocation loop or Foundry-owned `AIFunction`
standing in for a web search implementation anywhere in this slice.

## Session envelope / state-key non-participation design (review-sensitive)

Unlike every other G3 slice (History, Planning, Skills), `WebSearch`
deliberately does **not** extend `HarnessProviderComposition.BuildProviderStateKeys`'s
parameter list and does **not** appear in the
`sessionContinuityEnabled` OR-condition passed to `HarnessGuardedAgent`'s
constructor. This is a considered per-slice design choice, not an oversight:
`HostedWebSearchTool` is a stateless upstream marker with no
`AgentSession.StateBag` interaction and no `AIContextProvider` participation
(reflection-confirmed), so unioning it into the state-key set or the envelope
trigger would create a claim this slice cannot honestly back — that hosted
web search has session state to protect. `Compose_WebSearchOnly_NoProviderStateKeysAndNoSessionEnvelopeActivation`
proves this directly: composing `WebSearch` alone still routes
`SerializeSessionCoreAsync` through `HarnessGuardedAgent`'s
`!sessionContinuityEnabled` early-return branch, bypassing
`HarnessSessionEnvelope` entirely, so the serialized session contains none of
`providerStateKeys`, `enabledCapabilities`, or `schemaVersion`.

## Scoped-out surface (review-sensitive)

Consistent with the task's explicit scope boundary, this slice does not
implement a Foundry-owned web search client, does not invoke Bing/Azure or
any concrete search backend, and does not infer capability from a provider or
model name. It also does not touch the workspace bridge, compaction, the
complete session bundle, or loop evaluators, and introduces no optional
parameters, default interface members, or Needlr dependency. All of these
remain deferred to their respective later gates (see "Next permitted work"
below).

## Accepted/deferred items (cumulative across all five G3 slices)

- No direct Harness AOT execution proof exists yet for any G3 selected-provider
  slice (carried forward from G2/G3-approval/G3-skills; still open).
- The scoped-out third-turn MAF-internal standing-rule re-approval
  integration test (Approval slice; still open).
- File-backed skill sources (`AgentFileSkill`, `AgentFileSkillsSource`, raw
  file paths, `AgentFileSkillScriptRunner`) remain rejected by construction
  and deferred to a future slice (Skills slice; still open).
- A concrete Foundry-owned web search backend, Bing/Azure invocation, or any
  provider/model-name-based capability inference is explicitly **not**
  accepted work for this or any future slice under the current architecture
  — `HostedWebSearchTool` is, and remains, the only supported mechanism.
- `WebSearchToolTypeCollision` is retained as structurally unreachable
  defensive code (see "Collision-guard design" above); it is accepted as
  intentional defense-in-depth, not dead code to be removed.

## Test inventory (T039)

16 `[Fact]`/`[Fact] async Task` tests in `HarnessWebSearchCapabilityTests.cs`:

| Area | Tests |
|---|---|
| Plugin construction | `CreateWebSearchPlugin_Default_WrapsHostedWebSearchToolMarker`, `HarnessWebSearchPlugin_Create_NullTool_FailsClosed` |
| Resolver/composition-level defer/enable + trust/AOT/diagnostics truthfulness | `Profile_WebSearchRequestedWithoutProviderEvidence_RemainsDeferredNonExecutable`, `Profile_WebSearchRequestedWithProviderEvidence_ResolvesEnabled`, `Profile_WebSearchEvidence_TrustBoundaryAotDiagnosticsRemainAsPreRegistered` |
| Capability/plugin symmetry fail-closed (including the deferred-without-evidence case) | `Compose_WebSearchEnabledWithoutPlugin_FailsClosedWithoutAgent`, `Compose_PluginWhenWebSearchNotRequested_FailsClosedWithoutAgent`, `Compose_PluginWhenWebSearchDeferredWithoutProviderEvidence_FailsClosedWithoutAgent` |
| Collision guard | `Compose_GeneratedToolNameCollidesWithHostedMarker_FailsClosedBeforeAgentIsBuilt`, `HostedWebSearchTool_and_AIFunction_AreDisjointTypes_TypeCollisionIsUnreachable` |
| Coherent composition / no unselected activation | `Compose_WebSearchEnabled_AppendsExactlyOneHostedWebSearchToolMarker` |
| `GetService` hiding | `Compose_WebSearchComposed_NeverExposesRawToolOrOptionsThroughGetService` |
| Session-state/envelope non-activation | `Compose_WebSearchOnly_NoProviderStateKeysAndNoSessionEnvelopeActivation` |
| Strict no-run-options | `Compose_WebSearchComposed_OtherRunOptions_AreRejectedBeforeModelCall` |
| G2/G3 regression | `Compose_NoWebSearch_PreservesPriorNonWebSearchBehavior` |
| Structural no-auto-detection | `HarnessWebSearchPlugin_and_Guard_NeverReferenceProviderNameOrChatClientMetadata` |

## Validation commands run

```powershell
$env:NUGET_PACKAGES='G:\dev\caches\nuget\packages'
dotnet build src\NexusLabs.Foundry.MicrosoftAgentFramework\NexusLabs.Foundry.MicrosoftAgentFramework.csproj
dotnet build src\NexusLabs.Foundry.MicrosoftAgentFramework.Tests\NexusLabs.Foundry.MicrosoftAgentFramework.Tests.csproj
dotnet test src\NexusLabs.Foundry.MicrosoftAgentFramework.Tests\NexusLabs.Foundry.MicrosoftAgentFramework.Tests.csproj --filter "FullyQualifiedName~HarnessWebSearchCapabilityTests"
dotnet test src\NexusLabs.Foundry.MicrosoftAgentFramework.Tests\NexusLabs.Foundry.MicrosoftAgentFramework.Tests.csproj --filter "FullyQualifiedName~Harness"
dotnet test src\NexusLabs.Foundry.MicrosoftAgentFramework.Tests\NexusLabs.Foundry.MicrosoftAgentFramework.Tests.csproj
dotnet build src
```

Results: 16/16, 170/170, 1739/1739 — all passed, 0 failed, 0 skipped. Full
`src` solution build: succeeded, 0 errors.

No commit, push, or pull request was made as part of this work, per explicit
instruction. All changes remain local, uncommitted, on `harness/g3-web`.

## Next permitted work

All five Phase 3 G3 slices (history, planning, approval, skills, web search)
are now complete and independently gated. Proceed with:

- **Gate G4** (`specs/001-maf-harness-first-class/evidence/gate-g4.md`,
  T041-T056): the workspace bridge and eager tool-result offload — an
  internal `IWorkspace`-backed MAF file-store bridge, digest-backed artifact
  references, and explicit rehydration, scoped only to semantics already
  proven feasible in `workspace-identity-feasibility.md`.
- **Gate G5** (`gate-g5.md`, T057-T071): experimental hybrid context and
  compaction, explicitly gated as experimental/opt-in.
- **Gate G6** (`gate-g6.md`, T072-T080): the optional complete Harness
  bundle, including any first public Harness API surface decision.
- **Gate G7** (`gate-g7.md`, T081-T094): AOT, analyzer, testing, and
  documentation hardening, including closing the direct Harness AOT
  execution gap accepted above.

No further Phase 3 selected-provider slice work remains; this gate document
supersedes the individual `gate-g3-history.md`, `gate-g3-planning.md`,
`gate-g3-approval.md`, and `gate-g3-skills.md` documents as the authoritative,
cumulative G3 record, without retracting or contradicting any of them.
