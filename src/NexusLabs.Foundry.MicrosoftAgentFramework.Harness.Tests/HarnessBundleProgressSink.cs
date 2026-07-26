using System.Collections.Concurrent;

using NexusLabs.Foundry.MicrosoftAgentFramework.Progress;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Tests;

internal sealed class HarnessBundleProgressSink : IProgressSink
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
