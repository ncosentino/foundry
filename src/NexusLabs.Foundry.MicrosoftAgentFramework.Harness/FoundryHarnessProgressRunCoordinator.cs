namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Bundle;

internal sealed class FoundryHarnessProgressRunCoordinator
{
    private readonly AsyncLocal<FoundryHarnessProgressRunState?> _current = new();

    internal FoundryHarnessProgressRunState? Current => _current.Value;

    internal FoundryHarnessProgressRunState? Enter(
        FoundryHarnessProgressRunState state)
    {
        var previous = _current.Value;
        _current.Value = state;
        return previous;
    }

    internal void Restore(
        FoundryHarnessProgressRunState? previous)
    {
        _current.Value = previous;
    }
}
