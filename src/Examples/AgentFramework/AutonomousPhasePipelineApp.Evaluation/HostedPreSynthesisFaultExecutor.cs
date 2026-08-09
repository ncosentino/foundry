using AutonomousPhasePipelineApp.Core;

using Microsoft.Agents.AI.Workflows;

namespace AutonomousPhasePipelineApp.Evaluation;

internal sealed class HostedPreSynthesisFaultExecutor(
    bool failFirstAttempt) :
    Executor<ReferencePhaseArtifact, ReferencePhaseArtifact>(ExecutorId)
{
    internal const string ExecutorId = "hosted-pre-synthesis-fault.v1";

    private int _callCount;

    public override ValueTask<ReferencePhaseArtifact> HandleAsync(
        ReferencePhaseArtifact message,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        using System.Diagnostics.Activity? activity =
            HostedEvaluationActivitySource.Source.StartActivity(
                "phase.fault-boundary");
        if (failFirstAttempt &&
            Interlocked.Increment(ref _callCount) == 1)
        {
            activity?.SetStatus(
                System.Diagnostics.ActivityStatusCode.Error,
                "injected-first-attempt-failure");
            throw new HttpRequestException(
                "Injected failure after the accepted manifest checkpoint.");
        }

        activity?.SetStatus(
            System.Diagnostics.ActivityStatusCode.Ok);
        return ValueTask.FromResult(message);
    }
}
