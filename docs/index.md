---
description: Build reliable .NET AI agents with generated discovery, workflows, observability, evaluation, and deterministic testing.
---

# Foundry

![Foundry](assets/foundry.webp){ width="240" }

**Forge reliable, observable, and testable AI agent systems in .NET.**

[![CI](https://github.com/ncosentino/foundry/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/ncosentino/foundry/actions/workflows/ci.yml)
[![Documentation](https://github.com/ncosentino/foundry/actions/workflows/docs.yml/badge.svg?branch=main)](https://github.com/ncosentino/foundry/actions/workflows/docs.yml)
[![NuGet prerelease](https://img.shields.io/nuget/vpre/NexusLabs.Foundry.MicrosoftAgentFramework.svg)](https://www.nuget.org/packages/NexusLabs.Foundry.MicrosoftAgentFramework)
[![MIT License](https://img.shields.io/badge/license-MIT-blue.svg)](https://github.com/ncosentino/foundry/blob/main/LICENSE)
[![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/download/dotnet/10.0)
![Status: Alpha](https://img.shields.io/badge/status-alpha-orange)

Foundry is an AI and agentic application framework for .NET. It brings agent
construction, orchestration, diagnostics, evaluation, experimentation,
provider integrations, and deterministic testing into one composable package
family.

Foundry is dependency-injection neutral at its core. Use standard
`IServiceCollection` registration, or add the optional Needlr integrations
when you want source-generated discovery and Needlr's plugin-oriented
composition model.

!!! warning "Alpha release"
    Foundry 0.1.0-alpha.1 is published as NuGet version
    `0.1.0-alpha-0001`. Package IDs, namespaces, and APIs may change before the
    first stable release.

## Why Foundry?

AI prototypes are easy to start and difficult to operate. Foundry focuses on
the engineering concerns that become important after the first successful
prompt:

- **Declarative agents and tools** with generated registries and typed factory
  methods.
- **Composable orchestration** for sequential, handoff, group-chat, graph, and
  iterative-loop workloads.
- **Provider-neutral evaluation** with retries, concurrency limits,
  deterministic and statistical policies, and publication pipelines.
- **Built-in observability** for agent runs, tool calls, token usage, progress,
  budgets, and pipeline performance.
- **Replaceable integrations** for Microsoft Agent Framework, MEAI Reporting,
  Langfuse, GitHub Copilot, Semantic Kernel, and Needlr.
- **Deterministic testing tools** for generated functions, agents, workflows,
  and scripted model interactions.

!!! tip "Use the composition model you already have"
    Neutral Foundry packages never depend on Needlr. Applications can use
    standard Microsoft dependency injection, while Needlr-specific behavior
    remains isolated in `NexusLabs.Foundry.Needlr.*` packages.

## Quick Start

### Install Foundry

Install the runtime package, then add the workflow, generator, analyzer,
provider, or Needlr integration packages required by your application:

```powershell
dotnet add package NexusLabs.Foundry.MicrosoftAgentFramework --version 0.1.0-alpha-0001
```

See [Getting Started](getting-started.md) for the recommended package
combinations and a guided first-agent walkthrough.

### Create a generated agent

Register the runtime with a standard `IServiceCollection`, declare an agent,
and create it through the generated factory:

```csharp
using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Foundry.MicrosoftAgentFramework;
using NexusLabs.Foundry.MicrosoftAgentFramework.Workflows.Diagnostics;

var services = new ServiceCollection();
services.AddFoundryAgentFramework(builder => builder
    .UsingChatClient(chatClient)
    .UsingDiagnostics());

using var provider = services.BuildServiceProvider();
var agent = provider
    .GetRequiredService<IAgentFactory>()
    .CreateAgent<ResearchAgent>();

var response = await agent.RunAsync(
    "Compare the trade-offs of reflection and source generation.",
    cancellationToken: CancellationToken.None);

[FoundryAgent(
    Instructions = "Research the request and return a concise, sourced answer.")]
internal sealed partial class ResearchAgent
{
}
```

The source generator discovers the declaration and emits the registries used
by `IAgentFactory`. Reflection-based discovery remains available when runtime
flexibility matters more than trimming or NativeAOT.

## Choose Your Path

| Goal | Start here |
|---|---|
| Build generated agents and orchestrated workflows | [Agent Framework integrations and workflows](ai-integrations.md) |
| Add progress, diagnostics, and operational metrics | [Progress reporting](progress-reporting.md) and [pipeline metrics](pipeline-metrics.md) |
| Create repeatable AI quality gates | [Provider-neutral experiment runner](experiment-runner.md) |
| Publish telemetry, datasets, and evaluation results | [Langfuse integration](langfuse.md) |
| Test agents without nondeterministic model calls | [Testing tools](testing-tools.md) |
| Browse public contracts by release | [Versioned API reference](api/index.md) |
| Understand the architectural boundaries | [Architecture decisions](adr/adr-0004-extract-ai-platform-from-needlr.md) |

## Package Families

Foundry is intentionally modular so applications can reference only the
capabilities they use:

| Package family | Purpose |
|---|---|
| `NexusLabs.Foundry.MicrosoftAgentFramework` | Agent construction, context, diagnostics, progress, workspace, and topology declarations |
| `NexusLabs.Foundry.MicrosoftAgentFramework.Workflows` | Sequential, handoff, group-chat, and graph workflow execution |
| `NexusLabs.Foundry.MicrosoftAgentFramework.Testing` | Deterministic agent and workflow testing |
| `NexusLabs.Foundry.MicrosoftAgentFramework.Generators` | Compile-time agent, function, and workflow registries |
| `NexusLabs.Foundry.MicrosoftAgentFramework.Analyzers` | Compile-time agent and topology validation |
| `NexusLabs.Foundry.Evaluation` | MEAI evaluation and provider-neutral experiments |
| `NexusLabs.Foundry.Evaluation.Reporting` | MEAI Reporting adapter |
| `NexusLabs.Foundry.Langfuse` | Langfuse telemetry, datasets, scoring, and experiment publication |
| `NexusLabs.Foundry.Copilot` | GitHub Copilot `IChatClient` and web-search integration |
| `NexusLabs.Foundry.Needlr.*` | Optional Needlr composition and source-generation integrations |
