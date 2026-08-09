using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

namespace AutonomousPhasePipelineApp.Core;

internal static class MagenticPhaseRunner
{
    internal static async Task<MagenticPhaseRunResult> RunAsync(
        MagenticPhaseRuntime runtime,
        string task,
        CancellationToken cancellationToken)
    {
        int initialLedgerCount = runtime.Probe.Ledgers.Count;
        runtime.Probe.BeginExecution();
        CheckpointManager checkpoints = CheckpointManager.CreateInMemory();
        var environment = InProcessExecution.Lockstep.WithCheckpointing(
            checkpoints);
        await using StreamingRun run = await environment.RunStreamingAsync(
            runtime.Workflow,
            new List<ChatMessage>
            {
                new(ChatRole.User, task),
            },
            $"phase-local-magentic:{Guid.NewGuid():N}",
            cancellationToken);
        bool sent = await run.TrySendMessageAsync(
            new TurnToken(emitEvents: true));
        if (!sent)
        {
            return new MagenticPhaseRunResult(
                Succeeded: false,
                FinalText: null,
                FailureCode: "turn-token-rejected");
        }

        using CancellationTokenRegistration registration =
            cancellationToken.Register(() => _ = run.CancelRunAsync());
        string? finalText = null;
        bool invalidSpeaker = false;
        bool pendingReview = false;
        string? failure = null;
        await foreach (WorkflowEvent workflowEvent in run.WatchStreamAsync(
            blockOnPendingRequest: false,
            cancellationToken))
        {
            runtime.Probe.Record(workflowEvent);
            switch (workflowEvent)
            {
                case WorkflowWarningEvent warning:
                    invalidSpeaker |= warning.Data?.ToString()?.Contains(
                        "Invalid next speaker",
                        StringComparison.Ordinal) == true;
                    break;
                case RequestInfoEvent:
                    pendingReview = true;
                    break;
                case WorkflowOutputEvent output
                    when output.Is<List<ChatMessage>>():
                    finalText = output
                        .As<List<ChatMessage>>()?
                        .LastOrDefault()?
                        .Text;
                    break;
                case WorkflowErrorEvent error:
                    failure = error.Exception?.GetType().Name
                        ?? "workflow-error";
                    runtime.Probe.RecordFailure(
                        error.Exception?.ToString() ?? failure);
                    break;
                case ExecutorFailedEvent error:
                    failure = error.Data?.GetType().Name
                        ?? "executor-failed";
                    runtime.Probe.RecordFailure(
                        error.Data?.ToString() ?? failure);
                    break;
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (failure is not null)
        {
            return new MagenticPhaseRunResult(
                Succeeded: false,
                FinalText: finalText,
                FailureCode: failure);
        }

        if (pendingReview)
        {
            return new MagenticPhaseRunResult(
                Succeeded: false,
                FinalText: finalText,
                FailureCode: "plan-review-required");
        }

        if (invalidSpeaker)
        {
            return new MagenticPhaseRunResult(
                Succeeded: false,
                FinalText: finalText,
                FailureCode: "invalid-next-speaker");
        }

        MagenticLedgerSnapshot? latestLedger = runtime.Probe.Ledgers
            .Skip(initialLedgerCount)
            .LastOrDefault();
        if (latestLedger?.IsRequestSatisfied != true)
        {
            return new MagenticPhaseRunResult(
                Succeeded: false,
                FinalText: finalText,
                FailureCode: "request-not-satisfied");
        }

        if (string.IsNullOrWhiteSpace(finalText))
        {
            return new MagenticPhaseRunResult(
                Succeeded: false,
                FinalText: finalText,
                FailureCode: "missing-final-output");
        }

        return new MagenticPhaseRunResult(
            Succeeded: true,
            FinalText: finalText,
            FailureCode: null);
    }
}
