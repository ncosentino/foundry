namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Providers;

internal sealed record HarnessTodoItemSnapshot(
    int Id,
    string Title,
    string? Description,
    bool IsComplete);
