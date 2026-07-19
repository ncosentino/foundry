# Foundry

Foundry is an AI and agentic application framework for .NET. It provides
composable agent orchestration, diagnostics, evaluation, experimentation,
provider integrations, observability, and deterministic testing.

Foundry's core packages are dependency-injection neutral. Optional integration
packages connect Foundry to Needlr without making Needlr a dependency of the
runtime, evaluation, or provider packages.

## Package families

- `NexusLabs.Foundry.MicrosoftAgentFramework` provides agent construction,
  diagnostics, progress, context, workspace, and generated topology.
- `NexusLabs.Foundry.MicrosoftAgentFramework.Workflows` provides sequential,
  group, handoff, and graph workflow execution.
- `NexusLabs.Foundry.Evaluation` provides MEAI evaluation and provider-neutral
  experiments.
- `NexusLabs.Foundry.Langfuse` provides Langfuse telemetry, datasets, and
  experiment publication.
- `NexusLabs.Foundry.Copilot` provides GitHub Copilot chat integration.
- `NexusLabs.Foundry.Needlr.*` packages provide optional Needlr integration.
- `NexusLabs.Foundry.Needlr.SemanticKernel.Generators` provides compile-time
  Semantic Kernel plugin registration for Needlr consumers.

## Start here

- [Getting started](getting-started.md)
- [Configuration](configuration.md)
- [Building from source](building.md)
- [Agent Framework integrations and workflows](ai-integrations.md)
- [Provider-neutral experiment runner](experiment-runner.md)
- [Langfuse integration](langfuse.md)
- [Testing tools](testing-tools.md)
- [Architecture decisions](adr/adr-0004-extract-ai-platform-from-needlr.md)
