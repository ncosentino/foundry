namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness;

internal sealed record HarnessExecutionBindingCaptureResult(
    HarnessExecutionBindingStatus Status,
    HarnessExecutionBinding? Binding,
    string? Detail);
