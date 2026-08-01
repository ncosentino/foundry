using Microsoft.Extensions.AI;

using NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Bundle;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Tests;

/// <summary>
/// Shared construction helpers for <see cref="FoundryHarnessAgentConfiguration"/> and
/// <see cref="FoundryHarnessFeatureSelections"/> test fixtures.
/// </summary>
internal static class HarnessBundleTestsHelpers
{
    internal static FoundryHarnessFeatureSelections AllFeaturesDisabled() =>
        new()
        {
            EnableWebSearch = false,
            EnableFileMemory = false,
            EnableAgentSkills = false,
            EnableToolAutoApproval = false,
            EnableApprovalNotRequiredFunctionBypassing = false,
            EnableApprovalResponseBinding = false,
            EnableOpenTelemetry = false,
            EnableTodoProvider = false,
            EnableAgentModeProvider = false,
            EnableCompaction = false,
            EnableHybridCompaction = false,
        };

    internal static FoundryHarnessFeatureSelections AllFeaturesEnabled() =>
        new()
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
            // Hybrid compaction cannot be enabled here: it fails closed without an explicit budget
            // and reducer, which this helper has no basis to choose for every caller.
            EnableHybridCompaction = false,
        };

    internal static FoundryHarnessAgentConfiguration CreateBaseline(
        FoundryHarnessFeatureSelections? features = null) =>
        new()
        {
            Id = null,
            Name = "test-agent",
            Description = null,
            Instructions = null,
            HarnessInstructionsOverride = null,
            ChatClient = new FakeHarnessChatClient(),
            Tools = [],
            Features = features ?? AllFeaturesDisabled(),
            ProgressAccessor = null,
            MaxContextWindowTokens = null,
            MaxOutputTokens = null,
            MaximumIterationsPerRequest = null,
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

    internal static FoundryHarnessAgentConfiguration CreateWithBuiltInToolProvider(
        FoundryHarnessFeature feature,
        IChatClient? chatClient = null)
    {
        var disabledFeatures = AllFeaturesDisabled();
        var configuration = feature switch
        {
            FoundryHarnessFeature.WebSearch => CreateBaseline(
                disabledFeatures with { EnableWebSearch = true }),
            FoundryHarnessFeature.TodoProvider => CreateBaseline(
                disabledFeatures with { EnableTodoProvider = true }),
            FoundryHarnessFeature.AgentModeProvider => CreateBaseline(
                disabledFeatures with { EnableAgentModeProvider = true }),
            FoundryHarnessFeature.FileMemory => CreateBaseline(
                disabledFeatures with { EnableFileMemory = true }) with
                {
                    FileMemoryStore = new InMemoryAgentFileStoreFake(),
                },
            FoundryHarnessFeature.FileAccess => CreateBaseline(disabledFeatures) with
            {
                FileAccessStore = new InMemoryAgentFileStoreFake(),
            },
            FoundryHarnessFeature.AgentSkills => CreateBaseline(
                disabledFeatures with { EnableAgentSkills = true }) with
                {
                    AgentSkillsSource = new FakeAgentSkillsSource(),
                },
            _ => throw new ArgumentOutOfRangeException(nameof(feature), feature, null),
        };

        return chatClient is null
            ? configuration
            : configuration with { ChatClient = chatClient };
    }
}
