using AutonomousPhasePipelineApp.Core;

using Microsoft.Agents.AI.Workflows;

namespace AutonomousPhasePipelineApp.Evaluation;

internal sealed class HostedDeliveryExecutor(
    ReferenceArtifactStore artifacts,
    IdempotentDeliverySink delivery,
    string runId) :
    Executor<ReferencePhaseArtifact, ReferencePipelineResult>(
        DeliveryExecutor.ExecutorId)
{
    private readonly DeliveryExecutor _inner =
        new(artifacts, delivery, runId);

    public override async ValueTask<ReferencePipelineResult> HandleAsync(
        ReferencePhaseArtifact message,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        using System.Diagnostics.Activity? activity =
            HostedEvaluationActivitySource.Source.StartActivity(
                "phase.delivery");
        ReferencePipelineResult result = await _inner.HandleAsync(
            message,
            context,
            cancellationToken);
        activity?.SetTag(
            "foundry.delivery.id",
            result.DeliveryId);
        activity?.SetStatus(
            System.Diagnostics.ActivityStatusCode.Ok);
        return result;
    }
}
