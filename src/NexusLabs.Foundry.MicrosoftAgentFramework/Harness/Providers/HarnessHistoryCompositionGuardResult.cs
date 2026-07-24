namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Providers;

internal sealed record HarnessHistoryCompositionGuardResult(
    HarnessHistoryCompositionGuardStatus Status,
    string? Detail);
