# Foundry

Foundry is an AI and agentic application framework for .NET. It provides
composable agent orchestration, diagnostics, evaluation, experimentation,
provider integrations, observability, and deterministic testing.

Foundry is dependency-injection neutral at its core. Optional integration
packages connect Foundry to Needlr without making Needlr a dependency of the
runtime, evaluation, or provider packages.

## Packages

- `NexusLabs.Foundry.MicrosoftAgentFramework` - agent construction, diagnostics, progress, context, workspace, and generated topology
- `NexusLabs.Foundry.MicrosoftAgentFramework.Workflows` - sequential, group, handoff, and graph workflow execution
- `NexusLabs.Foundry.MicrosoftAgentFramework.Testing` - deterministic agent and workflow testing
- `NexusLabs.Foundry.MicrosoftAgentFramework.DevUI` - Microsoft Agent Framework DevUI integration
- `NexusLabs.Foundry.MicrosoftAgentFramework.Generators` - compile-time agent and workflow registries
- `NexusLabs.Foundry.MicrosoftAgentFramework.Analyzers` - compile-time agent and topology validation
- `NexusLabs.Foundry.Evaluation` - MEAI evaluation and provider-neutral experiments
- `NexusLabs.Foundry.Evaluation.Reporting` - MEAI Reporting integration
- `NexusLabs.Foundry.Langfuse` - Langfuse telemetry, datasets, and experiment publication
- `NexusLabs.Foundry.Copilot` - GitHub Copilot `IChatClient` integration
- `NexusLabs.Foundry.Needlr.MicrosoftAgentFramework` - optional Needlr integration for the Foundry runtime
- `NexusLabs.Foundry.Needlr.SemanticKernel` - Needlr integration for Semantic Kernel

Foundry is being extracted from the alpha Needlr AI packages. Package IDs,
namespaces, and APIs may change during this migration.

## Local CI

Trusted Linux jobs support isolated, ephemeral
[PitCrew](https://github.com/ncosentino/pitcrew) runners. See
[Local CI Runners](docs/local-runners.md) for provisioning, routing, cloud
fallback, and fork-safety behavior.
