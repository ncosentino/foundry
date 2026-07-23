# Implementation Plan: First-Class Microsoft Agent Framework Harness Support

**Branch**: `001-maf-harness-first-class` | **Date**: 2026-07-22 | **Spec**: [spec.md](spec.md)

**Input**: Reviewed feature specification from
`specs/001-maf-harness-first-class/spec.md`

## Summary

Introduce Microsoft Agent Framework Harness as a first-class, opt-in Foundry MAF
offering through an evidence-gated migration.

The plan uses two consumption lanes:

1. Selected stable MAF core providers composed through the existing Foundry MAF
   integration.
2. The complete opinionated `HarnessAgent` bundle isolated in an optional
   Foundry package.

The long-running target is a hybrid context model: recent conversation remains
available and may be compacted, while oversized tool results and bulk artifacts
are eagerly offloaded to Foundry `IWorkspace` and selectively rehydrated through
digest-backed references.

No dependency or runtime API change is authorized by this planning artifact.
The first implementation group is a compatibility and behavior spike against
the latest verified coherent stable line:

- MAF core, Workflows, Generators, and Harness 1.15.0
- MEAI and MEAI Evaluation 10.6.0
- DevUI and Hosting 1.15.0 preview builds assessed as separate satellite gates

## Technical Context

**Language/Version**: C# 14 / .NET 10

**Primary Dependencies**:

- Current: Microsoft Agent Framework 1.3.0, MEAI 10.5.0
- Candidate: Microsoft Agent Framework 1.15.0, Harness 1.15.0,
  MEAI/Evaluation 10.6.0
- OpenTelemetry 1.15.3
- Existing Foundry source-generation, analyzer, testing, evaluation, and
  workspace packages

**Storage**:

- Foundry `IWorkspace` remains authoritative for hybrid bulk artifacts.
- MAF `AgentSession` and `ChatHistoryProvider` own conversation/session
  continuity.
- Cross-process durability remains an explicit provider or serialization
  decision; default in-memory history is not durable.

**Testing**:

- xUnit v3 and existing Foundry deterministic testing projects
- Source-generator and analyzer test harnesses
- NativeAOT publish/run probe
- Hosted ExperimentRunner comparison, not run locally

**Target Platform**:

- Primary: .NET 10 libraries and applications
- Any host capable of running MAF and an `IChatClient`
- Azure hosting is optional, not required

**Project Type**: Multi-package .NET framework integration

**Performance Goals**:

- No request exceeds its configured effective context envelope.
- Stable offloaded artifact bodies are not repeatedly retransmitted.
- Conversation compaction does not grow unbounded or corrupt tool sequences.
- Hybrid decisions and token attribution are observable.

**Constraints**:

- No provider-specific dependency in neutral Foundry code.
- No public API promotion before conformance evidence.
- No duplicate function-invocation loop or telemetry recorder.
- No broad evaluation workload on developer machines.
- Experimental upstream capabilities remain explicit opt-in.
- No removal of `IWorkspace` or `IIterativeAgentLoop` in the initial delivery.

**Scale/Scope**:

- Existing Foundry package family, generators, analyzers, workflows, evaluation,
  testing, DevUI, examples, and AOT path
- Three execution modes in later comparison: current iterative, plain Harness
  compaction, hybrid Harness plus workspace

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after design.*

| Principle | Pre-design check | Post-design check |
|---|---|---|
| Neutral Core, Replaceable Integrations | PASS: Harness remains within MAF integration | PASS: bundle isolated; `IWorkspace` remains authoritative |
| Evidence-Gated API Evolution | PASS: no implementation authorized | PASS: internal candidates, explicit gates, removal ledger |
| Hybrid Context and Workspace State | PASS: hybrid target is the feature purpose | PASS: eager offload, compaction, references, rehydration contracts |
| Deterministic, Testable, Observable | PASS: deterministic fixtures required | PASS: diagnostics, ordering, cancellation, AOT, and hosted evidence defined |
| Explicit Contracts and API Discipline | PASS: no final API names in spec | PASS: behavioral contracts separate data from services |

Architecture constraints also pass:

- MAF remains provider integration.
- Needlr boundaries are unchanged.
- AOT and trimming receive a minimum supported profile.
- Experimental dependencies are isolated.
- Trust boundaries and ADR requirements are explicit.

## Architecture Decisions

### 1. Candidate dependency graph

The compatibility spike evaluates one coherent graph:

```text
Microsoft.Agents.AI                1.15.0
Microsoft.Agents.AI.Workflows      1.15.0
Microsoft.Agents.AI.Workflows.Generators 1.15.0
Microsoft.Agents.AI.Harness        1.15.0
Microsoft.Extensions.AI                      10.6.0
Microsoft.Extensions.AI.Abstractions         10.6.0
Microsoft.Extensions.AI.OpenAI               10.6.0
Microsoft.Extensions.AI.Evaluation           10.6.0
Microsoft.Extensions.AI.Evaluation.Quality   10.6.0
Microsoft.Extensions.AI.Evaluation.Reporting 10.6.0
Microsoft.Agents.AI.DevUI          1.15.0-preview.260722.1 satellite
Microsoft.Agents.AI.Hosting        1.15.0-preview.260722.1 satellite
Microsoft.Agents.AI.Hosting.OpenAI 1.15.0-alpha.260722.1 satellite
```

Package availability is verified. Runtime, build, and satellite compatibility
remain evidence-gated.

### 2. Consumption lanes

#### Lane A: selected core providers

The existing `NexusLabs.Foundry.MicrosoftAgentFramework` integration may compose
individually selected stable providers after the compatibility gate:

- per-service-call history;
- todo;
- agent mode;
- tool approval;
- skills;
- provider-dependent web search when supported.

The lane does not reference the complete `.Harness` bundle and does not enable
unselected upstream defaults.

Lane A capability, context, workspace, and session integration lives in the
base package's `Harness/` subtree and references only `Microsoft.Agents.AI`
core. Only Lane B bundle code lives in the optional `.Harness` package.

#### Lane B: complete Harness bundle

Candidate isolated package:

```text
NexusLabs.Foundry.MicrosoftAgentFramework.Harness
```

This package may reference `Microsoft.Agents.AI.Harness` and provide the
batteries-included `HarnessAgent` path. It remains optional and reports any
upstream default that cannot be disabled.

### 3. Hybrid context boundary

Foundry owns the hybrid semantic policy:

```text
tool result
  -> eager size/policy decision
  -> full payload to IWorkspace when offloaded
  -> digest-backed reference enters chat history

conversation working set
  -> preserve pinned/authoritative content
  -> evict recoverable rehydrated bodies
  -> compact eligible conversation
  -> drop only explicit optional context
  -> structured irreducible-context termination

artifact reference
  -> explicit tool request or deterministic policy
  -> identity/digest/budget validation
  -> selective rehydration
```

Upstream compaction remains experimental and is consumed only after the selected
strategy passes the preservation contract.

Every hybrid profile predeclares its token estimator, provider/model context
limit, reserved output allowance, compaction execution safety margin, and
bounded-summary overhead. The effective input envelope is the context limit
minus the reserved output and safety margin.

### 4. Workspace authority

The hybrid profile uses one authoritative bulk store:

```text
Foundry IWorkspace
  -> MAF AgentFileStore bridge
  -> FileMemoryProvider / FileAccessProvider when enabled
```

The upstream default timestamp/GUID file-memory location is disabled or replaced
for the hybrid profile.

The bridge does not invent semantics that `IWorkspace` lacks. In particular,
ordinary upstream writes cannot claim compare-exchange without an expected
version, and synchronous workspace operations can check cancellation before
entry but cannot guarantee mid-call interruption. Unsupported semantics are
reported through capability evidence and may stop the profile gate.

### 5. Session and identity

- A host-issued trusted execution binding owns user, orchestration, session, and
  workspace identity.
- Missing or mismatched identity fails closed.
- In-memory history is reported as non-durable.
- Restored standing approvals remain untrusted until host validation.
- No durable wire format is promised in the initial delivery.

### 6. Middleware and decorator ordering

The implementation phase must prove one effective composition across two
distinct extension surfaces:

1. `AIAgent` decorators and `AIContextProvider` lifecycle.
2. `IChatClient` middleware and the function-invocation loop.

The plan does not assert a single linear stack before tracing MAF 1.15. The
ordering fixture must prove:

- the selected compaction provider observes every provider request made during
  intermediate tool rounds;
- exactly one function-invocation loop is active;
- eager offload transforms an oversized result before an ordinary full-payload
  tool-result message is created or persisted;
- rehydrated payloads enter the current context as marked recoverable segments
  after the eager-offload boundary and are not immediately re-offloaded;
- approval, history persistence, diagnostics, resilience, budgets, progress,
  and outer looping occupy documented surfaces;
- one OpenTelemetry layer is explicitly suppressed when composition would
  double-record spans.

### 7. Source-generated tool ingress

Existing generated `AIFunction` instances enter either lane through the agent's
chat options. The minimum AOT profile:

- uses `GeneratedAIFunctionProvider`;
- does not scan assemblies;
- treats trim/AOT warnings as errors;
- excludes unsupported dynamic skill/script and reflection-only profiles;
- publishes and runs an end-to-end scripted Harness scenario.

No new generated capability matrix is required initially. A runtime versioned
matrix is sufficient unless compile-time evidence proves a generator adds value.

## Project Structure

### Documentation

```text
specs/001-maf-harness-first-class/
├── spec.md
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── harness-profile.md
│   ├── hybrid-context.md
│   ├── workspace-file-store.md
│   ├── diagnostics-events.md
│   └── evaluation-evidence.md
├── checklists/
│   └── requirements.md
├── reviews/
│   ├── spec-review.md
│   └── plan-review.md
└── tasks.md
```

### Candidate source changes after gates

```text
src/
├── Directory.Packages.props
├── NexusLabs.Foundry.MicrosoftAgentFramework/           # Lane A: MAF core only
│   ├── AgentFrameworkBuilder*.cs
│   ├── AgentFactory.cs
│   ├── Harness/
│   │   ├── Capabilities/
│   │   ├── Providers/
│   │   ├── Context/
│   │   └── Workspace/
│   ├── Context/
│   ├── Diagnostics/
│   ├── Progress/
│   └── Workspace/
├── NexusLabs.Foundry.MicrosoftAgentFramework.Harness/   # Lane B bundle only
│   ├── FoundryHarnessAgentFactory.cs
│   ├── FoundryHarnessAgentConfiguration.cs
│   └── FoundryHarnessTelemetryComposition.cs
├── NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Tests/  # candidate new project
├── NexusLabs.Foundry.MicrosoftAgentFramework.GeneratedWrapper.Tests/
├── NexusLabs.Foundry.MicrosoftAgentFramework.Analyzers/
├── NexusLabs.Foundry.MicrosoftAgentFramework.Analyzers.Tests/
├── NexusLabs.Foundry.MicrosoftAgentFramework.DevUI/
└── Examples/AgentFramework/
    └── HarnessHybridApp/                                     # after runtime gates
```

Analyzer and generator additions are conditional on demonstrated static value.
The plan does not pre-authorize new diagnostics or generated outputs.

## Logical Delivery Groups and Gates

### Group 0: Planning closure

**Purpose**: Complete specification, plan, contracts, reviews, tasks, and
consistency analysis.

**Deliverables**:

- reviewed Spec Kit artifacts;
- current NuGet/package evidence;
- versioned capability matrix;
- middleware ordering candidate;
- overlap/temporary-duplication ledger;
- deterministic fixture catalog;
- hosted evaluation protocol.

**Gate G0**: No unresolved critical review finding, placeholder, dependency
ambiguity, or constitution conflict.

**Status**: Completed by the reviewed specification, this plan, `tasks.md`,
`reviews/spec-review.md`, `reviews/plan-review.md`, and the final Spec Kit
consistency analysis. No implementation task may start until that record is
complete.

**Release**: None.

### Group 1: Package-graph and baseline compatibility proof

**Purpose**: Prove the candidate package graph before exposing capability.

**Deliverables**:

- isolated candidate package diff plus a disposable Harness compatibility probe
  that directly references Harness, MAF core, Workflows, Generators, MEAI, and
  Evaluation;
- restore and compile report;
- public API and analyzer diagnostic delta;
- targeted baseline results for agents, workflows, generators, analyzers,
  evaluation, diagnostics, testing, and AOT;
- separate DevUI/Hosting/Hosting.OpenAI satellite status;
- telemetry baseline.
- traced Harness lifecycle evidence identifying actual tool-result,
  history-persistence, compaction, and intermediate tool-round seams.

**Gate G1**:

- coherent package graph;
- no unexplained targeted regression;
- minimum generated-tool Harness AOT probe publishes and executes;
- pre-history tool-result interception and per-tool-round compaction seams are
  proven or explicitly found unavailable;
- satellite failures are passed, failed, or explicitly deferred.

**Stop condition**: Defer Harness if no coherent core graph or AOT path exists.

**Release**: Internal candidate only; no new public Harness API.

### Group 2: Composition foundation

**Purpose**: Establish internal provider-specific seams without public API
commitment.

**Deliverables**:

- internal Harness profile and capability evidence;
- generated-tool ingress;
- trusted execution/session binding;
- deterministic middleware-order fixture;
- one-loop guard;
- telemetry ownership decision;
- stable-provider selected lane prototype.

**Dependencies**: G1.

**Gate G2**:

- non-Azure deterministic end-to-end scenario;
- one loop and no duplicate telemetry;
- default-on capability state inspectable;
- ordinary non-Harness agents show no behavior change.
- an API-candidate review records every new surface as internal, rejected, or
  approved for an explicitly experimental prerelease.

**Release**: Internal only until the API-candidate gate passes.

### Group 3: Stable capability slices

**Purpose**: Deliver stable providers incrementally through the selected lane.

**Candidate slices**:

1. Session continuity and durability reporting
2. Todo and agent modes
3. Tool approval and structured approval events
4. Skills with explicit trust-boundary documentation
5. Provider-dependent web search capability evidence

Each slice has an independent deterministic scenario and may proceed in parallel
after G2, subject to shared files and middleware ordering.

**Dependencies**: G2.

**Gate G3 per slice**:

- capability-specific contract passes;
- effective state is inspectable;
- diagnostics/progress evidence exists;
- no unselected capability activates.
- a slice-specific gate record identifies evidence, effective capability state,
  public API status, and the next permitted group.

**Release**: Additive prerelease slices; no removals.

### Group 4: Workspace bridge and eager offload

**Purpose**: Establish Foundry-authoritative artifact storage before compaction.

**Deliverables**:

- a feasibility and trust-boundary report mapping actual `AgentFileStore`
  operations to `IWorkspace`, trusted identity, cancellation, and unsupported
  semantics;
- a lifecycle feasibility report and idempotent partial-commit protocol for
  artifact persistence plus conversational reference commit;
- internal `IWorkspace` to `AgentFileStore` bridge;
- trusted ownership mapping;
- eager tool-result offload boundary;
- digest-backed artifact references;
- stale/missing/unauthorized evidence;
- deterministic offload, contention, and cancellation fixtures.

**Dependencies**: G2. May run in parallel with stable capability slices.

**Gate G4**:

- path canonicalization and traversal rejection;
- compare-exchange behavior preserved where required;
- no competing authoritative file-memory store;
- oversized result never enters chat history in the fixture;
- no cross-session leakage.
- partial persistence/reference failures recover idempotently.

**Release**: Internal or experimental prerelease; no public neutral abstraction.

### Group 5: Experimental hybrid context

**Purpose**: Combine conversation retention, compaction, workspace references,
and rehydration.

**Deliverables**:

- explicit hybrid profile;
- selected upstream compaction strategy plus Foundry preservation verification;
- compaction trigger margin;
- bounded recompaction and deterministic fallback;
- valid tool-call/result group handling;
- deterministic context reduction order;
- explicit rehydration decisions;
- irreducible-context termination;
- context composition and token attribution diagnostics.
- proof that rehydrated content bypasses eager re-offload for its active request.

**Dependencies**: G4 and the composition foundation. Stable provider slices are
not all required.

**Gate G5**:

- preservation fixtures pass;
- no orphaned tool sequence;
- context remains within envelope;
- stable offloaded bodies are not retransmitted;
- compaction remains explicitly experimental;
- cancellation and failure taxonomy passes.
- Gate G5 evidence records the compaction strategy, fallback, experimental
  status, and public API decision.

**Release**: Experimental opt-in only; never default.

### Group 6: Optional complete Harness bundle

**Purpose**: Offer upstream batteries-included construction without changing
the ordinary Foundry path.

**Deliverables**:

- optional package or recipe;
- effective-default inspection;
- generated-tool integration;
- telemetry/loop composition;
- explicit limitations for default message injection, file memory, web search,
  approvals, skills, and OpenTelemetry;
- bundle-versus-selected-lane conformance report.

**Dependencies**: G2 and evidence from at least one stable provider slice.

**Gate G6**:

- non-adopters have no new dependency or runtime behavior;
- bundle path has no duplicate loop or telemetry;
- unsupported defaults are visible;
- experimental features remain opt-in.
- a bundle-specific API-candidate review records all consumer-facing surfaces.

**Release**: Separate optional package; satellite integrations may be deferred.

### Group 7: AOT, diagnostics, testing, and documentation hardening

**Purpose**: Promote supported profiles only after framework-quality evidence.

**Deliverables**:

- minimum AOT Harness example and publish/run test;
- deterministic scenario harness;
- progress and diagnostics events;
- telemetry parity tests;
- capability matrix publication;
- analyzer diagnostics only for statically provable invalid combinations;
- docs comparing plain Foundry, selected providers, bundle, iterative, hybrid,
  workflows, DevUI, Hosting, and sample console.

**Dependencies**: Starts after G2; profile-specific completion depends on G3-G6.

**Gate G7**:

- all deterministic success criteria applicable to the profile are complete;
  hosted-comparison criteria SC-010 and SC-015 remain G8 evidence;
- no reflection fallback in minimum AOT profile;
- review confirms no speculative analyzer or public API.
- analyzer feasibility evidence confirms any proposed rule is statically
  provable and non-redundant with runtime or IL/AOT tooling.

### Group 8: Hosted comparative evidence and retention decisions

**Purpose**: Compare execution modes before removal or default recommendations.

**Deliverables**:

- versioned case set with development/hosted separation;
- current iterative, plain Harness, and hybrid arms;
- pinned fair-comparison controls;
- deterministic anchors for every decision dimension;
- paired uncertainty reporting;
- judge calibration artifacts;
- decision artifact bundle;
- overlap retention/removal recommendation.

**Dependencies**: G3, G4, G5, and applicable G7 evidence.

**Gate G8**:

- stochastic evidence is not the sole automated gate;
- reports include operational definitions and uncertainty;
- every proposed removal has workload-specific parity evidence and migration
  guidance.

**Release**: Later retention/default decision; never part of initial Harness
enablement.

### Group 9: Final integration and release review

**Purpose**: Revalidate the complete artifact and implementation set before any
public promotion or release.

**Deliverables**:

- final Spec Kit consistency analysis;
- shell package/manual-composition requirement evidence;
- public API and XML-documentation review;
- completed duplication ledger;
- release notes and migration guidance.

**Dependencies**: Applicable G3-G8 gates for the profile being released.

**Gate G9**:

- no critical cross-artifact inconsistency;
- every public member is intentionally promoted and documented;
- every temporary duplicate has a retention or deletion disposition;
- migration and release guidance matches the supported capability matrix.

**Release**: Only artifacts and profiles passing G9 may be published.

### Group 10: Post-implementation reconciliation and Spec Kit cleanup

**Purpose**: Compare delivered behavior with the original plan, verify
documentation accuracy, separate follow-up scope, and remove feature-specific
planning artifacts when they no longer provide operational value.

**Deliverables**:

- implementation-versus-plan variance report;
- delivered-documentation parity audit;
- critical deviations fixed or explicitly accepted;
- non-critical deviations and opportunities filed as post-MVP follow-up issues;
- human-reviewed per-artifact retention decision for the feature spec, reviews,
  evidence, generated agent context, and active feature pointer;
- separate cleanup PR implementing the approved retention decision.

**Dependencies**: G9 release evidence and delivered documentation.

**Gate G10**:

- delivered behavior and public APIs are mapped back to the plan;
- documentation describes the delivered system rather than the intended system;
- unresolved non-critical scope is tracked outside the MVP;
- ADRs, changelog, and durable migration guidance remain available;
- feature-specific Spec Kit artifacts are removed or archived only after their
  approved cleanup decision.

**Release**: Separate cleanup PR after the implementation and documentation
reviews. Cleanup does not rewrite accepted ADR or release history.

## Dependency Graph

```text
G0 Planning closure
  -> G1 Package graph compatibility
      -> G2 Composition foundation
          -> G3 Stable capability slices -----------+
          -> G4 Workspace bridge and eager offload -+-> G7 Hardening
          -> G6 Optional bundle --------------------+
              G4 -> G5 Experimental hybrid ---------+

G3 + G4 + G5 + applicable G7
  -> G8 Hosted comparison and retention decisions
      -> G9 Final integration and release review
          -> G10 Post-implementation reconciliation and cleanup
```

Parallel opportunities after G2:

- stable capability slices;
- workspace bridge;
- deterministic test infrastructure;
- identity and approval threat model;
- telemetry fixture;
- AOT generated-tool probe;
- evaluation case-set design.

## Temporary Duplication and Retention Ledger

| ID | Concrete overlap | Owner | Authority | Start group | Review group/release | Parity evidence | Deletion or retention trigger |
|---|---|---|---|---|---|---|---|
| DUP-001 | Foundry `IWorkspace` and upstream file stores | Workspace bridge owner | Foundry workspace | G4 | G8 | Path, isolation, contention, artifact reuse | Retain `IWorkspace`; remove only redundant bridge code, not workspace |
| DUP-002 | `IIterativeAgentLoop` and Harness/LoopAgent | Agent runtime owner | Profile-specific | G2 | G8 | Completion, continuity, cost, trajectory by workload | Retain until every current workload has migration evidence; no initial deletion |
| DUP-003 | Foundry diagnostics and upstream OTel | Diagnostics owner | One declared telemetry owner | G2 | G7 | Deterministic span/event parity | Delete temporary suppression bridge only after one permanent composition path is approved |
| DUP-004 | Foundry function loop and Harness invocation | Agent composition owner | One selected loop per profile | G2 | G2 | Ordering and one-loop fixture | Dual-loop combinations remain prohibited |
| DUP-005 | Structured state and conversation summary | Session-state owner | Structured state | G2 | Permanent | Conflict fixtures | Permanent invariant; no deletion |
| DUP-006 | Foundry preservation policy and upstream compaction | Hybrid-context owner | Foundry preservation contract | G5 | G8 | Preservation and bounded-context fixtures | Remove fallback only if upstream strategy satisfies all required profiles |
| DUP-007 | Foundry approval events and upstream approval execution | Approval owner | Upstream execution, host validation | G3 | G7 | Restore, identity, and transition fixtures | Retain Foundry events while they provide diagnostics/host semantics |
| DUP-008 | Selected provider lane and complete bundle | MAF integration owner | Separate lanes | G2/G6 | G8 | Dependency, behavior, and consumer evidence | Retain both unless one lane has no distinct supported scenario |

## Evaluation and Validation Strategy

### Deterministic local layer

Credential-free fixtures:

- dependency and capability matrix
- generated-tool Harness run
- eager oversized-result offload
- compaction preservation and MEAI sequence validity
- selective rehydration and stale references
- session isolation and restored approval validation
- middleware order and telemetry deduplication
- cancellation matrix
- NativeAOT publish/run

These are correctness gates.

### Hosted stochastic layer

Three paired execution modes:

- current Foundry iterative;
- plain Harness with explicit compaction;
- hybrid Harness plus workspace.

Before hosted execution, a versioned analysis protocol declares trial count,
retry-versus-trial semantics, exclusions, aggregation, paired comparison,
uncertainty, cost/time caps, and advisory judge use.

Fairness controls are classified rather than assumed identical:

- **Identical where supported**: case and case-set version, provider/model,
  sampling parameters, controlling instructions, tools and tool versions,
  timeout, cancellation policy, initial workspace snapshot, package/capability
  versions.
- **Operationally equivalent per arm**: iteration/loop limits, token budgets,
  retry policies, and workspace projection rules whose mechanics differ across
  execution modes.
- **Arm-specific but recorded**: compaction strategy, workspace adapter, and
  execution-mode-only providers.

Reports separate:

- deterministic task completion;
- continuity;
- token cost and attribution;
- context safety;
- artifact reuse;
- trajectory and errors;
- latency;
- cancellation;
- termination;
- uncertainty.

Hosted evidence remains advisory for automation unless a later product decision
defines predeclared statistical gates.

Every hosted arm uses the existing evaluation-capture surface so evaluator logic
can be replayed without re-invoking providers. The workflow is dispatch or
schedule only, is not a required pull-request status, and fails only for
infrastructure, schema, or deterministic-contract errors rather than stochastic
quality differences.

## Quickstart Validation Scenarios

The companion [quickstart.md](quickstart.md) validates:

1. Spec artifact completeness without implementation.
2. Candidate dependency compatibility report.
3. Generated-tool non-Azure Harness scenario.
4. Eager offload and explicit rehydration.
5. Experimental compaction preservation.
6. Session isolation and cancellation.
7. NativeAOT profile.
8. Hosted comparison artifact production.

## Risks

| Risk | Impact | Mitigation |
|---|---|---|
| MAF 1.15 uplift breaks current integrations | High | G1 isolated compatibility spike and baseline comparison |
| DevUI/Hosting preview mismatch | Medium | Satellite gate and explicit deferral |
| Experimental compaction changes | High | Explicit opt-in, preservation contract, deterministic fallback |
| Duplicate loops or telemetry | High | G2 ordering contract and runtime/test guard |
| Oversized result enters history before offload | High | Eager boundary fixture before hybrid work |
| Competing file stores | High | Foundry workspace authority invariant |
| Identity/session leakage | High | Fail-closed host binding and contention fixtures |
| Restored approvals escalate privilege | High | Reauthorization requirement |
| AOT path activates reflection | Medium | Minimum generated profile, warnings as errors |
| Scope expands to all Harness features | Medium | Capability matrix and slice gates |
| Stochastic evaluation overrules deterministic evidence | Medium | FR-059 and human decision artifacts |

## Complexity Tracking

| Complexity | Why Needed | Simpler Alternative Rejected Because |
|---|---|---|
| Two consumption lanes | Separates reusable core providers from opinionated bundle defaults | One bundle path would leak dependencies/defaults and reduce composition control |
| Workspace/file-store bridge | Preserves provider-neutral artifact authority and existing workspace guarantees | Replacing workspace with upstream store loses neutral and orchestration semantics |
| Hybrid context policy | Addresses both total history loss and unbounded context growth | History-only and workspace-only modes each have demonstrated failure modes |
| Separate deterministic and hosted evaluation | Distinguishes correctness from stochastic quality evidence | One evaluation mode either cannot prove contracts or is too unstable for local gates |
| Optional new package | Isolates bundle transitive dependencies and behavior | Adding bundle to base MAF package affects non-adopters |

## Planning Exit Criteria

- All research decisions have rationale and alternatives.
- Data model and behavioral contracts are complete.
- Logical groups, dependencies, gates, and parallel work are explicit.
- Temporary duplication has owners and decision triggers.
- No implementation dependency or runtime API change has occurred.
- Independent plan and task reviews have no unresolved blocking finding.

## Initial Delivery Definition

The first increment is an **internal technical MVP**, not an externally
published feature:

1. G1 compatibility proof
2. G2 internal composition foundation
3. One stable G3 provider slice

No package is published and no consumer-facing API is promoted until the
API-candidate gate records the surface as reviewed.

## Autopilot Delivery Workflow

### GitHub hierarchy

- One root implementation issue links research issue #12, the published spec
  branch/PR, plan, tasks, traceability, and review artifacts.
- G1-G10 are direct sub-issues of the root.
- Each group contains vertical, independently reviewable leaf issues.
- T001-T130 remain checklist items inside leaf issues rather than 130 flat
  GitHub issues.
- GitHub parent/sub-issue and blocked-by relationships mirror the plan graph.

### Branch and pull-request strategy

- A stage integration branch is the base for leaf PRs within the active group.
- Leaf branches and PRs target the stage integration branch, not `main`.
- Leaf PRs may merge automatically after required deterministic tests and
  multi-model review have no unresolved blocking finding.
- At a completed group gate, one stage PR targets `main`.
- A stage PR may auto-merge only after its gate evidence, CI, architecture
  reviews, and scope audit pass.
- The G10 Spec Kit cleanup PR is always treated as a stage PR and requires the
  scope-preservation and gate-evidence reviews before merge.
- Direct pushes to `main` are not part of this workflow.

### Review requirements

Each leaf PR receives:

- a domain specialist review where applicable;
- an independent code or architecture review using a different model;
- a rubber-duck review focused on logic and scope;
- targeted security review when the issue changes trust boundaries;
- deterministic tests named by the issue.

Each stage PR receives:

- at least two architecture reviews using different models;
- gate-evidence verification;
- integration and AOT checks applicable to the stage;
- a scope-preservation review against the parent and leaf issues.

### Scope control

- A leaf issue's acceptance criteria and task IDs define its implementation
  scope.
- Review findings that are not critical blockers are filed as separate
  post-MVP follow-up issues rather than added to the active PR.
- Drive-by refactors and adjacent feature additions are prohibited.
- A critical blocker may expand the active issue only when the remaining plan
  cannot proceed safely without resolving it.
- If a blocker changes package boundaries, context authority, security,
  persistence, public API, or the dependency graph, implementation pauses and
  the specification and remaining plan are regrouped and re-reviewed.
