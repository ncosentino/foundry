using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

using NexusLabs.Foundry.MicrosoftAgentFramework.Diagnostics;
using NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Bundle;
using NexusLabs.Foundry.MicrosoftAgentFramework.Testing;
using NexusLabs.Foundry.MicrosoftAgentFramework.Workspace;

namespace AotHarnessApp;

/// <summary>
/// Proves that every optional Harness bundle capability reachable through
/// <see cref="FoundryHarnessFeatureSelections"/> initializes under NativeAOT.
/// </summary>
/// <remarks>
/// The minimum profile scenario deliberately disables every optional feature, so it never
/// exercised the provider, skills, file, todo, mode, approval, telemetry, or compaction wiring.
/// This scenario enables all of them at once and asserts each one reports an enabled effective
/// state, which is the evidence that its types survived trimming and constructed at runtime.
/// </remarks>
internal sealed class AotHarnessCapabilityScenario : IHarnessScenario
{
    internal const string OutputPath = "capabilities/result.txt";
    internal const string ExpectedWorkspaceContent = "aot-capabilities";

    private static readonly FoundryHarnessAgentFactory Factory = new();

    private readonly string _fileRoot = Path.Combine(
        Path.GetTempPath(),
        $"aot-harness-capabilities-{Guid.NewGuid():N}");

    internal FoundryHarnessEffectiveDefaults? EffectiveDefaults { get; private set; }

    public string Name => "capability-aot-harness";

    public string Description =>
        "Proves every optional Harness bundle capability initializes under NativeAOT.";

    public string SystemPrompt =>
        "Use the generated WriteWorkspace tool exactly once.";

    public string UserPrompt =>
        "Write the deterministic AOT capability proof value.";

    public IReadOnlyList<Type> GeneratedFunctionTypes { get; } =
        [typeof(AotHarnessTool)];

    public void SeedWorkspace(IWorkspace workspace)
    {
        workspace.TryWriteFile("capabilities/seed.txt", "seeded");
    }

    public AIAgent CreateAgent(HarnessScenarioAgentContext context)
    {
        var function = context.GeneratedTools.Single();
        Directory.CreateDirectory(_fileRoot);
        var configuration = new FoundryHarnessAgentConfiguration
        {
            Id = "capability-aot-harness-agent",
            Name = "Capability AOT Harness Agent",
            Description = Description,
            Instructions = SystemPrompt,
            HarnessInstructionsOverride = string.Empty,
            ChatClient = new AotHarnessScriptedChatClient(
                function.Name,
                ExpectedWorkspaceContent),
            Tools = [.. context.GeneratedTools],
            Features = new FoundryHarnessFeatureSelections
            {
                EnableWebSearch = true,
                EnableFileMemory = true,
                EnableAgentSkills = true,
                EnableToolAutoApproval = true,
                EnableApprovalNotRequiredFunctionBypassing = true,
                EnableApprovalResponseBinding = true,
                EnableOpenTelemetry = true,
                EnableTodoProvider = true,
                EnableAgentModeProvider = true,
                EnableCompaction = true,
            },
            ProgressAccessor = null,
            // Compaction requires either an explicit strategy or both token budgets. Supplying
            // the budgets exercises the upstream default strategy rather than a Foundry one.
            MaxContextWindowTokens = 16000,
            MaxOutputTokens = 1024,
            MaximumIterationsPerRequest = 4,
            FileAccessStore = new FileSystemAgentFileStore(_fileRoot),
            FileAccessProviderOptions = null,
            ChatHistoryProvider = null,
            FileMemoryStore = new FileSystemAgentFileStore(_fileRoot),
            AgentSkillsSource = null,
            ToolApprovalAgentOptions = null,
            AgentModeProviderOptions = null,
            CompactionStrategy = null,
            OpenTelemetrySourceName = "aot-harness-capabilities",
            AdditionalContextProviders = [],
        };
        EffectiveDefaults = Factory.DescribeEffectiveDefaults(configuration);
        return Factory.Create(configuration, context.Services);
    }

    public void Verify(
        IWorkspace workspace,
        IAgentRunDiagnostics? diagnostics)
    {
        if (!workspace.FileExists("capabilities/seed.txt"))
        {
            throw new ScenarioVerificationException(
                Name,
                "The seeded workspace artifact was missing.");
        }
    }

    public void VerifyHarness(HarnessScenarioVerificationContext context)
    {
        if (context.ExecutionError is not null)
        {
            throw new ScenarioVerificationException(
                Name,
                $"Harness execution failed: {context.ExecutionError.Message}");
        }

        if (context.Session is null)
        {
            throw new ScenarioVerificationException(Name, "No MAF session was created.");
        }

        if (EffectiveDefaults is null)
        {
            throw new ScenarioVerificationException(
                Name,
                "The effective-default report was unavailable.");
        }

        foreach (var feature in EnabledFeatures)
        {
            var disposition = EffectiveDefaults.GetDisposition(feature);
            if (disposition.EffectiveState == FoundryHarnessFeatureEffectiveState.Disabled)
            {
                throw new ScenarioVerificationException(
                    Name,
                    $"Feature '{feature}' was requested but reported a disabled effective state.");
            }
        }

        foreach (var feature in UnreachableFeatures)
        {
            var disposition = EffectiveDefaults.GetDisposition(feature);
            if (disposition.EffectiveState != FoundryHarnessFeatureEffectiveState.Disabled ||
                string.IsNullOrWhiteSpace(disposition.Limitation))
            {
                throw new ScenarioVerificationException(
                    Name,
                    $"Feature '{feature}' is expected to stay disabled with a recorded limitation " +
                    "explaining that the bundle does not expose it.");
            }
        }
    }

    internal void Cleanup()
    {
        if (Directory.Exists(_fileRoot))
        {
            Directory.Delete(_fileRoot, recursive: true);
        }
    }

    private static IEnumerable<FoundryHarnessFeature> EnabledFeatures =>
    [
        FoundryHarnessFeature.WebSearch,
        FoundryHarnessFeature.FileMemory,
        FoundryHarnessFeature.FileAccess,
        FoundryHarnessFeature.AgentSkills,
        FoundryHarnessFeature.ToolAutoApproval,
        FoundryHarnessFeature.ApprovalNotRequiredFunctionBypassing,
        FoundryHarnessFeature.ApprovalResponseBinding,
        FoundryHarnessFeature.OpenTelemetry,
        FoundryHarnessFeature.TodoProvider,
        FoundryHarnessFeature.AgentModeProvider,
        FoundryHarnessFeature.Compaction,
    ];

    private static IEnumerable<FoundryHarnessFeature> UnreachableFeatures =>
    [
        FoundryHarnessFeature.BackgroundAgents,
        FoundryHarnessFeature.LoopEvaluation,
    ];
}
