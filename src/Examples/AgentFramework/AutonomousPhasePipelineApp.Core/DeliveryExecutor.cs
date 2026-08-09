using Microsoft.Agents.AI.Workflows;

namespace AutonomousPhasePipelineApp.Core;

internal sealed class DeliveryExecutor(
    ReferenceArtifactStore artifacts,
    IdempotentDeliverySink delivery,
    string runId) :
    Executor<ReferencePhaseArtifact, ReferencePipelineResult>(ExecutorId)
{
    internal const string ExecutorId = "deterministic-delivery.v1";

    public override ValueTask<ReferencePipelineResult> HandleAsync(
        ReferencePhaseArtifact message,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        ReferenceArtifactReference manifestReference = message.Inputs
            .Single();
        ReferenceArtifactManifest manifest = artifacts.GetManifest(
            manifestReference,
            runId);
        ReferencePipelineOutcome outcome =
            manifest.Outcome == ReferencePipelineOutcome.Failed ||
            message.Outcome is ReferencePipelineOutcome.Failed
                or ReferencePipelineOutcome.Skipped
                ? ReferencePipelineOutcome.Failed
                : manifest.Outcome;
        var candidate = new ReferencePipelineResult(
            runId,
            outcome,
            $"reference-delivery/{runId}",
            manifestReference,
            message,
            manifest.Branches,
            manifest.Gaps);
        return ValueTask.FromResult(delivery.Publish(candidate));
    }
}
