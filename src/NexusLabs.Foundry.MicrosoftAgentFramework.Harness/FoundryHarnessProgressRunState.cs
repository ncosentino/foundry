using NexusLabs.Foundry.MicrosoftAgentFramework.Progress;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Bundle;

internal sealed class FoundryHarnessProgressRunState(
    IProgressReporter reporter,
    string? parentAgentId,
    string agentName)
{
    private int _nextLlmCallSequence = -1;
    private int _toolCallCount;
    private long _inputTokens;
    private long _outputTokens;
    private long _totalTokens;

    internal IProgressReporter Reporter { get; } = reporter;

    internal string? ParentAgentId { get; } = parentAgentId;

    internal string AgentName { get; } = agentName;

    internal int ToolCallCount => Volatile.Read(ref _toolCallCount);

    internal long InputTokens => Interlocked.Read(ref _inputTokens);

    internal long OutputTokens => Interlocked.Read(ref _outputTokens);

    internal long TotalTokens => Interlocked.Read(ref _totalTokens);

    internal int NextLlmCallSequence() =>
        Interlocked.Increment(ref _nextLlmCallSequence);

    internal void IncrementToolCallCount() =>
        Interlocked.Increment(ref _toolCallCount);

    internal void AddUsage(
        long inputTokens,
        long outputTokens,
        long totalTokens)
    {
        Interlocked.Add(ref _inputTokens, inputTokens);
        Interlocked.Add(ref _outputTokens, outputTokens);
        Interlocked.Add(ref _totalTokens, totalTokens);
    }
}
