namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness;

internal sealed record HarnessCompositionGuardResult(
    HarnessCompositionGuardStatus Status,
    string? Detail);
