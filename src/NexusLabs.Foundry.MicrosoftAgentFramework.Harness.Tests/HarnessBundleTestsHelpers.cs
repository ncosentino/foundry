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
            MaxContextWindowTokens = null,
            MaxOutputTokens = null,
            MaximumIterationsPerRequest = null,
            FileAccessStore = null,
        };
}
