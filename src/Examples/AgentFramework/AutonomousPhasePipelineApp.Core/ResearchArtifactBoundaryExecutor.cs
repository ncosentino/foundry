using Microsoft.Agents.AI.Workflows;

namespace AutonomousPhasePipelineApp.Core;

internal sealed class ResearchArtifactBoundaryExecutor(
    ReferenceArtifactStore artifacts,
    string runId) :
    Executor<ReferencePhaseArtifact, ReferencePhaseArtifact>(ExecutorId)
{
    internal const string ExecutorId = "research-artifact-boundary.v1";

    public override ValueTask<ReferencePhaseArtifact> HandleAsync(
        ReferencePhaseArtifact message,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        if (message.Outcome != ReferencePipelineOutcome.Completed)
        {
            throw new InvalidOperationException(
                $"Research artifact validation failed: {message.Error ?? message.Outcome.ToString()}.");
        }

        if (!ReferenceArtifactValidator.TryValidateResearch(
            message.CandidateContent,
            out string error))
        {
            throw new InvalidOperationException(
                $"Research artifact validation failed: {error}.");
        }

        ReferenceArtifactReference reference = artifacts.Write(
            runId,
            message.CandidateContent!);
        return ValueTask.FromResult(
            ReferencePhaseArtifact.WithArtifact(
                message,
                reference));
    }
}
