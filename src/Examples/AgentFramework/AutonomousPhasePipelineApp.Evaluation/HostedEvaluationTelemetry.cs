using Microsoft.Extensions.AI;

namespace AutonomousPhasePipelineApp.Evaluation;

internal sealed class HostedEvaluationTelemetry
{
    private readonly HashSet<string> _childAgents =
        new(StringComparer.Ordinal);
    private readonly HashSet<string> _completedChildAgents =
        new(StringComparer.Ordinal);
    private readonly object _sync = new();
    private long _cachedInputTokens;
    private int _cachedUsageObserved;
    private long _inputTokens;
    private int _childProviderFailures;
    private int _completedChildSessionCountAtFault;
    private int _completedModelCalls;
    private int _completedModelCallsAtFault;
    private long _boundaryOutputOrdinal;
    private long _eventOrdinal;
    private long _faultActivationOrdinal;
    private int _faultActivated;
    private HostedEvaluationAgentRole? _faultRole;
    private int _modelCalls;
    private long _outputTokens;
    private int _providerFailures;
    private int _toolCalls;
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

    internal void RecordResponse(
        ChatResponse response,
        string agentId,
        bool isChild)
    {
        Interlocked.Increment(ref _completedModelCalls);
        if (isChild)
        {
            lock (_sync)
            {
                _completedChildAgents.Add(agentId);
            }
        }

        _ = Interlocked.Increment(ref _eventOrdinal);
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
    }

    internal void RecordFailure(bool isChild)
    {
        Interlocked.Increment(ref _providerFailures);
        if (isChild)
        {
            Interlocked.Increment(ref _childProviderFailures);
        }
    }

    internal void RecordFaultActivated(
        HostedEvaluationAgentRole role)
    {
        if (Interlocked.Exchange(ref _faultActivated, 1) != 0)
        {
            return;
        }

        lock (_sync)
        {
            _faultRole = role;
            _completedChildSessionCountAtFault =
                _completedChildAgents.Count;
        }

        _completedModelCallsAtFault =
            Volatile.Read(ref _completedModelCalls);
        _faultActivationOrdinal =
            Interlocked.Increment(ref _eventOrdinal);
        FaultActivated.TrySetResult();
    }

    internal void RecordBoundaryOutput() =>
        _boundaryOutputOrdinal =
            Interlocked.Increment(ref _eventOrdinal);

    internal HostedEvaluationTelemetrySnapshot Snapshot()
    {
        int childCount;
        string? observedModel;
        lock (_sync)
        {
            childCount = _childAgents.Count;
            observedModel = _observedModel;
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
            CompletedModelCalls:
                Volatile.Read(ref _completedModelCalls),
            FaultActivated: Volatile.Read(ref _faultActivated) != 0,
            FaultRole: _faultRole,
            CompletedModelCallsAtFault:
                Volatile.Read(ref _completedModelCallsAtFault),
            CompletedChildSessionCountAtFault:
                Volatile.Read(
                    ref _completedChildSessionCountAtFault),
            FaultActivationOrdinal:
                Volatile.Read(ref _faultActivationOrdinal),
            BoundaryOutputOrdinal:
                Volatile.Read(ref _boundaryOutputOrdinal),
            ObservedModel: observedModel);
    }
}
