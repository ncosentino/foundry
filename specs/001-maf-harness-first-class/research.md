# Research: First-Class Microsoft Agent Framework Harness Support

## Decision 1: Use the latest coherent stable Harness line as the candidate baseline

**Decision**: Evaluate a coordinated candidate graph using MAF core, Workflows,
Generators, and Harness 1.15.0 with MEAI and MEAI Evaluation 10.6.0 or newer.

**Rationale**: `Microsoft.Agents.AI.Harness` first reached stable release at
1.14.0 and now ships 1.15.0 in lockstep with MAF core and Workflows 1.15.0.
Foundry currently pins MAF 1.3.0 and MEAI 10.5.0, so Harness cannot be added
independently.

**Alternatives considered**:

- Keep MAF 1.3.0 and recreate Harness capabilities in Foundry: rejected because
  it duplicates first-party providers and misses the requested upstream
  integration.
- Adopt an earlier preview Harness: rejected because a stable line exists and
  preview APIs add avoidable uncertainty.

**Evidence gate**: No capability implementation begins until the uplift report
covers compilation, runtime behavior, source generation, analyzers, diagnostics,
evaluation, NativeAOT, and satellite-package status.

## Decision 2: Use two integration lanes

**Decision**:

1. Primary lane: consume reusable providers from `Microsoft.Agents.AI` core
   through Foundry's existing MAF builder/plugin model.
2. Optional lane: expose the complete opinionated `HarnessAgent` bundle through
   an isolated optional Foundry integration.

**Rationale**: Todo, modes, approvals, skills, file providers, compaction,
background agents, and loop evaluators live in MAF core. The `.Harness` package
is a thin bundle that turns many defaults on and controls internal middleware
ordering.

**Alternatives considered**:

- Make `HarnessAgent` the default Foundry agent: rejected because it would
  subsume Foundry composition and introduce opinionated defaults for all MAF
  consumers.
- Expose only the complete bundle: rejected because consumers need stable
  capabilities without unrelated experimental or provider-dependent behavior.

## Decision 3: Treat the hybrid context model as a Foundry semantic

**Decision**: The target execution model combines:

- bounded retained conversation;
- explicit compaction;
- structured session state;
- eager large-tool-result offload;
- workspace-backed bulk artifacts;
- digest-backed references;
- selective explicit rehydration;
- bounded per-call and cumulative token use.

**Rationale**: History-free execution loses decisions and corrective context.
Unbounded conversational history causes superlinear cost and context failure.
Neither extreme satisfies long-running workspace-centric agents.

**Alternatives considered**:

- Preserve all history and rely only on provider context limits: rejected as
  unbounded and costly.
- Rebuild every iteration only from workspace: retained as a specialized
  existing mode, but rejected as the only first-class long-running model.
- Inline the full workspace into every prompt: rejected because it recreates
  unbounded context growth.

## Decision 4: Keep `IWorkspace` authoritative for bulk content

**Decision**: Foundry `IWorkspace` remains the provider-neutral authoritative
artifact store. MAF file-memory and file-access providers operate through an
adapter to that workspace for the hybrid profile.

**Rationale**: `IWorkspace` supplies canonical paths, explicit operation
results, compare-exchange, decorators, deterministic testing, and
per-orchestration ownership that upstream `AgentFileStore` does not replace.

**Alternatives considered**:

- Replace `IWorkspace` with `AgentFileStore`: rejected because it introduces a
  MAF dependency into a neutral capability and loses Foundry semantics.
- Keep upstream default file memory beside Foundry offload: rejected because it
  creates two authoritative bulk stores and ambiguous durability.

## Decision 5: Offload oversized tool results eagerly

**Decision**: Tool results that can exceed the active context envelope are
offloaded at the tool-invocation boundary before their full payload enters chat
history.

**Rationale**: Retroactive compaction can run too late when one tool result is
larger than the remaining or total model context. The conversation should
receive a bounded digest and reference while the complete result remains in the
workspace.

**Alternatives considered**:

- Append first and compact later: rejected because the next provider request
  can fail before compaction.
- Truncate tool results without storage: rejected because it loses data and
  prevents later rehydration.

## Decision 6: Preserve valid MEAI message sequences

**Decision**: Compaction and offload preserve valid tool-call/result groupings.
No tool call or result may remain orphaned.

**Rationale**: Providers validate message sequence structure and can reject
orphaned tool calls or results. Tool-exchange integrity is also necessary for
accurate reasoning and diagnostics.

## Decision 7: Treat upstream compaction as experimental

**Decision**: Hybrid compaction is an explicit experimental opt-in until the
selected strategy satisfies the Foundry preservation, bounded-output,
determinism, and observability contracts.

**Rationale**: The Harness package line is stable at 1.15.0, but compaction
options remain annotated experimental. Upstream extension points are reusable,
but their default behavior cannot be assumed to satisfy Foundry guarantees.

**Alternatives considered**:

- Reimplement the entire compaction framework: rejected before evidence shows a
  semantic gap that cannot be addressed through an upstream strategy.
- Present compaction as a stable Foundry default: rejected while the upstream
  API and behavior remain experimental.

## Decision 8: Separate stable and experimental capability delivery

**Decision**: Maintain a versioned capability matrix:

- stable candidates: todo, agent mode, tool approval, skills, file memory,
  per-service-call history;
- experimental opt-ins: compaction configuration, custom stores, file access,
  background agents, loop evaluators;
- provider-dependent: hosted web search tool capability, not a core context
  provider;
- separate packages: shell and other tool providers;
- optional bundle: `HarnessAgent`.

**Rationale**: A single Harness on/off switch hides stability, provider, trust,
and dependency differences.

## Decision 9: Make effective middleware ordering a tested contract

**Decision**: The plan must establish one deterministic ordering for:

- Foundry diagnostics;
- resilience;
- tool-result handling;
- token budgets;
- progress;
- Harness function invocation;
- message injection;
- history persistence;
- compaction;
- approval;
- OpenTelemetry;
- outer looping.

**Rationale**: Duplicate function loops or telemetry corrupt behavior and
metrics. Ordering also determines whether errors, approvals, and history are
captured correctly.

## Decision 10: Distinguish continuity from durability

**Decision**: Session continuity and cross-process durability are separate
capabilities.

**Rationale**: Harness persists history after each service call, but its default
history provider is in-memory. Cross-process recovery requires a durable
provider or explicit session serialization plus available workspace artifacts.

**Alternatives considered**:

- Claim default crash durability: rejected as factually incorrect.
- Require a particular durable backend in the specification: deferred until
  the plan evaluates concrete consumers and storage constraints.

## Decision 11: Keep DevUI and Hosting as satellite gates

**Decision**: Stable core Harness support may proceed when DevUI or Hosting
compatibility is explicitly deferred with evidence and a separate gate.

**Rationale**: These packages are already isolated preview/alpha integrations.
Allowing them to block core support would defeat incremental migration.

## Decision 12: Preserve Foundry differentiators

**Decision**: Harness does not replace:

- source generation and analyzers;
- graph and sequential workflow semantics;
- provider-neutral workspace;
- history-free iterative execution;
- diagnostics and progress;
- resilience and provider selection;
- evaluation and experiment orchestration;
- deterministic testing;
- Copilot and Langfuse integrations.

**Rationale**: Harness is an opinionated single-agent composition, not a
replacement for these framework capabilities.

## Decision 13: Delay removals until comparative evidence exists

**Decision**: No existing Foundry loop, workspace, diagnostics, or middleware
surface is removed during initial Harness integration.

**Rationale**: The current iterative loop, plain Harness compaction, and hybrid
mode have different cost and state behavior. Removal requires workload-specific
parity evidence, migration guidance, and a release-bound decision.

## Decision 14: Use layered evaluation

**Decision**:

- deterministic local fixtures validate contracts and invariants;
- hosted stochastic evaluation compares execution modes using versioned cases,
  paired controls, uncertainty, and calibrated judge evidence;
- stochastic evidence is not a sole automated merge or removal gate.

**Rationale**: Agent quality is stochastic, but context integrity, path
isolation, message sequencing, offload, cancellation, and telemetry
deduplication are deterministic contracts.

## Decision 15: Do not adopt sample-console contracts

**Decision**: The shared console is a reference for commands, observers, session
import/export, approval presentation, and streaming UX, but none of those sample
types become Foundry contracts through this feature.

**Rationale**: The console is sample code, has its own cancellation and hosting
assumptions, and is not part of the released Harness package.

## Primary Sources

- [Harness release](https://devblogs.microsoft.com/agent-framework/the-microsoft-agent-framework-harness-is-now-released/)
- [Build Your Own Claw](https://devblogs.microsoft.com/agent-framework/build-your-own-claw-and-agent-harness-with-microsoft-agent-framework/)
- [Harness documentation](https://learn.microsoft.com/en-us/agent-framework/agents/harness)
- [Harness 1.15.0 release](https://github.com/microsoft/agent-framework/releases/tag/dotnet-1.15.0)
- [Harness NuGet package](https://www.nuget.org/packages/Microsoft.Agents.AI.Harness/)
- [Harness source](https://github.com/microsoft/agent-framework/tree/dotnet-1.15.0/dotnet/src/Microsoft.Agents.AI.Harness)
- [MAF core Harness providers](https://github.com/microsoft/agent-framework/tree/dotnet-1.15.0/dotnet/src/Microsoft.Agents.AI/Harness)
- [MAF skills](https://github.com/microsoft/agent-framework/tree/dotnet-1.15.0/dotnet/src/Microsoft.Agents.AI/Skills)
- [MAF compaction](https://github.com/microsoft/agent-framework/tree/dotnet-1.15.0/dotnet/src/Microsoft.Agents.AI/Compaction)
- [Harness research issue](https://github.com/ncosentino/foundry/issues/12)
