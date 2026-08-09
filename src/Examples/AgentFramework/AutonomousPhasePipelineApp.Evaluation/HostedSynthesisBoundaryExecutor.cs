using AutonomousPhasePipelineApp.Core;

using Microsoft.Agents.AI.Workflows;

namespace AutonomousPhasePipelineApp.Evaluation;

internal sealed class HostedSynthesisBoundaryExecutor(
    ReferenceArtifactStore artifacts,
    string runId) :
    Executor<ReferencePhaseArtifact, ReferencePhaseArtifact>(
        SynthesisArtifactBoundaryExecutor.ExecutorId)
{
    private readonly SynthesisArtifactBoundaryExecutor _inner =
        new(artifacts, runId);

    public override async ValueTask<ReferencePhaseArtifact> HandleAsync(
        ReferencePhaseArtifact message,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        using System.Diagnostics.Activity? activity =
            HostedEvaluationActivitySource.Source.StartActivity(
                "phase.artifact-boundary");
        ReferencePhaseArtifact result = await _inner.HandleAsync(
            message,
            context,
            cancellationToken);
        activity?.SetTag(
            "foundry.artifact.outcome",
            result.Outcome.ToString());
        activity?.SetStatus(
            result.Outcome == ReferencePipelineOutcome.Completed
                ? System.Diagnostics.ActivityStatusCode.Ok
                : System.Diagnostics.ActivityStatusCode.Error,
            result.Error);
        return result;
    }
}
