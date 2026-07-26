---
description: Choose and configure Foundry's Microsoft Agent Framework Harness paths, generated tools, progress, defaults, and AOT profile.
---

# Microsoft Agent Framework Harness

Foundry supports Microsoft Agent Framework (MAF) without forcing one
orchestration model on every application. Choose the path that matches who
should own the agent pipeline and how much upstream behavior you want.

## Choose an execution path

| Path | Public surface | Best for | Important boundary |
|---|---|---|---|
| Plain Foundry MAF agent | `NexusLabs.Foundry.MicrosoftAgentFramework` | Generated agents, ordinary MAF tools, and Foundry-owned construction | Does not opt into the complete Harness bundle |
| Complete Harness bundle | `NexusLabs.Foundry.MicrosoftAgentFramework.Harness` | The official upstream batteries-included Harness pipeline with explicit Foundry configuration | Upstream owns the function loop and OpenTelemetry |
| Iterative agent loop | `IIterativeAgentLoop` | Workspace-driven work where each iteration should start from a fresh model conversation | Foundry owns the outer loop; see [Iterative Agent Loop](iterative-agent-loop.md) |

The complete bundle is optional. Referencing
`NexusLabs.Foundry.MicrosoftAgentFramework` alone does not add
`Microsoft.Agents.AI.Harness` or change existing agents.

## Install the optional bundle

```powershell
dotnet add package NexusLabs.Foundry.MicrosoftAgentFramework.Harness
```

Add the generator when your application declares `[AgentFunction]` methods:

```xml
<PackageReference
    Include="NexusLabs.Foundry.MicrosoftAgentFramework.Generators"
    OutputItemType="Analyzer"
    ReferenceOutputAssembly="false" />
```

The optional package depends on the neutral Foundry MAF core and the upstream
Harness package. It does not depend on Needlr.

## Build a complete-bundle agent

Every exposed option is required so upstream defaults are never inherited
accidentally. Use explicit `null` values when you want an upstream default or
when a backing is not applicable.

```csharp
using Microsoft.Extensions.AI;

using NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Bundle;

var features = new FoundryHarnessFeatureSelections
{
    EnableWebSearch = false,
    EnableFileMemory = false,
    EnableAgentSkills = false,
    EnableToolAutoApproval = false,
    EnableApprovalNotRequiredFunctionBypassing = false,
    EnableApprovalResponseBinding = false,
    EnableOpenTelemetry = true,
    EnableTodoProvider = false,
    EnableAgentModeProvider = false,
    EnableCompaction = false,
};

var configuration = new FoundryHarnessAgentConfiguration
{
    Id = "support-agent",
    Name = "Support Agent",
    Description = "Answers product support questions.",
    Instructions = "Answer concisely and use the supplied tools when needed.",
    HarnessInstructionsOverride = null,
    ChatClient = chatClient,
    Tools = tools,
    Features = features,
    ProgressAccessor = progressAccessor,
    MaxContextWindowTokens = null,
    MaxOutputTokens = 1_000,
    MaximumIterationsPerRequest = 8,
    FileAccessStore = null,
    FileAccessProviderOptions = null,
    ChatHistoryProvider = null,
    FileMemoryStore = null,
    AgentSkillsSource = null,
    ToolApprovalAgentOptions = null,
    AgentModeProviderOptions = null,
    CompactionStrategy = null,
    OpenTelemetrySourceName = "MyApp.SupportAgent",
    AdditionalContextProviders = [],
};

var factory = new FoundryHarnessAgentFactory();
var agent = factory.Create(configuration, services);
var response = await agent.RunAsync(
    "Summarize the open support request.",
    cancellationToken: cancellationToken);
```

The factory always constructs the official upstream bundle through
`AsHarnessAgent`. It rejects incoherent feature/backing combinations, invalid
token budgets, duplicate caller tools, collisions with enabled built-in tools,
and discoverable pre-existing loop or telemetry middleware.

## Supply source-generated tools

The bundle does not scan assemblies or perform a reflection fallback. Resolve
generated functions through the public bootstrap and pass them through
`Tools`:

```csharp
if (!AgentFrameworkGeneratedBootstrap.TryGetAIFunctionProvider(
        out var functionProvider) ||
    !functionProvider.TryGetFunctions(
        typeof(SupportTools),
        services,
        out var generatedFunctions))
{
    throw new InvalidOperationException(
        "Generated SupportTools functions were unavailable.");
}

configuration = configuration with
{
    Tools = [.. generatedFunctions],
};
```

Generated tools are validated by the same duplicate and built-in collision
rules as hand-authored `AITool` instances.

## Inspect effective defaults

Use `DescribeEffectiveDefaults` before construction to see:

- what you requested;
- what the upstream bundle will actually enable;
- whether a caller object or an upstream default supplies each backing; and
- limitations for unavoidable or currently unexposed dimensions.

```csharp
var defaults = factory.DescribeEffectiveDefaults(configuration);
foreach (var disposition in defaults.Dispositions)
{
    Console.WriteLine(
        $"{disposition.Feature}: " +
        $"{disposition.RequestedState} -> {disposition.EffectiveState} " +
        $"({disposition.BackingSelection})");
}
```

Function invocation, message injection, and per-service-call history
persistence are unavoidable in the complete bundle. Background agents and
loop evaluators are upstream opt-ins that this public candidate does not yet
expose.

## Add Foundry progress without duplicate telemetry

Set `ProgressAccessor` to Foundry's `IProgressReporterAccessor` to emit
agent-, model-, and tool-level progress. Establish a reporter scope around the
run:

```csharp
var reporter = progressFactory.Create("support-workflow", [progressSink]);

using (progressAccessor.BeginScope(reporter))
{
    await agent.RunAsync(
        "Handle the support request.",
        cancellationToken: cancellationToken);
}
```

Foundry progress does not add another function loop, `ActivitySource`, meter,
or diagnostics writer. The upstream bundle remains the sole owner of its
agent-, chat-, and tool-level OpenTelemetry.

Do not replace the constructed
`FunctionInvokingChatClient.FunctionInvoker` if you rely on Foundry
tool-progress events; replacing that mutable upstream delegate also replaces
the progress hook.

## Choose a context strategy

The complete bundle and Foundry's selected-provider work solve different
context problems:

- **Upstream bundle compaction** is available when you explicitly enable it
  with an upstream strategy or valid context/output budgets.
- **Foundry experimental hybrid compaction** verifies structural preservation,
  workspace-backed artifact references, and per-provider-call context bounds.
  It remains an internal selected-provider profile and is not enabled by the
  public complete-bundle factory.
- **Foundry iterative execution** starts each iteration with a fresh
  workspace-derived prompt and is often a better fit when files are the
  authoritative working state.

See [Iterative Agent Loop](iterative-agent-loop.md) for a detailed comparison.

## NativeAOT

`AotHarnessApp` is the minimum supported NativeAOT profile:

- source-generated Foundry tools;
- no reflection fallback;
- a deterministic non-Azure `IChatClient`;
- the optional Foundry Harness factory;
- a real MAF session and workspace side effect;
- trim and AOT warnings treated as errors; and
- native binary execution in CI.

Dynamic skills/scripts, background agents, loop evaluators, and experimental
hybrid compaction are not included in that minimum profile.

## Internal selected-provider conformance example

`src/Examples/AgentFramework/HarnessHybridApp` demonstrates the stable
selected-provider seam with:

- generated tools;
- a non-Azure scripted provider;
- trusted workspace and session binding;
- Foundry-owned diagnostics/progress;
- exactly one function loop and telemetry owner; and
- an explicit assertion that experimental compaction is disabled.

!!! warning "Contributor-only internal example"
    `HarnessHybridApp` is a non-packable friend assembly over internal
    composition types. The unsigned `InternalsVisibleTo` grant is not a
    security boundary and does not make those types a supported API. Consumers
    should choose a public path from the table at the start of this guide.

The task-defined name `HarnessHybridApp` refers to the selected-provider seam
where experimental hybrid context can attach. The example itself uses the
stable-only profile and proves that compaction remains disabled; it does not
claim to execute hybrid compaction.

## Current limitations

- The optional bundle API remains prerelease.
- Opaque `IChatClient` wrappers can hide middleware if they do not forward
  `GetService`.
- Caller-provided context providers can inject dynamic tool names that cannot
  be collision-checked before a run.
- Cancellation uses the existing failure progress events because no
  cancellation-specific progress event exists.
- Stable release guidance remains subject to the final API and release review.

See [ADR-0005](adr/adr-0005-first-class-maf-harness-integration.md),
[ADR-0006](adr/adr-0006-hybrid-context-and-workspace-authority.md),
[ADR-0007](adr/adr-0007-experimental-hybrid-context-compaction.md), and
[ADR-0008](adr/adr-0008-optional-harness-bundle.md) for the architecture
decisions behind these boundaries.
