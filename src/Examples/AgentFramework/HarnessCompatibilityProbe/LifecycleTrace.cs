namespace HarnessCompatibilityProbe;

internal sealed class LifecycleTrace
{
    private readonly object _gate = new();
    private readonly List<string> _entries = [];

    internal IReadOnlyList<string> Entries
    {
        get
        {
            lock (_gate)
            {
                return [.. _entries];
            }
        }
    }

    internal void Add(string entry)
    {
        lock (_gate)
        {
            _entries.Add(entry);
        }
    }
}
