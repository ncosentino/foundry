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
    EnableHybridCompaction = false,
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
    HybridCompactionOptions = null,
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

## Shell is a separate opt-in package

Shell is not part of the Harness bundle and is not a bundle configuration
dimension. The upstream `HarnessAgentOptions` type exposes no shell property,
and `FoundryHarnessAgentConfiguration` deliberately does not invent one. No
Foundry package depends on a shell package, so no consumer acquires shell
execution transitively.

To give an agent shell access, take it as a deliberate opt-in:

1. reference the separate shell package yourself;
2. expose the commands you want as ordinary `AITool` or source-generated
   functions; and
3. pass them through `Tools`, or supply them from a context provider.

Shell tools then flow through the same duplicate-name, built-in-collision, and
approval checks as any other tool. Treat shell as an authorization decision:
Foundry does not grant it, bound it, or sandbox it on your behalf.

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
  with an upstream strategy or valid context/output budgets. It is evaluated
  **once per agent turn**, so it does not bound context inside a tool loop —
  see the limitation below.
- **Foundry hybrid compaction** verifies structural preservation,
  workspace-backed artifact references, and per-provider-call context bounds.
  It is enabled on the complete-bundle factory via
  `EnableHybridCompaction` and `HybridCompactionOptions`.
- **Foundry iterative execution** starts each iteration with a fresh
  workspace-derived prompt and is often a better fit when files are the
  authoritative working state.

See [Iterative Agent Loop](iterative-agent-loop.md) for a detailed comparison.

### Compaction vs. hybrid compaction

The two compaction dimensions are **independent, not layered**. Neither
overrides or suppresses the other, and either can be enabled alone. They differ
in what they act on:

| | `EnableCompaction` (upstream) | `EnableHybridCompaction` (Foundry) |
| --- | --- | --- |
| Runs | once per **agent turn** | once per **provider request** |
| Sits | above the tool loop | innermost, below everything |
| Acts on | the persisted history index | the exact messages being dispatched |
| Shrinks | what is **remembered** | what is **sent** |
| Budget | provider tokens | UTF-8 bytes |
| Configured by | `CompactionStrategy` or token budgets | `HybridCompactionOptions` |

The one-line rule: **upstream compaction shrinks what the agent remembers;
hybrid compaction shrinks what goes on the wire for one call.**

Measured behavior over a two-round tool loop, all four combinations:

| `EnableCompaction` | `EnableHybridCompaction` | upstream runs | hybrid assemblies |
| --- | --- | --- | --- |
| off | off | 0 | 0 |
| on | off | 1 | 0 |
| off | on | 0 | 2 |
| on | on | 1 | 2 |

Enabling hybrid does not extend upstream's reach: in the both-enabled row,
upstream still runs exactly once against the pre-tool-loop state. Enabling
upstream does not reduce hybrid's work either.

#### Hybrid compaction does not shrink stored history

Hybrid compaction is installed inner to the per-service-call history decorator,
so history is persisted **before** a reduction is applied. Every provider call
re-assembles from the full record and re-reduces it. With a reducer that drops
the oldest message, over a three-round loop:

| round | reducer input | reducer output | provider received |
| --- | --- | --- | --- |
| 1 | 1 | 1 | 1 |
| 2 | 3 | 2 | 2 |
| 3 | **5** | 4 | 4 |

Round 3 sees five messages, not four — the message dropped in round 2 is back,
because the drop was never persisted.

Two consequences worth planning for:

- **Nothing is lost from the conversation record.** A hybrid reduction is not
  destructive; the stored history is intact.
- **Reduction work is not cumulative.** The cost is paid on every call and the
  stored history keeps growing. If you need the *record* bounded over a long
  conversation, that is upstream compaction's job, which is a reason to enable
  both.

#### Choosing

- Context grows from **tool results inside one turn** — enable hybrid.
- Context grows **across many turns** — enable upstream.
- Both — enable both; they compose without interfering.

### Upstream compaction does not bound a tool loop

Upstream's `CompactionProvider` is an `AIContextProvider`, and context
providers are invoked once per agent turn rather than once per provider
request. A single agent run that makes several model calls — one per tool
round — therefore compacts only against the state that preceded the **first**
round.

This was measured with a scripted two-round tool loop, compaction enabled, and
a strategy whose trigger always fires:

| measurement | value |
| --- | --- |
| provider calls (model rounds) | 2 |
| `CompactCoreAsync` calls | 1 |
| messages in the index at that call | 2 |

The round carrying the `FunctionCallContent` and its `FunctionResultContent`
is never offered to the strategy. Identical results on
`Microsoft.Agents.AI.Harness` 1.15.0, 1.16.0, and 1.17.0.

If your agent's context growth comes from tool results inside a single turn,
upstream compaction will not bound it. Tracked in
[issue #73](https://github.com/ncosentino/foundry/issues/73).

### Enable hybrid compaction

Hybrid compaction wraps your chat client at the innermost position, so it
observes and bounds the exact message set dispatched for every provider
request, including each intermediate tool round.

```csharp
var configuration = new FoundryHarnessAgentConfiguration
{
    // ...
    Features = new FoundryHarnessFeatureSelections
    {
        // ...
        EnableCompaction = false,
        EnableHybridCompaction = true,
    },
    CompactionStrategy = null,
    HybridCompactionOptions = new FoundryHarnessHybridCompactionOptions
    {
        HardLimitBytes = 262_144,
        TriggerMarginBytes = 65_536,
        RecentMessageRetentionCount = 4,
        MaximumCompactionAttempts = 3,
        UpstreamReducer = myChatReducer,
    },
};
```

The two paths are independent and may be enabled together: upstream bounds
across turns, hybrid bounds within a call.

Limitations to weigh before enabling it:

- **Budgets are UTF-8 bytes of rendered content, not provider tokens.** Bytes
  are computable locally without a tokenizer matched to your provider, so
  choose a budget with headroom rather than treating it as a token count.
- **An irreducible context fails the request.** If assembled context cannot be
  reduced below `HardLimitBytes`, the call throws rather than forwarding an
  over-budget context.
- **`UpstreamReducer` output is a proposal, never ground truth.** Foundry
  verifies it against the hard limit and its own structural-preservation rules
  before anything is dispatched.
- **This is Foundry-owned, not upstream.** Its position depends on the verified
  upstream middleware order, so it is not covered by upstream's compatibility
  guarantees.
- **Upstream compaction strategies are not usable here.** They operate on
  upstream's per-turn index; hybrid compaction takes an
  `IChatReducer` over a single provider request.

`DescribeEffectiveDefaults` reports both dimensions, and each carries the
limitation text above in its `Limitation` field.

See [ADR-0011](adr/adr-0011-public-hybrid-context-compaction.md) for the
decision to make this public and what deliberately stayed internal.

## Run against a real provider

`src/Examples/AgentFramework/HarnessProviderApp` runs one Harness agent whose
only variable is the chat provider. The tools, workspace, session, and bundle
configuration are identical across providers, so switching isolates provider
behavior.

It defaults to a deterministic offline provider, so `dotnet run` costs nothing
and needs no credentials:

```bash
dotnet run --project src/Examples/AgentFramework/HarnessProviderApp
```

To run it against a real model, point it at GitHub Copilot. Create
`appsettings.Development.json` next to `appsettings.json` — that filename is
already git-ignored:

```json
{
  "Harness": {
    "Provider": "copilot"
  },
  "Copilot": {
    "Model": "gpt-4.1"
  }
}
```

Or set an environment variable for a single run:

```bash
Harness__Provider=copilot dotnet run --project src/Examples/AgentFramework/HarnessProviderApp
```

The Copilot provider uses `CopilotTokenSource.Auto`, which resolves the GitHub
Copilot CLI's local credentials first. If you are already signed in to the CLI,
no token belongs in configuration and none belongs in an environment variable.

Because a real model chooses its own wording, the scenario asserts only that the
agent used its workspace tool. Exact-output assertions belong in the
deterministic NativeAOT scenarios instead.

`EnableWebSearch` is opt-in and defaults to `false`. Web search is a hosted
tool, so only a provider that supports it can execute the declaration.

## NativeAOT

`AotHarnessApp` is the minimum supported NativeAOT profile:

- source-generated Foundry tools;
- no reflection fallback;
- a deterministic non-Azure `IChatClient`;
- the optional Foundry Harness factory;
- a real MAF session and workspace side effect;
- trim and AOT warnings treated as errors; and
- native binary execution in CI.

Dynamic skills/scripts, background agents, and loop evaluators are not
included in that minimum profile. Hybrid compaction is exercised separately by
the AOT capability scenario, which enables every reachable feature at once.

## Internal selected-provider conformance example

`src/Examples/AgentFramework/HarnessHybridApp` demonstrates the stable
selected-provider seam with:

- generated tools;
- a non-Azure scripted provider;
- trusted workspace and session binding;
- Foundry-owned diagnostics/progress;
- exactly one function loop and telemetry owner; and
- an explicit assertion that Foundry hybrid compaction is disabled for that
  profile.

!!! warning "Contributor-only internal example"
    `HarnessHybridApp` is a non-packable friend assembly over internal
    composition types. The unsigned `InternalsVisibleTo` grant is not a
    security boundary and does not make those types a supported API. Consumers
    should choose a public path from the table at the start of this guide.

The task-defined name `HarnessHybridApp` refers to the selected-provider seam
where hybrid context can attach. The example itself uses the stable-only
profile and proves that compaction remains disabled; it does not claim to
execute hybrid compaction. For hybrid compaction through a supported public
API, use the complete-bundle path described above.

## Migration and compatibility

There is **no compatibility shim** for the former alpha Harness APIs. The
package family is alpha, so superseded APIs are replaced directly; update call
sites rather than expecting a forwarding type.

Adopting Harness is additive. Existing Foundry MAF agents keep working
unchanged, and nothing is deprecated by this release. The iterative loop, plain
Harness, and hybrid execution modes are all retained. See
[ADR-0010](adr/adr-0010-harness-execution-mode-retention.md).

Choose exactly one supported path:

| You want | Use |
|---|---|
| Plain Foundry MAF agents | The core package, unchanged |
| The upstream complete bundle | The optional Harness package |
| Foundry-owned iterative execution | The iterative loop |

Selected-provider composition is **internal and unsupported**. It is not a
public consumption path, and `InternalsVisibleTo` grants in this repository are
not a security boundary because the assemblies are unsigned.

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
[ADR-0007](adr/adr-0007-experimental-hybrid-context-compaction.md) (superseded
by [ADR-0011](adr/adr-0011-public-hybrid-context-compaction.md)), and
[ADR-0008](adr/adr-0008-optional-harness-bundle.md) for the architecture
decisions behind these boundaries.
