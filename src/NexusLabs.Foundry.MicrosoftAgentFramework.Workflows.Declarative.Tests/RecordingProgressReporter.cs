using NexusLabs.Foundry.MicrosoftAgentFramework.Progress;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Workflows.Declarative.Tests;

/// <summary>
/// An in-memory progress reporter that records everything reported to it.
/// </summary>
internal sealed class RecordingProgressReporter(string workflowId) : IProgressReporter
{
    private readonly List<IProgressEvent> _events = [];
    private long _sequence;

    public string WorkflowId { get; } = workflowId;

    public string? AgentId => null;

    public int Depth => 0;

    public IReadOnlyList<IProgressEvent> Events
    {
        get
        {
            lock (_events)
            {
                return [.. _events];
            }
        }
    }

    public void Report(IProgressEvent progressEvent)
    {
        lock (_events)
        {
            _events.Add(progressEvent);
        }
    }

    public IProgressReporter CreateChild(string agentId) => this;

    public long NextSequence() => Interlocked.Increment(ref _sequence);
}
