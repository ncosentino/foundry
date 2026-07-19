---
description: Build Foundry, choose the packages for your scenario, register the runtime, and create your first generated agent.
---

# Getting Started

Foundry 0.1.0-alpha.1 is published as NuGet version `0.1.0-alpha-0001`.
Package IDs, namespaces, and APIs may change before the first stable release.

## 1. Install Foundry

Install the runtime, workflows, source generator, and analyzers for a generated
agent application:

```xml
<PackageReference Include="NexusLabs.Foundry.MicrosoftAgentFramework"
                  Version="0.1.0-alpha-0001" />
<PackageReference Include="NexusLabs.Foundry.MicrosoftAgentFramework.Workflows"
                  Version="0.1.0-alpha-0001" />
<PackageReference Include="NexusLabs.Foundry.MicrosoftAgentFramework.Generators"
                  Version="0.1.0-alpha-0001"
                  PrivateAssets="all" />
<PackageReference Include="NexusLabs.Foundry.MicrosoftAgentFramework.Analyzers"
                  Version="0.1.0-alpha-0001"
                  PrivateAssets="all" />
```

All Foundry packages in a release use the same version.

## 2. Choose the package boundary

Start with the smallest package set that owns the capability you need. Add
`NexusLabs.Foundry.Evaluation`, `NexusLabs.Foundry.Langfuse`,
`NexusLabs.Foundry.Copilot`, or the testing and DevUI packages only when the
application uses those capabilities.

Add `NexusLabs.Foundry.Needlr.MicrosoftAgentFramework` only when the application
uses Needlr as its composition system.

## 3. Register the runtime

Register Foundry with a standard `IServiceCollection` and supply the
`IChatClient` used by generated agents:

```csharp
var services = new ServiceCollection();
services.AddFoundryAgentFramework(builder => builder
    .UsingChatClient(chatClient)
    .UsingDiagnostics());
```

Needlr applications can use the equivalent `UsingAgentFramework` integration
on a `Syringe`.

## 4. Declare an agent

Use `[FoundryAgent]` to define the agent's instructions and tool scope:

```csharp
[FoundryAgent(
    Instructions = "Research the request and return a concise, sourced answer.")]
internal sealed partial class ResearchAgent
{
}
```

The source generator emits the registries used by `IAgentFactory` and the
workflow factories.

## 5. Create and run the agent

Resolve the factory, create the declared agent, and run it through Microsoft
Agent Framework:

```csharp
using var provider = services.BuildServiceProvider();
var agent = provider
    .GetRequiredService<IAgentFactory>()
    .CreateAgent<ResearchAgent>();

var response = await agent.RunAsync(
    "Compare reflection and source generation.",
    cancellationToken: CancellationToken.None);
```

## Next steps

- Learn the [agent and workflow model](ai-integrations.md).
- Add [progress reporting](progress-reporting.md) and
  [pipeline metrics](pipeline-metrics.md).
- Build repeatable quality gates with the
  [provider-neutral experiment runner](experiment-runner.md).
- Explore the runnable projects under
  [`src/Examples`](https://github.com/ncosentino/foundry/tree/main/src/Examples).
