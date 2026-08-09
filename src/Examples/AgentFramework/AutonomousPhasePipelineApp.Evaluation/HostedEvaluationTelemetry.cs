using Microsoft.Extensions.AI;

namespace AutonomousPhasePipelineApp.Evaluation;

internal sealed class HostedEvaluationTelemetry
{
    private readonly HashSet<string> _childAgents =
        new(StringComparer.Ordinal);
    private readonly object _sync = new();
    private long _cachedInputTokens;
    private int _cachedUsageObserved;
    private long _inputTokens;
    private int _childProviderFailures;
    private int _modelCalls;
    private long _outputTokens;
    private int _providerFailures;
    private int _toolCalls;
    private string? _lastTerminalText;
    private string? _observedModel;

    internal TaskCompletionSource FirstCallStarted { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    internal TaskCompletionSource FaultActivated { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    internal void RecordCallStarted(
        string agentId,
        bool isChild)
    {
        Interlocked.Increment(ref _modelCalls);
        if (isChild)
        {
            lock (_sync)
            {
                _childAgents.Add(agentId);
            }
        }

        FirstCallStarted.TrySetResult();
    }

    internal void RecordResponse(ChatResponse response)
    {
        Interlocked.Add(
            ref _toolCalls,
            response.Messages
                .SelectMany(message => message.Contents)
                .OfType<FunctionCallContent>()
                .Count());
        UsageDetails? usage = response.Usage;
        if (usage is not null)
        {
            Interlocked.Add(
                ref _inputTokens,
                usage.InputTokenCount ?? 0);
            Interlocked.Add(
                ref _outputTokens,
                usage.OutputTokenCount ?? 0);
            long? cached =
                usage.CachedInputTokenCount
                ?? usage.AdditionalCounts?.GetValueOrDefault(
                    "CachedInputTokens");
            if (cached is not null)
            {
                Interlocked.Exchange(ref _cachedUsageObserved, 1);
                Interlocked.Add(
                    ref _cachedInputTokens,
                    cached.Value);
            }
        }

        if (!string.IsNullOrWhiteSpace(response.ModelId))
        {
            lock (_sync)
            {
                _observedModel ??= response.ModelId;
            }
        }

        if (!response.Messages
                .SelectMany(message => message.Contents)
                .OfType<FunctionCallContent>()
                .Any() &&
            !string.IsNullOrWhiteSpace(response.Text))
        {
            lock (_sync)
            {
                _lastTerminalText = response.Text;
            }
        }
    }

    internal void RecordFailure(bool isChild)
    {
        Interlocked.Increment(ref _providerFailures);
        if (isChild)
        {
            Interlocked.Increment(ref _childProviderFailures);
        }
    }

    internal void RecordFaultActivated() =>
        FaultActivated.TrySetResult();

    internal HostedEvaluationTelemetrySnapshot Snapshot()
    {
        int childCount;
        string? observedModel;
        string? lastTerminalText;
        lock (_sync)
        {
            childCount = _childAgents.Count;
            observedModel = _observedModel;
            lastTerminalText = _lastTerminalText;
        }

        return new HostedEvaluationTelemetrySnapshot(
            ModelCalls: Volatile.Read(ref _modelCalls),
            ToolCalls: Volatile.Read(ref _toolCalls),
            InputTokens: Volatile.Read(ref _inputTokens),
            OutputTokens: Volatile.Read(ref _outputTokens),
            CachedInputTokens:
                Volatile.Read(ref _cachedUsageObserved) == 0
                    ? null
                    : Volatile.Read(ref _cachedInputTokens),
            ChildSessionCount: childCount,
            ChildFailureCount: Volatile.Read(ref _childProviderFailures),
            ProviderFailures: Volatile.Read(ref _providerFailures),
            ObservedModel: observedModel,
            LastTerminalText: lastTerminalText);
    }
}
