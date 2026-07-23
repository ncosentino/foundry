# Feature Specification: First-Class Microsoft Agent Framework Harness Support

**Feature Branch**: `001-maf-harness-first-class`

**Created**: 2026-07-22

**Status**: Reviewed

**Input**: User description: "Build Microsoft Agent Framework Harness as a first-class Foundry offering using an evidence-driven migration. Long-running agents must use a hybrid context model that retains and compacts useful conversation while storing bulk content and large tool results in a workspace for selective retrieval. Produce a reviewed specification and dependency-aware plan before changing dependencies or APIs."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Run a Long-Lived Harness Agent Without Losing Context (Priority: P1)

As a Foundry developer, I can create and run a Microsoft Agent Framework
Harness agent through Foundry using my selected chat provider and
Foundry-generated tools. The agent retains recent conversation and decisions,
compacts older conversation when necessary, stores bulk artifacts in a
workspace, and selectively restores relevant artifacts without requiring
application-specific loop code.

**Why this priority**: This is the core developer value and directly resolves
the current failure modes of losing all conversational continuity or allowing
history to grow without bound.

**Independent Test**: A deterministic multi-turn scenario can run through the
Foundry Harness path with a non-Azure chat client, generated tools, a workspace,
and a session. With experimental hybrid compaction explicitly enabled, the
scenario preserves required decisions across compaction, eagerly offloads a
large tool result before it enters chat history, later retrieves it, and
completes without exceeding the configured context boundary.

**Acceptance Scenarios**:

1. **Given** a configured chat client, generated Foundry tools, and a workspace,
   **When** a developer creates and runs a Harness-enabled Foundry agent,
   **Then** the agent executes through the supported Harness lifecycle without
   requiring Azure hosting or a custom tool-invocation loop.
2. **Given** an extended conversation approaching its context boundary,
   **When** the developer explicitly enables the experimental hybrid compaction
   profile and the agent continues working,
   **Then** older context is compacted while current instructions, decisions,
   unresolved work, approvals, tool-call integrity, and workspace references
   remain available.
3. **Given** a tool result or generated artifact too large for repeated chat
   transmission,
   **When** the result is produced,
   **Then** the bulk content is stored in the workspace before the full payload
   is appended to chat history, and the conversation retains a concise
   description and stable reference.
4. **Given** a later task that requires an offloaded artifact,
   **When** the context policy identifies it as relevant,
   **Then** the necessary content is rehydrated without retransmitting unrelated
   workspace content.

---

### User Story 2 - Adopt Harness Incrementally Without Replacing Foundry (Priority: P2)

As a Foundry maintainer, I can introduce Harness capabilities incrementally,
using individual stable MAF providers or the complete Harness bundle, while
preserving existing Foundry agent, workflow, workspace, diagnostics, testing,
evaluation, and provider integration paths until evidence supports a deliberate
replacement.

**Why this priority**: Harness adoption spans a major MAF version uplift and
overlaps several Foundry capabilities. A staged migration prevents dependency
changes or upstream defaults from silently redefining Foundry behavior.

**Independent Test**: A compatibility report and deterministic adapter suite
demonstrate that existing non-Harness agents, generated tools, workflows,
diagnostics, evaluation, DevUI integration, and AOT paths continue to work after
the candidate dependency uplift, without publishing a new permanent API.

**Acceptance Scenarios**:

1. **Given** Foundry's current MAF and MEAI package graph,
   **When** the candidate Harness-compatible versions are evaluated,
   **Then** all compile, behavioral, source-generation, analyzer, AOT, telemetry,
   and hosting compatibility differences are recorded before adoption.
2. **Given** a developer who wants selected Harness capabilities but not the
   opinionated bundle,
   **When** they configure a Harness-capable Foundry agent,
   **Then** stable upstream providers can be selected individually.
3. **Given** a developer who wants the batteries-included upstream experience,
   **When** they opt into the complete Harness offering,
   **Then** its dependency and middleware effects remain isolated from ordinary
   Foundry MAF consumers.
4. **Given** an overlapping Foundry capability,
   **When** upstream behavior is introduced,
   **Then** the existing capability remains until parity evidence, migration
   guidance, and an explicit removal trigger are documented.

---

### User Story 3 - Use Workspace, Session, and Approval State Safely (Priority: P2)

As an application developer, I can bind Harness file memory and file access to
a Foundry workspace, partition that state by trusted execution identity, manage
agent sessions explicitly, and surface approval requests without relying on
the sample console or process-global local directories.

**Why this priority**: Workspace, session, and approval boundaries determine
durability, multi-user isolation, and tool safety. Default Harness storage and
sample UX behavior are not sufficient for a reusable framework integration.

**Independent Test**: Two isolated deterministic sessions use the same Foundry
host without seeing each other's workspace, history, approvals, or artifacts.
One session can serialize and restore its supported state, while denied or
invalid workspace operations remain explicit failures.

**Acceptance Scenarios**:

1. **Given** a Foundry workspace,
   **When** Harness file memory or file access is enabled,
   **Then** upstream file operations use the workspace's canonical paths,
   isolation, and failure semantics.
2. **Given** two users or orchestration runs,
   **When** both use Harness capabilities concurrently,
   **Then** session history, workspace artifacts, todos, modes, and standing
   approvals remain isolated.
3. **Given** the default in-memory history provider,
   **When** cross-process durability is not configured,
   **Then** Foundry clearly reports that the session is not crash-durable.
4. **Given** an approval-required tool call,
   **When** approval is requested, denied, granted, or granted as a standing
   rule,
   **Then** the structured state transition is observable without requiring the
   shared console sample.

---

### User Story 4 - Observe and Evaluate Harness Behavior (Priority: P3)

As a Foundry operator or evaluator, I can distinguish model calls, tool calls,
compaction, artifact offload, artifact rehydration, approvals, background work,
loop iterations, and termination in diagnostics and progress output without
duplicate telemetry.

**Why this priority**: Hybrid context behavior and middleware composition cannot
be trusted or optimized if their decisions are invisible.

**Independent Test**: A deterministic trace fixture records one coherent
sequence for a multi-turn Harness run, including compaction and workspace
events, with exactly one agent span and one record per model and tool call.

**Acceptance Scenarios**:

1. **Given** Foundry and Harness telemetry are both available,
   **When** an agent run executes,
   **Then** instrumentation is composed so the run does not emit duplicate
   agent, model, or tool records.
2. **Given** compaction, offload, or rehydration occurs,
   **When** diagnostics are inspected,
   **Then** the reason, affected content category, size or token impact, and
   resulting reference are visible without exposing sensitive content.
3. **Given** current Foundry loop, plain Harness compaction, and hybrid
   Harness-plus-workspace modes,
   **When** hosted evaluation compares them,
   **Then** the evidence reports task completion, continuity, cumulative token
   cost, context safety, artifact reuse, trajectory quality, and uncertainty
   separately.

### Edge Cases

- The selected chat provider does not support hosted web search or another
  default Harness tool.
- Compaction is requested but the selected strategy is experimental,
  unavailable, or fails.
- The context budget is reached while a tool-call/result pair is incomplete.
- A single tool result exceeds the effective context window before a later
  compaction pass could run.
- Compaction would orphan a tool call from its corresponding tool result.
- The required preservation set alone exceeds the model's effective context
  window.
- An artifact write succeeds but the conversational reference cannot be
  recorded, or the reference is recorded but the write fails.
- Rehydration is requested for a missing, inaccessible, stale, or modified
  artifact.
- A session uses default in-memory history and the process exits.
- A durable history provider restores chat state but workspace artifacts are
  unavailable.
- Cancellation occurs during compaction, artifact persistence, rehydration,
  approval waiting, background-agent work, or a tool call.
- Two concurrent runs attempt to update the same workspace artifact.
- Standing approvals are restored from an untrusted or modified session payload.
- Harness default file memory and Foundry workspace offload are both active and
  attempt to become authoritative for the same artifact.
- Message injection adds new user content while compaction or context assembly
  is in progress.
- Foundry diagnostics and Harness OpenTelemetry both attempt to instrument the
  same run.
- An upstream experimental provider changes or disappears in a later MAF minor
  release.
- DevUI or Hosting lacks a compatible release for the selected MAF version.
- A non-Harness Foundry application is upgraded but does not opt into any
  Harness capability.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Foundry MUST provide a first-class, opt-in Harness agent path
  within its Microsoft Agent Framework integration.
- **FR-002**: The Harness path MUST accept any compatible `IChatClient` and MUST
  NOT require Azure hosting, Azure identity, or Foundry Hosted Agents.
- **FR-003**: Existing non-Harness Foundry agent and workflow paths MUST remain
  usable during migration.
- **FR-004**: Foundry-generated tools MUST be usable by Harness-enabled agents
  without reflection-only registration.
- **FR-005**: Stable Harness-related MAF providers MUST be individually
  selectable without requiring the opinionated Harness bundle.
- **FR-006**: The complete Harness bundle MUST remain an explicit opt-in whose
  dependencies and defaults do not affect consumers that do not select it.
- **FR-007**: Experimental Harness options or providers MUST be disabled by
  default and clearly identified at configuration and diagnostic boundaries.
- **FR-008**: No production dependency uplift or public runtime API change MAY
  begin until a compatibility report covers the stable core graph: MAF core,
  Workflows, Generators, MEAI, Evaluation, source generation, analyzers, AOT,
  telemetry, and current tests. DevUI and Hosting MUST be assessed separately
  as isolated satellite integrations and MAY be explicitly deferred without
  blocking stable core support.
- **FR-009**: The candidate package graph MUST use one coherent set of
  compatible MAF and MEAI versions.
- **FR-010**: The migration MUST document any Harness capability that remains
  unavailable because a compatible satellite package or stable upstream API is
  missing.
- **FR-011**: A Harness-enabled long-running agent MUST retain a bounded
  conversational working set across iterations or runs.
- **FR-012**: The conversational working set MUST support compaction before the
  configured context boundary is exceeded. Upstream compaction is experimental,
  so this path MUST require explicit opt-in and MUST trigger with enough token
  margin to execute the selected compaction strategy safely.
- **FR-013**: Compaction MUST preserve active system instructions, user
  constraints, accepted decisions, unresolved work, approval state, recent
  corrections, tool-call/result integrity, and workspace artifact references.
  Compacted messages MUST preserve valid MEAI tool-call/result sequencing and
  MUST NOT retain either side of a pair without the other. The selected
  compaction strategy MUST be verified against this preservation contract;
  Foundry MUST NOT assume an upstream default strategy provides these
  guarantees.
- **FR-014**: Bulk artifacts and large tool results MUST be eligible for
  workspace offload according to an explicit policy. A tool result that can
  exceed the active context envelope MUST be offloaded eagerly at the
  tool-invocation boundary before its full payload is appended to chat history.
- **FR-015**: After offload, conversational context MUST contain a concise
  description and stable reference rather than repeatedly embedding the full
  artifact.
- **FR-016**: The context policy MUST support selective rehydration of relevant
  workspace artifacts through an explicit, observable decision made by a tool
  request or deterministic context policy; rehydration MUST NOT occur through
  opaque background injection.
- **FR-017**: Unrelated workspace content MUST NOT be loaded into model context
  solely because it exists.
- **FR-018**: The hybrid context policy MUST expose the reason for retaining,
  compacting, offloading, omitting, or rehydrating content.
- **FR-019**: Failures during compaction, offload, or rehydration MUST produce
  explicit failure evidence and MUST NOT silently discard required context.
- **FR-020**: Foundry `IWorkspace` MUST remain the provider-neutral workspace
  contract.
- **FR-021**: Harness file memory and file access MUST be able to operate over a
  Foundry workspace without weakening workspace path or isolation semantics.
  When Foundry workspace integration is enabled, upstream default file-memory
  storage MUST NOT remain active as a second authoritative bulk-content store.
- **FR-022**: Workspace, session, todo, mode, approval, and artifact state MUST
  be bound to a trusted Foundry execution identity supplied by a documented
  host boundary. Missing, invalid, or mismatched identity MUST fail closed
  before state is read or mutated.
- **FR-023**: Foundry MUST distinguish in-memory session continuity from
  cross-process durable persistence.
- **FR-024**: Supported session serialization or durable history restoration
  MUST preserve the state required by enabled Harness providers, or explicitly
  report what cannot be restored. Restored standing approvals MUST be treated as
  untrusted until the host validates or reauthorizes them.
- **FR-025**: Tool approval requests and responses MUST be surfaced as
  structured events independent of the shared console sample.
- **FR-026**: Foundry MUST support stable upstream todo, agent-mode, skills,
  history-persistence, and tool-approval capabilities without reimplementing
  their behavior, but the plan MUST stage them through a versioned capability
  matrix rather than requiring all capabilities in one delivery.
- **FR-027**: Experimental compaction, custom file stores, file access,
  background agents, and loop evaluators MUST require explicit opt-in.
- **FR-028**: Provider-dependent defaults such as hosted web search MUST be
  disableable and MUST fail with actionable capability evidence when unsupported.
- **FR-029**: Foundry MUST compose exactly one effective tool-invocation loop for
  a Harness-enabled agent.
- **FR-030**: Foundry MUST prevent duplicate OpenTelemetry and diagnostic records
  when upstream and Foundry instrumentation overlap, including explicitly
  suppressing one instrumentation layer where composition cannot deduplicate it.
- **FR-031**: Progress and diagnostics MUST identify session, run, iteration,
  tool, approval, compaction, offload, rehydration, background work, and
  termination relationships.
- **FR-032**: Cancellation MUST propagate through model calls, tools,
  compaction, workspace operations, approvals, background work, and outer loops.
- **FR-033**: Deterministic local tests MUST cover generated tools, session
  isolation, context compaction decisions, artifact offload/rehydration,
  approvals, cancellation, and telemetry deduplication without live providers.
- **FR-034**: Hosted evaluation MUST compare current Foundry iterative execution,
  plain Harness with compaction, and hybrid Harness plus workspace using the same
  representative tasks.
- **FR-035**: Evaluation evidence MUST report task completion, continuity,
  cumulative token usage, context-window safety, artifact reuse, tool
  trajectory, termination, latency, tool error rate, cancellation behavior, and
  uncertainty as separate dimensions.
- **FR-036**: Source-generated, analyzer-backed, trimmed, and NativeAOT paths
  MUST remain valid for supported Harness scenarios.
- **FR-037**: Existing Foundry capabilities MUST NOT be removed until equivalent
  or superior behavior is demonstrated and a migration path and removal trigger
  are approved.
- **FR-038**: Every temporary parallel implementation introduced by the
  migration MUST record its purpose, owner, decision gate, and deletion trigger.
- **FR-039**: Documentation MUST distinguish plain Foundry MAF agents, selected
  MAF core providers, the complete Harness bundle, Foundry iterative execution,
  Foundry workflows, hosting, DevUI, and sample console code.
- **FR-040**: Artifact and offloaded-result references MUST include enough
  identity, digest, and size evidence to detect stale or changed content before
  rehydration. A digest mismatch MUST produce explicit stale-reference evidence
  rather than silently injecting changed content.
- **FR-041**: Recompacting already compacted content MUST NOT increase context
  size or repeatedly summarize summaries without a bounded policy. Any
  summarizing compactor MUST have a hard output limit and MUST fall back to a
  deterministic non-LLM reduction or preserve the prior context when it fails
  to reduce size.
- **FR-042**: System instructions MUST remain verbatim across compaction.
- **FR-043**: Structured session state MUST remain authoritative when a
  conversational summary conflicts with session-owned phase, todo, budget,
  decision, or approval state.
- **FR-044**: Comparative evaluations MUST use the same task cases, chat
  provider and model identifier, sampling parameters, tools and tool versions,
  controlling instructions, token budget, iteration cap, retry policy, timeout,
  cancellation policy, and initial workspace state across execution modes. Any
  unavoidable difference MUST be recorded and justified.
- **FR-045**: Every comparative task case MUST include at least one
  deterministic completion or artifact predicate independent of an LLM judge,
  plus a deterministic reference for every dimension used in a cross-mode
  product decision.
- **FR-046**: LLM-judged evidence MUST NOT be the sole basis for migration,
  retention, or removal decisions. Judge-derived dimensions without recorded
  model, rubric, bias checks, and calibration evidence MUST remain advisory.
- **FR-047**: Foundry MUST NOT introduce a neutral abstraction for an upstream
  provider solely to rename that provider; neutral contracts require a
  demonstrated second implementation or a differentiated Foundry semantic.
- **FR-048**: File access, shell, skills, MCP sources, session payloads, and
  approval state MUST be documented and tested as trust boundaries and MUST NOT
  be described as operating-system sandboxing or durable authorization.
- **FR-049**: Context assembly under pressure MUST follow a deterministic,
  documented reduction order that preserves required state, evicts recoverable
  rehydrated bodies before durable references, compacts eligible history, drops
  only explicitly optional context, and returns a distinct structured
  termination if the context remains irreducible. Oversized tool results MUST
  already have been handled eagerly under FR-014.
- **FR-050**: Tokens or size attributable to compaction, offload summaries, and
  rehydration MUST be observable separately from ordinary model input and
  output using a documented attribution taxonomy.
- **FR-051**: Stable artifact bodies, offloaded tool-result payloads, and
  unchanged reference material MUST NOT be retransmitted verbatim on every
  model call. Stable content is content whose recorded digest has not changed.
- **FR-052**: The Foundry Harness integration MUST define and test one
  deterministic ordering contract for Foundry diagnostics, resilience,
  tool-result handling, token budgets, progress, Harness function invocation,
  message injection, history persistence, compaction, approval, telemetry, and
  outer looping.
- **FR-053**: The effective state of every Harness default-on capability MUST be
  inspectable at the Foundry boundary. The selected-provider path MUST allow
  independent control; any complete-bundle default that upstream cannot disable
  MUST be reported as an explicit bundle limitation.
- **FR-054**: Opting into a Harness-compatible Foundry package MUST use one
  coherent MAF 1.15 and MEAI 10.6-or-newer application package graph; conflicting
  MAF or MEAI versions MUST be rejected with actionable dependency evidence.
- **FR-055**: The plan MUST define a minimum supported NativeAOT Harness profile
  using generated Foundry tools, trim and AOT warnings as errors, and no
  reflection-only fallback. Unsupported dynamic profiles MUST be explicitly
  excluded.
- **FR-056**: The plan MUST inventory every concrete overlap between existing
  Foundry behavior and upstream Harness behavior, designate its owner, define
  parity evidence, and record a release-bound retention or deletion decision.
- **FR-057**: Hosted comparative evaluations MUST use a published, versioned
  case set and a predeclared trial, aggregation, paired-comparison, and
  uncertainty method. Point estimates without uncertainty MUST NOT be used as
  comparative evidence.
- **FR-058**: Each evaluation dimension MUST have a published operational
  definition identifying whether it is deterministic, judge-derived, or hybrid,
  its reference evidence, its value range, and its aggregation rule.
- **FR-059**: Hosted stochastic evaluation MUST NOT be the sole automated
  merge, release, retention, or removal gate.
- **FR-060**: Shell MUST be treated as a separate opt-in package and manually
  composed capability; the plan MUST NOT assume a `HarnessAgentOptions`
  shell property exists.

### Key Entities

- **Harness Profile**: The selected stable and experimental capabilities,
  provider requirements, defaults, and isolation expectations for one agent.
- **Conversation Working Set**: The bounded conversational state retained across
  model interactions.
- **Workspace Artifact**: Bulk content stored outside chat with identity,
  metadata, ownership, and integrity information.
- **Artifact Reference**: A stable conversational handle containing identity,
  digest, size, ownership, and location evidence for a workspace artifact.
- **Hybrid Context Decision**: Evidence explaining retention, compaction,
  offload, omission, or rehydration.
- **Session State**: History and provider state associated with one logical
  agent session.
- **Approval State**: Pending, granted, denied, or standing authorization for a
  tool action.
- **Capability Evidence**: The supported, unsupported, stable, experimental, or
  provider-dependent status of one Harness capability.
- **Migration Gate**: Evidence and acceptance conditions required before a
  dependency, API, or removal step proceeds.
- **Evaluation Comparison**: Comparable results for current iterative, plain
  Harness, and hybrid execution modes.

## Scope and Non-Goals

- This feature specifies and plans migration; it does not implement runtime or
  dependency changes.
- Harness support is limited to the Microsoft Agent Framework integration and
  MUST NOT make MAF a neutral Foundry dependency.
- The feature does not replace Foundry graph workflows, source generation,
  analyzers, evaluation, experiment orchestration, testing, progress, or
  diagnostics.
- The feature does not copy the `Harness_Shared_Console` sample into a core
  package.
- The feature does not provide an operating-system sandbox, hosted agent
  service, production approval UI, rate limiter, cost-control service, or
  general distributed scheduler.
- The feature does not commit to a serialized flow, context, or checkpoint wire
  format before a concrete persistence consumer and compatibility policy exist.
- The feature does not require immediate removal of `IIterativeAgentLoop` or
  `IWorkspace`.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A deterministic end-to-end scenario creates and runs a
  Harness-enabled Foundry agent with a non-Azure chat client, generated tools,
  a workspace, and a session.
- **SC-002**: A long-running deterministic fixture with experimental compaction
  explicitly enabled completes without exceeding its documented context
  boundary and records at least one compaction decision containing its reason,
  affected category, and before/after size or token evidence.
- **SC-003**: A large artifact is stored outside conversational context and is
  not retransmitted verbatim after offload unless explicitly rehydrated.
- **SC-004**: Deterministic fixtures define a labeled preservation set of
  instructions, constraints, decisions, unresolved tasks, approvals,
  tool-call/result groupings, and artifact references and prove that every
  labeled item survives compaction without contradiction.
- **SC-005**: Rehydration loads only artifacts selected by the context policy,
  and missing or unauthorized artifacts produce explicit failure evidence.
- **SC-006**: Two concurrent deterministic sessions bound to distinct trusted
  execution identities exercise overlapping paths, approval identifiers, and
  tool arguments without cross-session leakage; every contention attempt
  produces explicit evidence.
- **SC-007**: A deterministic telemetry fixture with a documented number of
  agent, model, and tool invocations records no duplicate agent, model, or tool
  entries across the composed Foundry and upstream instrumentation pipelines.
- **SC-008**: The compatibility report accounts for every centrally pinned
  dependency transitively reachable from MAF core, Workflows, Generators, MEAI,
  Evaluation, source-generation, analyzer, diagnostics, and AOT surfaces, and
  records DevUI and Hosting as separately passed, failed, or deferred satellite
  gates.
- **SC-009**: Existing targeted Foundry agent, workflow, generator, analyzer,
  evaluation, testing, diagnostics, and AOT suites show no regression against
  the pre-uplift baseline on the same test selection before migration approval.
- **SC-010**: Hosted comparison evidence is produced for current iterative,
  plain Harness, and hybrid execution using the controls and statistical
  treatment required by FR-044 and FR-057. Each FR-035 dimension is reported
  per mode with uncertainty, and every claimed cross-mode difference includes
  paired evidence or a documented reason paired analysis is inapplicable.
- **SC-011**: Every proposed removal or supersession specifies its evidence
  gate, pass/fail criteria, migration artifact, owner, and release-bound
  decision point.
- **SC-012**: Independent specification reviewers report no unresolved
  constitution conflict, contradictory requirement, missing acceptance
  criterion, or undefined term used by a success criterion before planning
  begins.
- **SC-013**: On the compaction-invariance deterministic fixture, repeated
  compaction does not increase measured context size beyond documented bounded
  overhead and leaves the system instruction byte-for-byte unchanged.
- **SC-014**: On the offload-retention deterministic fixture, stable offloaded
  artifact bodies and tool-result payloads are not retransmitted verbatim after
  their digest-backed references are established, except on a turn with an
  explicit recorded rehydration decision.
- **SC-015**: Every hosted comparison report identifies execution mode, package
  versions, provider and model, sampling configuration, case-set version,
  deterministic acceptance evidence, judge model and rubric when applicable,
  and uncertainty without using a judge as the sole decision signal.
- **SC-016**: Deterministic cancellation fixtures covering model invocation,
  tool execution, compaction, workspace persistence, rehydration, approval
  waiting, background work, and outer looping produce explicit cancellation
  evidence and no success-shaped result.
- **SC-017**: The minimum supported NativeAOT Harness profile publishes and runs
  with generated Foundry tools, trim and AOT warnings as errors, and no
  reflection-only fallback.
- **SC-018**: A versioned capability matrix identifies every stable,
  experimental, provider-dependent, unsupported, and deferred Harness
  capability in the initial delivery.

## Assumptions

- Microsoft Agent Framework Harness 1.14.0 was the first stable release used by
  the initial research. The planning candidate is the latest verified coherent
  stable line, MAF and Harness 1.15.0 with MEAI 10.6.0, while individually
  annotated experimental capabilities may change.
- Foundry's current MAF 1.3.0 and MEAI 10.5.0 package graph requires a
  coordinated compatibility evaluation before Harness adoption.
- `IChatClient` remains the model-provider boundary for Foundry's MAF
  integration.
- `IWorkspace` remains the provider-neutral bulk artifact and shared workspace
  boundary.
- Broad stochastic evaluations and provider smoke tests run in hosted CI;
  local validation uses deterministic clients and targeted tests.
- The package family is alpha, so superseded APIs may be replaced directly once
  migration gates are satisfied; alpha status does not weaken deterministic,
  comparative, uncertainty, or review evidence requirements.
- The shared console, Foundry Hosted Agents, Foundry memory, and Foundry Toolbox
  are reference or optional integrations rather than requirements of the
  first-class Foundry Harness offering.
- Shell is supplied by a separate opt-in package and composed through context
  providers and tools; the researched Harness options do not expose a shell
  property.
- The Foundry workspace is the authoritative bulk-content store for the hybrid
  profile; upstream default file-memory storage is replaced or disabled for
  that profile.
