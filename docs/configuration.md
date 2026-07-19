---
description: Understand Foundry's configuration surfaces for agents, workflows, diagnostics, evaluation, providers, and optional Needlr integration.
---

# Configuration

Foundry has no single global options object. Configuration stays with the
package that owns the behavior so applications can compose only the features
they need.

| Area | Primary configuration surface |
|---|---|
| Agent runtime | `AddFoundryAgentFramework(...)` and `AgentFrameworkBuilder` |
| Needlr integration | `UsingAgentFramework(...)` |
| Diagnostics and metrics | `UsingDiagnostics()`, `ConfigureMetrics(...)`, and diagnostics options |
| Progress reporting | Registered `IProgressSink` implementations or per-run sinks |
| Token budgets | Token-budget builder extensions and runtime budget policies |
| Sequential pipelines | `SequentialPipelineOptions`, stages, phases, validation, and completion gates |
| Graph workflows | `[AgentGraphEntry]`, `[AgentGraphEdge]`, and `[AgentGraphNode]` attributes |
| Iterative loops | `IterativeLoopOptions` and `IterativeContext` |
| Evaluation | Experiment definitions, run options, evaluators, and quality policies |
| Langfuse | `LangfuseOptions` or `AddFoundryLangfuse(...)` |
| GitHub Copilot | `CopilotChatClientOptions` |

Configuration APIs use explicit overloads and options records rather than
optional public parameters. Provider-specific settings remain isolated from
the neutral orchestration and evaluation packages.

See the feature pages in the navigation for the complete configuration model
for each package.
