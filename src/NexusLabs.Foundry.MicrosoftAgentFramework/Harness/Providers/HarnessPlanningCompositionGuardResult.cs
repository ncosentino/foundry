namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Providers;

internal sealed record HarnessPlanningCompositionGuardResult(
    HarnessPlanningCompositionGuardStatus Status,
    string? Detail);
