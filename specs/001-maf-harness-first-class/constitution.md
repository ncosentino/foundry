# Foundry Harness Migration Constitution

## Core Principles

### I. Neutral Core, Replaceable Integrations

Neutral Foundry packages MUST NOT depend on Needlr or a provider-specific agent
framework. Microsoft Agent Framework, Microsoft.Extensions.AI, Langfuse,
Copilot, Semantic Kernel, hosting, and UI integrations MUST remain replaceable
adapters over neutral Foundry contracts. Provider integrations MAY expose
provider-native escape hatches, but provider types MUST NOT become requirements
of neutral generated code.

### II. Evidence-Gated API Evolution

Public contracts MUST be promoted from demonstrated, independently tested use
cases rather than speculative generalization. New capabilities SHOULD begin as
internal contract candidates or provider adapters, pass conformance across
materially different scenarios, and replace or simplify an existing concept
before becoming permanent public API. Temporary duplication MUST identify its
removal trigger. Alpha APIs MAY break directly; compatibility shims for former
alpha package identities MUST NOT be added.

### III. Hybrid Context and Workspace State

Long-running agents MUST NOT be forced into either unbounded conversation
history or history-free execution. Conversational working memory SHOULD retain
recent decisions, corrections, and active tool exchanges while compaction keeps
its context bounded. Bulk artifacts, large tool results, and durable shared
state SHOULD live in a workspace and be selectively projected back into model
context. Offload, compaction, retrieval, and rehydration decisions MUST be
observable and testable.

### IV. Deterministic, Testable, and Observable Execution

Deterministic control flow, bounded resource use, explicit termination, and
structured results are the default. Routing, joins, retries, approvals,
budgets, and completion policies MUST be testable without a live model.
Diagnostics MUST preserve run, node, tool, token, progress, and termination
evidence. OpenTelemetry integrations MUST avoid duplicate spans and remain
compatible with Foundry diagnostics and evaluation.

### V. Explicit Contracts and API Discipline

Public types and members MUST be documented with XML documentation. Public APIs
MUST use explicit overloads and required option records rather than optional
parameters or default interface members. Data carriers SHOULD be records;
services and mutable behavior SHOULD be classes. Types SHOULD be internal unless
consumers require direct access. Expected failures SHOULD use explicit result
contracts; errors MUST NOT be silently swallowed or converted into
success-shaped outcomes. One public type per C# file and file-scoped namespaces
are required.

## Architecture and Technology Constraints

- Foundry targets .NET 10 and uses centrally managed package versions.
- Needlr integration belongs only in `NexusLabs.Foundry.Needlr.*`.
- Source generation and analyzers MUST preserve NativeAOT and trimming paths.
- Generated neutral registries, factories, and topology MUST NOT require
  runtime reflection when source generation is enabled.
- Provider preview dependencies MUST remain isolated from stable neutral
  packages.
- Diagnostic identifiers MUST use the `FDRY` prefix.
- Architecturally significant changes MUST be recorded in `docs/adr/` and
  accepted ADR history MUST be superseded rather than rewritten.
- Workspace paths, user identity, tool authorization, shell execution, MCP
  sources, and external skills MUST be treated as trust boundaries.

## Development Workflow and Quality Gates

- Specifications MUST define measurable behavior and independent validation
  scenarios before implementation planning.
- Plans MUST identify dependency boundaries, experimental APIs, migration
  gates, temporary duplication, and removal criteria.
- Task groups MUST be independently reviewable and list explicit dependencies
  and parallel opportunities.
- Public API changes, source-generator changes, analyzer behavior, AOT paths,
  cancellation semantics, and persistence formats require targeted contract
  tests.
- Local validation MUST remain narrowly targeted and deterministic. Broad
  provider smoke tests and evaluation workloads MUST run in hosted CI.
- Agent-generated specifications and plans MUST receive independent reviews
  from different models with different architectural focuses before
  implementation begins.
- No implementation dependency or runtime API change may begin while required
  specification or plan gates remain unresolved.

## Governance

This constitution governs the Harness migration specification and
implementation program. It supplements `AGENTS.md` and repository-scoped
instructions; the stricter requirement applies when guidance overlaps.
Amendments require an explicit rationale, impact analysis, and version change.
Reviews MUST identify constitution conflicts as blocking until the
specification, plan, or constitution is deliberately revised.

**Version**: 1.0.0 | **Ratified**: 2026-07-22 | **Last Amended**: 2026-07-22
