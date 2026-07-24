namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Providers;

internal sealed record HarnessWebSearchCompositionGuardResult(
    HarnessWebSearchCompositionGuardStatus Status,
    string? Detail);
