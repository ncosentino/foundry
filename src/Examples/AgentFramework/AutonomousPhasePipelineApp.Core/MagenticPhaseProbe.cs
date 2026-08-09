using System.Collections.Concurrent;

using Microsoft.Agents.AI.Workflows;
using Microsoft.Agents.AI.Workflows.Specialized.Magentic;

namespace AutonomousPhasePipelineApp.Core;

internal sealed class MagenticPhaseProbe
{
    private readonly ConcurrentQueue<CheckpointInfo> _checkpoints = new();
    private readonly ConcurrentQueue<string> _failures = new();
    private readonly ConcurrentQueue<MagenticLedgerSnapshot> _ledgers = new();
    private readonly ConcurrentQueue<string> _plans = new();
    private readonly ConcurrentQueue<string> _replans = new();
    private readonly ConcurrentQueue<string> _warnings = new();
    private int _executionCount;
    private int _progressEventCount;

    internal int ExecutionCount => Volatile.Read(ref _executionCount);

    internal int ProgressEventCount => Volatile.Read(ref _progressEventCount);

    internal IReadOnlyList<CheckpointInfo> Checkpoints => _checkpoints.ToArray();

    internal IReadOnlyList<string> Failures => _failures.ToArray();

    internal IReadOnlyList<MagenticLedgerSnapshot> Ledgers => _ledgers.ToArray();

    internal IReadOnlyList<string> Plans => _plans.ToArray();

    internal IReadOnlyList<string> Replans => _replans.ToArray();

    internal IReadOnlyList<string> Warnings => _warnings.ToArray();

    internal void BeginExecution() =>
        Interlocked.Increment(ref _executionCount);

    internal void Record(WorkflowEvent workflowEvent)
    {
        switch (workflowEvent)
        {
            case MagenticPlanCreatedEvent plan:
                _plans.Enqueue(plan.FullTaskLedger.Text);
                break;
            case MagenticReplannedEvent replan:
                _replans.Enqueue(replan.FullTaskLedger.Text);
                break;
            case MagenticProgressLedgerUpdatedEvent:
                Interlocked.Increment(ref _progressEventCount);
                break;
            case WorkflowWarningEvent warning:
                _warnings.Enqueue(
                    warning.Data?.ToString() ?? "unknown warning");
                break;
            case SuperStepCompletedEvent
            {
                CompletionInfo.Checkpoint: { } checkpoint,
            }:
                _checkpoints.Enqueue(checkpoint);
                break;
        }
    }

    internal void RecordFailure(string failure) =>
        _failures.Enqueue(failure);

    internal void RecordLedgerSnapshot(
        MagenticLedgerSnapshot snapshot) =>
        _ledgers.Enqueue(snapshot);
}
