namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Providers;

internal sealed record HarnessApprovalCompositionGuardResult(
    HarnessApprovalCompositionGuardStatus Status,
    string? Detail);
