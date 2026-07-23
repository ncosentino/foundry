# Tasks: First-Class Microsoft Agent Framework Harness Support

**Input**: Design documents from
`specs/001-maf-harness-first-class/`

**Prerequisites**: `spec.md`, `plan.md`, `research.md`, `data-model.md`,
`contracts/`, `quickstart.md`, `traceability.md`, `reviews/spec-review.md`,
`reviews/plan-review.md`, `reviews/final-analysis.md`

**Tests**: Required. Deterministic correctness tests precede implementation.
Broad provider and stochastic evaluation tasks run only in hosted CI.

**Organization**: Tasks are grouped by migration gate and user story. The
dependency graph after the task list is authoritative.

## Format

`- [ ] T### [P?] [Story?] Description with file path`

- `[P]`: can proceed in parallel with other ready tasks because it changes
  different files and has no unfinished dependency.
- `[US1]` through `[US4]`: maps the task to the reviewed specification.

## Phase 1: Package Graph and Baseline Compatibility

**Purpose**: Prove the coherent MAF 1.15 / MEAI 10.6 graph before exposing
Harness behavior.

- [ ] T001 Record the pre-uplift package graph and restore output in `specs/001-maf-harness-first-class/evidence/package-baseline.md`
- [ ] T002 [P] Record one canonical targeted test-selection manifest with exact commands, selectors, and hash plus pre-uplift results in `specs/001-maf-harness-first-class/evidence/test-baseline.md`
- [ ] T003 [P] Record the pre-uplift telemetry event/span baseline in `specs/001-maf-harness-first-class/evidence/telemetry-baseline.md`
- [ ] T004 [P] Record the pre-uplift NativeAOT example result and warning set in `specs/001-maf-harness-first-class/evidence/aot-baseline.md`
- [ ] T005 Create an isolated Harness compatibility probe in `src/Examples/AgentFramework/HarnessCompatibilityProbe/`, add `Microsoft.Agents.AI.Harness` 1.15.0, stage MAF core/Workflows/Generators 1.15.0, all six MEAI/Evaluation packages 10.6.0, and DevUI/Hosting/Hosting.OpenAI 1.15 satellite candidates in `src/Directory.Packages.props`
- [ ] T006 Restore and build `src/NexusLabs.Foundry.slnx` plus the direct Harness compatibility probe and record transitive version lifts in `specs/001-maf-harness-first-class/evidence/package-candidate.md`
- [ ] T007 [P] Run the T002 canonical core agent and workflow selectors against the candidate graph and write `specs/001-maf-harness-first-class/evidence/test-core-workflows.md`
- [ ] T008 [P] Run the T002 canonical generator and analyzer selectors against the candidate graph and write `specs/001-maf-harness-first-class/evidence/test-generators-analyzers.md`
- [ ] T009 [P] Run the T002 canonical evaluation, reporting, diagnostics, progress, and testing selectors against the candidate graph and write `specs/001-maf-harness-first-class/evidence/test-evaluation-diagnostics.md`
- [ ] T010 Publish and execute a minimal generated-tool Harness AOT probe with reflection disabled and update `specs/001-maf-harness-first-class/evidence/aot-candidate.md`
- [ ] T011 [P] Assess DevUI 1.15.0 preview compatibility in `specs/001-maf-harness-first-class/evidence/devui-satellite.md`
- [ ] T012 [P] Assess Hosting and Hosting.OpenAI compatibility in `specs/001-maf-harness-first-class/evidence/hosting-satellite.md`
- [ ] T013 Confirm the MAF 1.15 tool-ingress API and trace actual function-loop, message-injection, history-persistence, compaction, approval, and intermediate tool-round lifecycle seams; synthesize test and public API deltas in `specs/001-maf-harness-first-class/evidence/uplift-delta.md`
- [ ] T014 Record Gate G1 pass, fail, or defer decision in `specs/001-maf-harness-first-class/evidence/gate-g1.md`

**Checkpoint G1**: Stop if the core package graph is incoherent, the generated
tool path cannot remain AOT-safe, or targeted regressions lack a migration path.

## Phase 2: Composition Foundation

**Purpose**: Establish internal provider-specific seams without publishing a
permanent API.

### Tests first

- [ ] T015 [P] Add capability-profile contract tests in `src/NexusLabs.Foundry.MicrosoftAgentFramework.Tests/Harness/HarnessCapabilityProfileTests.cs`
- [ ] T016 [P] Add generated-tool ingress tests in `src/NexusLabs.Foundry.MicrosoftAgentFramework.GeneratedWrapper.Tests/HarnessGeneratedToolIngressTests.cs`
- [ ] T017 [P] Add middleware/decorator ordering tests in `src/NexusLabs.Foundry.MicrosoftAgentFramework.Tests/Harness/HarnessCompositionOrderTests.cs`
- [ ] T018 [P] Add one-loop conflict tests in `src/NexusLabs.Foundry.MicrosoftAgentFramework.Tests/Harness/HarnessLoopOwnershipTests.cs`
- [ ] T019 [P] Add telemetry ownership tests in `src/NexusLabs.Foundry.MicrosoftAgentFramework.Tests/Harness/HarnessTelemetryOwnershipTests.cs`
- [ ] T020 Produce the trusted-identity and `AgentFileStore`/`IWorkspace` feasibility report, including unsupported cancellation, authorization, and compare-exchange semantics, in `specs/001-maf-harness-first-class/evidence/workspace-identity-feasibility.md`
- [ ] T021 [P] Add trusted execution-binding and non-adopter regression tests in `src/NexusLabs.Foundry.MicrosoftAgentFramework.Tests/Harness/HarnessExecutionBindingTests.cs` and `src/NexusLabs.Foundry.MicrosoftAgentFramework.Tests/Harness/HarnessOptOutTests.cs`

### Internal candidate implementation

- [ ] T022 Create internal capability evidence records in `src/NexusLabs.Foundry.MicrosoftAgentFramework/Harness/Capabilities/`
- [ ] T023 Create the versioned capability resolver in `src/NexusLabs.Foundry.MicrosoftAgentFramework/Harness/Capabilities/HarnessCapabilityResolver.cs`
- [ ] T024 Create generated-tool ingress using the MAF 1.15 extension point confirmed by T013 and Foundry `IAIFunctionProvider` in `src/NexusLabs.Foundry.MicrosoftAgentFramework/Harness/HarnessGeneratedToolSource.cs`
- [ ] T025 Create the fail-closed trusted execution-binding component in `src/NexusLabs.Foundry.MicrosoftAgentFramework/Harness/HarnessExecutionBinding.cs`
- [ ] T026 Create the internal selected-provider composition entry point in `src/NexusLabs.Foundry.MicrosoftAgentFramework/Harness/HarnessProviderComposition.cs`
- [ ] T027 Implement one-loop and telemetry ownership runtime guards in `src/NexusLabs.Foundry.MicrosoftAgentFramework/Harness/HarnessCompositionGuard.cs`
- [ ] T028 Record the dependency uplift, two consumption lanes, ordering, and composition decisions plus Gate G2 internal/API-candidate disposition in `docs/adr/adr-0005-first-class-maf-harness-integration.md` and `specs/001-maf-harness-first-class/evidence/gate-g2.md`

**Checkpoint G2**: A non-Azure scripted agent runs with generated tools,
inspectable capabilities, one loop, one telemetry owner, and no opt-out behavior
change.

## Phase 3: Stable Selected-Provider Slices

**Purpose**: Add stable capabilities incrementally through the selected-provider
lane.

### Session continuity

- [ ] T029 [P] [US2] Add per-service history, session serialization/restore, provider-state restoration, workspace-binding, and non-durable-default tests in `src/NexusLabs.Foundry.MicrosoftAgentFramework.Tests/Harness/HarnessHistoryProviderTests.cs`
- [ ] T030 [US2] Add stable history-provider composition and explicit supported-persistence modes in `src/NexusLabs.Foundry.MicrosoftAgentFramework/Harness/Providers/HarnessHistoryProviderPlugin.cs`
- [ ] T031 [US2] Emit in-memory versus durable capability evidence and record the session-continuity slice decision in `src/NexusLabs.Foundry.MicrosoftAgentFramework/Harness/Capabilities/HarnessCapabilityResolver.cs` and `specs/001-maf-harness-first-class/evidence/gate-g3-history.md`

### Todo and modes

- [ ] T032 [P] [US2] Add todo and agent-mode conformance tests in `src/NexusLabs.Foundry.MicrosoftAgentFramework.Tests/Harness/HarnessPlanningProviderTests.cs`
- [ ] T033 [US2] Add selected todo and agent-mode composition and record the slice decision in `src/NexusLabs.Foundry.MicrosoftAgentFramework/Harness/Providers/HarnessPlanningProvidersPlugin.cs` and `specs/001-maf-harness-first-class/evidence/gate-g3-planning.md`

### Tool approval

- [ ] T034 [P] [US3] Add structured approval transition, session restore, identity mismatch, and standing-approval reauthorization tests in `src/NexusLabs.Foundry.MicrosoftAgentFramework.Tests/Harness/HarnessApprovalTests.cs`
- [ ] T035 [US3] Add tool-approval composition and host-validation hooks and record the slice decision in `src/NexusLabs.Foundry.MicrosoftAgentFramework/Harness/Providers/HarnessApprovalPlugin.cs` and `specs/001-maf-harness-first-class/evidence/gate-g3-approval.md`
- [ ] T036 [US3] Add approval progress records in `src/NexusLabs.Foundry.MicrosoftAgentFramework/Progress/HarnessApprovalProgressEvents.cs`

### Skills and provider-dependent tools

- [ ] T037 [P] [US2] Add skills capability and trust-boundary tests in `src/NexusLabs.Foundry.MicrosoftAgentFramework.Tests/Harness/HarnessSkillsTests.cs`
- [ ] T038 [US2] Add selected skills composition and record the slice decision in `src/NexusLabs.Foundry.MicrosoftAgentFramework/Harness/Providers/HarnessSkillsPlugin.cs` and `specs/001-maf-harness-first-class/evidence/gate-g3-skills.md`
- [ ] T039 [P] [US2] Add hosted-web-search capability evidence tests in `src/NexusLabs.Foundry.MicrosoftAgentFramework.Tests/Harness/HarnessWebSearchCapabilityTests.cs`
- [ ] T040 [US2] Add provider-dependent web-search registration policy and record the final G3 slice matrix in `src/NexusLabs.Foundry.MicrosoftAgentFramework/Harness/Providers/HarnessWebSearchPlugin.cs` and `specs/001-maf-harness-first-class/evidence/gate-g3.md`

**Checkpoint G3 per slice**: The capability is independently enabled, observable,
tested, and does not activate another capability implicitly.

## Phase 4: Workspace Bridge and Eager Tool-Result Offload

**Purpose**: Make Foundry workspace authoritative before adding compaction.

### Tests first

- [ ] T041 Trace MAF 1.15 tool-result serialization, history insertion, compaction, rehydration, and `AgentFileStore` operations; define the eager-offload interception seam and idempotent partial-commit protocol in `specs/001-maf-harness-first-class/evidence/harness-lifecycle-feasibility.md`
- [ ] T042 [P] [US3] Add workspace bridge path, traversal, read, write, and explicit failure mapping tests in `src/NexusLabs.Foundry.MicrosoftAgentFramework.Tests/Harness/WorkspaceAgentFileStoreTests.cs`
- [ ] T043 [P] [US3] Add ordinary-write contention tests and verify that upstream operations do not claim unsupported compare-exchange semantics in `src/NexusLabs.Foundry.MicrosoftAgentFramework.Tests/Harness/WorkspaceAgentFileStoreConcurrencyTests.cs`
- [ ] T044 [P] [US3] Add cross-session workspace isolation tests in `src/NexusLabs.Foundry.MicrosoftAgentFramework.Tests/Harness/HarnessWorkspaceIsolationTests.cs`
- [ ] T045 [P] [US1] Add oversized single-tool-result fixture in `src/NexusLabs.Foundry.MicrosoftAgentFramework.Tests/Harness/HarnessEagerOffloadTests.cs`
- [ ] T046 [P] [US1] Add digest, stale-reference, and missing-reference tests in `src/NexusLabs.Foundry.MicrosoftAgentFramework.Tests/Harness/HarnessArtifactReferenceTests.cs`
- [ ] T047 [P] [US1] Add explicit rehydration selection tests in `src/NexusLabs.Foundry.MicrosoftAgentFramework.Tests/Harness/HarnessRehydrationTests.cs`
- [ ] T048 [P] [US3] Add offload cancellation and failure-injection tests for artifact-written/reference-not-committed and reference-committed/history-persistence-failed states in `src/NexusLabs.Foundry.MicrosoftAgentFramework.Tests/Harness/HarnessWorkspaceCancellationTests.cs`

### Internal candidate implementation

- [ ] T049 [US3] Implement the internal `IWorkspace`-backed MAF file-store bridge only for semantics proven by T020 and T041 in `src/NexusLabs.Foundry.MicrosoftAgentFramework/Harness/Workspace/WorkspaceAgentFileStore.cs`
- [ ] T050 [P] [US1] Implement internal digest-backed artifact records in `src/NexusLabs.Foundry.MicrosoftAgentFramework/Harness/Context/HarnessArtifactReference.cs`
- [ ] T051 [US1] Extend or extract the shared tool-result transformation seam so eager offload occurs before ordinary full-payload chat-message creation in `src/NexusLabs.Foundry.MicrosoftAgentFramework/Tools/`
- [ ] T052 [US1] Implement explicit rehydration requests whose resolved payload is injected as a marked recoverable context segment after the eager-offload boundary in `src/NexusLabs.Foundry.MicrosoftAgentFramework/Harness/Context/HarnessArtifactRehydration.cs`
- [ ] T053 [US1] Implement stale, missing, unauthorized, and over-budget reference outcomes in `src/NexusLabs.Foundry.MicrosoftAgentFramework/Harness/Context/HarnessArtifactResolution.cs`
- [ ] T054 [US4] Add offload and rehydration progress events in `src/NexusLabs.Foundry.MicrosoftAgentFramework/Progress/HarnessArtifactProgressEvents.cs`
- [ ] T055 [US4] Add category-level offload and rehydration diagnostics in `src/NexusLabs.Foundry.MicrosoftAgentFramework/Diagnostics/HarnessArtifactDiagnostics.cs`
- [ ] T056 Record workspace authority, unsupported semantics, partial-commit recovery, and Gate G4 disposition in `docs/adr/adr-0006-hybrid-context-and-workspace-authority.md` and `specs/001-maf-harness-first-class/evidence/gate-g4.md`

**Checkpoint G4**: The oversized payload never enters chat history, workspace
semantics remain intact, and no competing authoritative file-memory store is
active.

## Phase 5: Experimental Hybrid Context and Compaction

**Purpose**: Add explicit experimental conversation compaction after eager
offload is proven.

### Tests first

- [ ] T057 [P] [US1] Add labeled preservation-set and contradictory-summary-versus-authoritative-session-state fixtures in `src/NexusLabs.Foundry.MicrosoftAgentFramework.Tests/Harness/HarnessCompactionPreservationTests.cs`
- [ ] T058 [P] [US1] Add valid tool-call/result sequence fixtures in `src/NexusLabs.Foundry.MicrosoftAgentFramework.Tests/Harness/HarnessCompactionSequenceTests.cs`
- [ ] T059 [P] [US1] Add compaction trigger-margin tests in `src/NexusLabs.Foundry.MicrosoftAgentFramework.Tests/Harness/HarnessCompactionMarginTests.cs`
- [ ] T060 [P] [US1] Add bounded recompaction tests proving fallback reduces eligible context or returns irreducible termination rather than forwarding unchanged over-budget history in `src/NexusLabs.Foundry.MicrosoftAgentFramework.Tests/Harness/HarnessRecompactionTests.cs`
- [ ] T061 [P] [US1] Add irreducible-context termination tests in `src/NexusLabs.Foundry.MicrosoftAgentFramework.Tests/Harness/HarnessIrreducibleContextTests.cs`
- [ ] T062 [P] [US1] Add message-injection interaction tests in `src/NexusLabs.Foundry.MicrosoftAgentFramework.Tests/Harness/HarnessMessageInjectionTests.cs`
- [ ] T063 [P] [US1] Add compaction cancellation tests in `src/NexusLabs.Foundry.MicrosoftAgentFramework.Tests/Harness/HarnessCompactionCancellationTests.cs`

### Experimental candidate implementation

- [ ] T064 [US1] Implement the internal hybrid context policy using the lifecycle seams proven by T041 in `src/NexusLabs.Foundry.MicrosoftAgentFramework/Harness/Context/HarnessHybridContextPolicy.cs`
- [ ] T065 [US1] Implement the preservation verifier and deterministic reducing fallback over the selected upstream compaction strategy in `src/NexusLabs.Foundry.MicrosoftAgentFramework/Harness/Context/HarnessCompactionVerifier.cs`
- [ ] T066 [US1] Implement deterministic reduction ordering and irreducible termination in `src/NexusLabs.Foundry.MicrosoftAgentFramework/Harness/Context/HarnessContextAssembler.cs`
- [ ] T067 [US1] Add the explicit experimental hybrid profile in `src/NexusLabs.Foundry.MicrosoftAgentFramework/Harness/HarnessHybridProfile.cs`
- [ ] T068 [US4] Add compaction and context-composition progress events in `src/NexusLabs.Foundry.MicrosoftAgentFramework/Progress/HarnessContextProgressEvents.cs`
- [ ] T069 [US4] Add compaction/offload/rehydration token attribution in `src/NexusLabs.Foundry.MicrosoftAgentFramework/Diagnostics/HarnessContextDiagnostics.cs`
- [ ] T070 [US1] Integrate the selected compaction strategy at the MAF 1.15 extension point proven to observe every intermediate tool-round provider request in `src/NexusLabs.Foundry.MicrosoftAgentFramework/Harness/Context/HarnessCompactionComposition.cs`
- [ ] T071 Update the capability matrix and record Gate G5 strategy, fallback, cancellation, and experimental disposition in `src/NexusLabs.Foundry.MicrosoftAgentFramework/Harness/Capabilities/HarnessCapabilityResolver.cs` and `specs/001-maf-harness-first-class/evidence/gate-g5.md`

**Checkpoint G5**: Preservation, message sequencing, context envelope,
non-retransmission, cancellation, and failure fixtures pass. The profile remains
explicit experimental opt-in.

## Phase 6: Optional Complete Harness Bundle

**Purpose**: Expose the official batteries-included path without affecting
ordinary consumers.

### Project scaffolding

- [ ] T072 [US2] Create the optional production and test project files at `src/NexusLabs.Foundry.MicrosoftAgentFramework.Harness/NexusLabs.Foundry.MicrosoftAgentFramework.Harness.csproj` and `src/NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Tests/NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Tests.csproj`, then add both to `src/NexusLabs.Foundry.slnx`

### Tests first

- [ ] T073 [P] [US2] Add optional-package dependency-closure tests in `src/NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Tests/HarnessPackageIsolationTests.cs`
- [ ] T074 [P] [US2] Add bundle default inspection tests in `src/NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Tests/HarnessBundleDefaultsTests.cs`
- [ ] T075 [P] [US1] Add bundle generated-tool ingress tests in `src/NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Tests/HarnessBundleGeneratedToolsTests.cs`
- [ ] T076 [P] [US4] Add bundle loop and telemetry deduplication tests in `src/NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Tests/HarnessBundleTelemetryTests.cs`

### Optional package

- [ ] T077 [US2] Add the optional bundle construction path in `src/NexusLabs.Foundry.MicrosoftAgentFramework.Harness/FoundryHarnessAgentFactory.cs`
- [ ] T078 [US2] Map generated tools and explicit Foundry options into upstream Harness options in `src/NexusLabs.Foundry.MicrosoftAgentFramework.Harness/FoundryHarnessAgentConfiguration.cs`
- [ ] T079 [US4] Compose bundle telemetry and progress in `src/NexusLabs.Foundry.MicrosoftAgentFramework.Harness/FoundryHarnessTelemetryComposition.cs`
- [ ] T080 [US2] Record the optional bundle architecture decision, API-candidate review, and Gate G6 dependency/default disposition in `docs/adr/adr-0007-optional-harness-bundle.md` and `specs/001-maf-harness-first-class/evidence/gate-g6.md`

**Checkpoint G6**: Consumers without the optional package have no bundle
dependency or behavior; bundle users see effective defaults and no duplicate
loop or telemetry.

## Phase 7: AOT, Analyzer, Testing, and Documentation Hardening

**Purpose**: Promote only proven profiles and add static guidance where useful.

- [ ] T081 [P] [US4] Create the minimum generated-tool AOT example project in `src/Examples/AgentFramework/AotHarnessApp/AotHarnessApp.csproj`
- [ ] T082 [P] [US4] Define the Harness-specific lifecycle additions over `IAgentScenario` in `src/NexusLabs.Foundry.MicrosoftAgentFramework.Testing/IHarnessScenario.cs`
- [ ] T083 [US4] Add the deterministic Harness scenario runner as an extension of existing scenario infrastructure in `src/NexusLabs.Foundry.MicrosoftAgentFramework.Testing/HarnessScenarioRunner.cs`
- [ ] T084 [US4] Implement the scripted AOT Harness scenario in `src/Examples/AgentFramework/AotHarnessApp/Program.cs`
- [ ] T085 [P] Add source-generated tool parity tests in `src/NexusLabs.Foundry.MicrosoftAgentFramework.GeneratedWrapper.Tests/HarnessGeneratedWrapperParityTests.cs`
- [ ] T086 [P] Add AOT publish and binary-execution automation for `AotHarnessApp` in `.github/workflows/ci.yml`
- [ ] T087 Produce an analyzer feasibility matrix proving whether any Harness misuse is statically decidable and non-redundant with runtime guards or IL/AOT tooling in `specs/001-maf-harness-first-class/evidence/analyzer-feasibility.md`
- [ ] T088 [P] If T087 approves a rule, add failing and no-diagnostic analyzer tests first in `src/NexusLabs.Foundry.MicrosoftAgentFramework.Analyzers.Tests/HarnessAnalyzerTests.cs`
- [ ] T089 If T087 approves a rule, implement only the approved analyzer in `src/NexusLabs.Foundry.MicrosoftAgentFramework.Analyzers/`
- [ ] T090 If T089 is implemented, allocate the approved `FDRY` ID and update `src/NexusLabs.Foundry.MicrosoftAgentFramework.Analyzers/MafDiagnosticIds.cs` and `src/NexusLabs.Foundry.MicrosoftAgentFramework.Analyzers/AnalyzerReleases.Unshipped.md`
- [ ] T091 [P] Add the first-class Harness guide in `docs/maf-harness.md`
- [ ] T092 Update context-strategy guidance in `docs/iterative-agent-loop.md`
- [ ] T093 [P] Update package and integration maps in `README.md` and `docs/ai-integrations.md`
- [ ] T094 [P] Add the non-Azure selected-provider example and record Gate G7 profile/API disposition in `src/Examples/AgentFramework/HarnessHybridApp/` and `specs/001-maf-harness-first-class/evidence/gate-g7.md`

**Checkpoint G7**: Supported profiles have complete deterministic evidence,
minimum AOT proof, and reviewed documentation. No speculative analyzer ships.

## Phase 8: Hosted Evaluation and Retention Decisions

**Purpose**: Produce comparative evidence before recommending defaults or
removing existing behavior.

### Pre-registration and tests first

- [ ] T095 [US4] Publish the pre-registered hosted analysis protocol in `artifacts/eval/case-sets/harness-001/v1.0/analysis-plan.md`, including trial count rationale, retry-versus-trial semantics, exclusions, per-metric paired method, uncertainty, unknown-sample treatment, and non-gating status
- [ ] T096 [P] [US4] Add case-set manifest schema and round-trip tests in `src/NexusLabs.Foundry.Evaluation.Tests/Harness/HarnessCaseSetManifestTests.cs`
- [ ] T097 [P] [US4] Add diagnostics-schema parity tests across iterative, plain Harness, and hybrid fixtures in `src/NexusLabs.Foundry.Evaluation.Tests/Harness/HarnessDiagnosticsParityTests.cs`
- [ ] T098 [P] [US4] Add context-safety and compaction-validity evaluator tests in `src/NexusLabs.Foundry.Evaluation.Tests/Harness/HarnessContextEvaluatorTests.cs`
- [ ] T099 [P] [US4] Add artifact-reuse and rehydration evaluator tests in `src/NexusLabs.Foundry.Evaluation.Tests/Harness/HarnessArtifactEvaluatorTests.cs`
- [ ] T100 [P] [US4] Add telemetry, ordering, identity, cancellation, and session-continuity evaluator tests in `src/NexusLabs.Foundry.Evaluation.Tests/Harness/HarnessRuntimeEvaluatorTests.cs`
- [ ] T101 [P] [US4] Add cost-attribution and trajectory evaluator tests in `src/NexusLabs.Foundry.Evaluation.Tests/Harness/HarnessCostTrajectoryEvaluatorTests.cs`
- [ ] T102 [P] [US4] Add known-answer paired binary and continuous comparison tests in `src/NexusLabs.Foundry.Evaluation.Tests/Experiments/ExperimentPairedComparisonEvidenceTests.cs`
- [ ] T103 [P] [US4] Author versioned advisory judge rubrics in `artifacts/eval/case-sets/harness-001/v1.0/judges/`
- [ ] T104 [P] [US4] Curate the held-out human-labeled judge calibration subset in `artifacts/eval/case-sets/harness-001/v1.0/judges/calibration/`
- [ ] T105 [P] [US4] Add position, verbosity, style, and deterministic-disagreement judge fixtures in `src/NexusLabs.Foundry.Evaluation.Tests/Harness/Judging/HarnessJudgeCalibrationTests.cs`

### Evaluation implementation

- [ ] T106 [US4] Define the versioned case-set manifest and loader, reusing `IExperimentCaseSource`, in `artifacts/eval/case-sets/harness-001/v1.0/manifest.json` and `src/NexusLabs.Foundry.Evaluation/Harness/HarnessManifestCaseSource.cs`
- [ ] T107 [US4] Author deterministic completion and dimension references in `artifacts/eval/case-sets/harness-001/v1.0/cases/`
- [ ] T108 [P] [US4] Implement context-safety and compaction-validity per-item evaluators in `src/NexusLabs.Foundry.Evaluation/Harness/`
- [ ] T109 [P] [US4] Implement artifact-reuse and rehydration per-item evaluators in `src/NexusLabs.Foundry.Evaluation/Harness/`
- [ ] T110 [P] [US4] Implement telemetry, ordering, identity, cancellation, and session-continuity evaluators with explicit per-item or run-level contracts in `src/NexusLabs.Foundry.Evaluation/Harness/`
- [ ] T111 [P] [US4] Implement cost-attribution and trajectory evaluators by extending existing Foundry evaluator evidence in `src/NexusLabs.Foundry.Evaluation/Harness/`
- [ ] T112 [US4] Implement reusable paired binary and continuous evidence primitives in `src/NexusLabs.Foundry.Evaluation/Experiments/ExperimentPairedComparisonEvidence.cs`
- [ ] T113 [US4] Add the three-arm case source and `ExperimentDefinition` factories in `src/NexusLabs.Foundry.Evaluation/Harness/HarnessComparisonExperiment.cs`
- [ ] T114 [US4] Add the Harness-specific reporter under `src/NexusLabs.Foundry.Evaluation/Harness/HarnessComparisonReporter.cs`, using `ExperimentPairedComparisonEvidence` and `ExperimentJsonArtifactWriter`
- [ ] T115 [US4] Add advisory judge evaluators in `src/NexusLabs.Foundry.Evaluation/Harness/Judging/` using the versioned rubrics and calibration evidence
- [ ] T116 [US4] Add deterministic-versus-judge disagreement reporting to `src/NexusLabs.Foundry.Evaluation/Harness/HarnessComparisonReporter.cs`

### Hosted execution and human decision

- [ ] T117 [US4] Add `.github/workflows/harness-evaluation.yml` with only `workflow_dispatch` and `schedule` triggers, explicit wall-clock/request/cost caps, capture/replay artifacts, and non-failing stochastic summaries
- [ ] T118 [US4] Verify the hosted workflow is not a required branch-protection status and record the result in `specs/001-maf-harness-first-class/evidence/hosted-eval-gate.md`
- [ ] T119 [US4] Execute the hosted paired comparison, ingest immutable inputs and outputs, and retain the decision artifact bundle under `artifacts/eval/reports/harness-001/`
- [ ] T120 [US4] Publish the comparison artifact conforming to `contracts/evaluation-evidence.md`, including deterministic dimensions, paired uncertainty, judge disagreement, diagnostics parity, and a human-review signature block
- [ ] T121 Record the human-reviewed overlap retention/removal decisions and supersede affected ADR guidance where necessary in `specs/001-maf-harness-first-class/evidence/retention-decisions.md` and `docs/adr/`

**Checkpoint G8**: Every recommendation or removal has deterministic anchors,
paired evidence, uncertainty, migration guidance, and human review.

### Phase 8 execution order

```text
T095
  -> T096-T105 in parallel
      -> T106-T112
          -> T113
              -> T114-T116
                  -> T117
                      -> T118
                          -> T119
                              -> T120
                                  -> T121
```

## Phase 9: Final Integration and Release Review

- [ ] T122 Add shell-specific package-boundary and manual-composition tests in `src/NexusLabs.Foundry.MicrosoftAgentFramework.Tests/Harness/HarnessShellCompositionTests.cs` and document the absence of a Harness options shell property in `docs/maf-harness.md`
- [ ] T123 Re-run the Spec Kit cross-artifact analysis against the implemented profile and resolve all critical findings in `specs/001-maf-harness-first-class/reviews/pre-release-analysis.md`
- [ ] T124 Perform a public API review, verify XML documentation for every promoted public member, and record promoted versus internal candidates in `specs/001-maf-harness-first-class/evidence/api-review.md`
- [ ] T125 Verify every temporary duplication entry has a release-bound disposition in `specs/001-maf-harness-first-class/evidence/duplication-ledger.md`
- [ ] T126 Prepare release notes and migration guidance in `CHANGELOG.md` and `docs/maf-harness.md`

## Phase 10: Post-Implementation Reconciliation and Specification Cleanup

- [ ] T127 Compare delivered code, packages, public APIs, capability profiles, and retained/deleted overlaps against `specs/001-maf-harness-first-class/plan.md` in `specs/001-maf-harness-first-class/evidence/implementation-vs-plan.md`
- [ ] T128 Audit `README.md`, `docs/maf-harness.md`, `docs/iterative-agent-loop.md`, API documentation, examples, and release notes against delivered behavior in `specs/001-maf-harness-first-class/evidence/documentation-vs-delivery.md`
- [ ] T129 File every accepted non-critical deviation or opportunity as a post-MVP GitHub follow-up linked to the root Harness implementation issue and record links in `specs/001-maf-harness-first-class/evidence/post-mvp-follow-ups.md`
- [ ] T130 Synthesize T127-T129 into a human-reviewed per-artifact retain/archive/delete decision in `specs/001-maf-harness-first-class/evidence/specification-retention-decision.md`
- [ ] T131 Execute the approved T130 retention decision in a separate stage-review cleanup PR, preserving ADRs, changelog, migration guidance, and GitHub traceability while removing or archiving no-longer-useful `specs/001-maf-harness-first-class/` artifacts

## Dependencies and Execution Order

### Group dependencies

```text
G1 Package compatibility
  -> G2 Composition foundation
      -> G3 Stable provider slices
      -> G4 Workspace bridge
      -> G6 Optional bundle

G4 Workspace bridge
  -> G5 Experimental hybrid

G2 begins G7 test/AOT scaffolding
G3/G4/G5/G6 complete profile-specific G7 evidence

G3 + G4 + G5 + applicable G7
  -> G8 Hosted comparison
      -> G9 Retention/API/release review
          -> G10 Reconciliation and cleanup
```

### User story dependencies

- **US1 Long-lived hybrid agent**: requires G2 and G4; compaction portion also
  requires G5.
- **US2 Incremental adoption**: begins with G1/G2; stable slices are G3 and
  optional bundle is G6.
- **US3 Safe workspace/session/approval**: identity starts in G2, approval in
  G3, workspace/session hardening in G4.
- **US4 Observability/evaluation**: telemetry starts in G2; deterministic
  evidence grows through G3-G7; hosted comparison is G8.

### Parallel opportunities

After G2:

- G3 stable provider slices can run independently where files do not overlap.
- G4 workspace bridge can run in parallel with G3.
- G6 bundle tests can begin after selected-provider composition is understood.
- AOT, telemetry, identity threat modeling, and case-set design can proceed in
  parallel.
- Evaluator implementations in T108-T111 can run in parallel after diagnostics
  contracts stabilize.

## MVP Strategy

The first increment is an internal technical MVP, not a published customer
feature:

1. G1 compatibility proof
2. G2 internal composition foundation
3. One G3 stable provider slice with generated tools and deterministic telemetry

The hybrid context profile is a later experimental increment after workspace
offload and preservation contracts are proven.

## Task Format Validation

- 131 tasks use checkbox and sequential task IDs.
- Story tasks include `[US1]` through `[US4]`.
- Parallel markers identify different-file work after dependencies.
- Every implementation task names a repository path.
- Tests precede implementation within each capability group.
- Broad hosted evaluation appears only in Phase 8.
