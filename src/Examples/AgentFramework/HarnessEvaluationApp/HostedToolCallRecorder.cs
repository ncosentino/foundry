namespace HarnessEvaluationApp;

internal sealed class HostedToolCallRecorder
{
    private readonly object _gate = new();
    private readonly List<string> _toolNames = [];

    internal void Record(string toolName)
    {
        lock (_gate)
        {
            _toolNames.Add(toolName);
        }
    }

    internal IReadOnlyList<string> Snapshot()
    {
        lock (_gate)
        {
            return _toolNames.ToArray();
        }
    }
}
