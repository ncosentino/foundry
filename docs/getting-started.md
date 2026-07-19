---
description: Build Foundry, choose the packages for your scenario, register the runtime, and create your first generated agent.
---

# Getting Started

Foundry is currently preparing for its first alpha package release. The
package references below show the intended package layout but are not available
from NuGet.org yet. You can explore the complete framework today by building
the repository and running the included examples.

## 1. Build Foundry

Clone the repository and build the complete solution:

```powershell
git clone https://github.com/ncosentino/foundry.git
Set-Location foundry
dotnet build src\NexusLabs.Foundry.slnx
```

The solution includes the production packages, tests, source generators,
analyzers, and 30 example projects.

## 2. Choose the package boundary

Start with the smallest package set that owns the capability you need:

```xml
<PackageReference Include="NexusLabs.Foundry.MicrosoftAgentFramework" />
<PackageReference Include="NexusLabs.Foundry.MicrosoftAgentFramework.Workflows" />
<PackageReference Include="NexusLabs.Foundry.MicrosoftAgentFramework.Generators"
                  OutputItemType="Analyzer"
                  ReferenceOutputAssembly="false" />
<PackageReference Include="NexusLabs.Foundry.MicrosoftAgentFramework.Analyzers"
                  OutputItemType="Analyzer"
                  ReferenceOutputAssembly="false" />
```

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
