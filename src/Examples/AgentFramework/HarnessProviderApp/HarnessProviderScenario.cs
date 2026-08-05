using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

using NexusLabs.Foundry.MicrosoftAgentFramework.Diagnostics;
using NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Bundle;
using NexusLabs.Foundry.MicrosoftAgentFramework.Testing;
using NexusLabs.Foundry.MicrosoftAgentFramework.Workspace;

namespace HarnessProviderApp;

/// <summary>
/// One Harness scenario whose only variable is the chat provider. Everything else — tools,
/// workspace, session, and bundle configuration — is identical, so running it against the
/// scripted provider and then against a real model isolates provider behavior.
/// </summary>
internal sealed class HarnessProviderScenario(
    IChatClient chatClient,
    bool enableWebSearch) : IHarnessScenario
{
    private static readonly FoundryHarnessAgentFactory Factory = new();

    internal FoundryHarnessEffectiveDefaults? EffectiveDefaults { get; private set; }

    public string Name => "harness-provider";

    public string Description =>
        "Runs one Harness agent against a configurable chat provider.";

    public string SystemPrompt =>
        "You are a concise assistant with workspace tools. " +
        "Use write_note to persist your answer, then use read_note to confirm what you wrote.";

    public string UserPrompt =>
        $"Write a one-sentence summary of what the Foundry Harness bundle does to " +
        $"'{HarnessProviderRun.NotePath}', then confirm it.";

    public IReadOnlyList<Type> GeneratedFunctionTypes { get; } =
        [typeof(HarnessProviderTools)];

    public void SeedWorkspace(IWorkspace workspace)
    {
        workspace.TryWriteFile("notes/.keep", string.Empty);
    }

    public AIAgent CreateAgent(HarnessScenarioAgentContext context)
    {
        var configuration = new FoundryHarnessAgentConfiguration
        {
            Id = "harness-provider-agent",
            Name = "Harness Provider Agent",
            Description = Description,
            Instructions = SystemPrompt,
            HarnessInstructionsOverride = null,
            ChatClient = chatClient,
            Tools = [.. context.GeneratedTools],
            Features = new FoundryHarnessFeatureSelections
            {
                // Web search is a hosted tool: only a provider that supports it can execute the
                // declaration, so it stays opt-in through configuration.
                EnableWebSearch = enableWebSearch,
                EnableFileMemory = false,
                EnableAgentSkills = false,
                EnableToolAutoApproval = true,
                EnableApprovalNotRequiredFunctionBypassing = false,
                EnableApprovalResponseBinding = false,
                EnableOpenTelemetry = false,
                EnableTodoProvider = true,
                EnableAgentModeProvider = false,
                EnableCompaction = false,
                EnableHybridCompaction = false,
            },
            ProgressAccessor = null,
            MaxContextWindowTokens = null,
            MaxOutputTokens = 800,
            MaximumIterationsPerRequest = 6,
            FileAccessStore = null,
            FileAccessProviderOptions = null,
            ChatHistoryProvider = null,
            FileMemoryStore = null,
            AgentSkillsSource = null,
            ToolApprovalAgentOptions = null,
            AgentModeProviderOptions = null,
            CompactionStrategy = null,
            HybridCompactionOptions = null,
            OpenTelemetrySourceName = null,
            AdditionalContextProviders = [],
        };
        EffectiveDefaults = Factory.DescribeEffectiveDefaults(configuration);
        return Factory.Create(configuration, context.Services);
    }

    public void Verify(
        IWorkspace workspace,
        IAgentRunDiagnostics? diagnostics)
    {
        // A real model decides for itself how to answer, so the scenario asserts only that the
        // agent used its workspace tool at all. Stricter output assertions belong in the
        // deterministic AOT scenarios, not here.
        if (!workspace.FileExists(HarnessProviderRun.NotePath))
        {
            throw new ScenarioVerificationException(
                Name,
                $"The agent did not write '{HarnessProviderRun.NotePath}' using its workspace tool.");
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
    }
}
