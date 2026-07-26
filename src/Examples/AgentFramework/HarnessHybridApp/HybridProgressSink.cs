using System.Collections.Concurrent;

using NexusLabs.Foundry.MicrosoftAgentFramework.Progress;

namespace HarnessHybridApp;

internal sealed class HybridProgressSink : IProgressSink
{
    private readonly ConcurrentQueue<IProgressEvent> _events = new();

    internal IReadOnlyList<IProgressEvent> Events => _events.ToArray();

    public ValueTask OnEventAsync(
        IProgressEvent progressEvent,
        CancellationToken cancellationToken)
    {
        _events.Enqueue(progressEvent);
        return ValueTask.CompletedTask;
    }
}
