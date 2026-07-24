namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness;

internal sealed record HarnessExecutionBindingValidationResult(
    HarnessExecutionBindingStatus Status,
    string? Detail);
