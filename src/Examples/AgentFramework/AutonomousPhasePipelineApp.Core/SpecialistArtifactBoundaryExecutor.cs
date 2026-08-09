using Microsoft.Agents.AI.Workflows;

namespace AutonomousPhasePipelineApp.Core;

internal sealed class SpecialistArtifactBoundaryExecutor(
    ReferenceBranchDefinition branch,
    ReferenceArtifactStore artifacts,
    string runId) :
    Executor<ReferencePhaseArtifact, ReferencePhaseArtifact>(
        $"specialist-{branch.Phase}-artifact-boundary.v1")
{
    public override ValueTask<ReferencePhaseArtifact> HandleAsync(
        ReferencePhaseArtifact message,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        if (message.Outcome != ReferencePipelineOutcome.Completed)
        {
            return ValueTask.FromResult(message);
        }

        if (!ReferenceArtifactValidator.TryValidateSpecialist(
            message.CandidateContent,
            out string error))
        {
            return ValueTask.FromResult(
                ReferencePhaseArtifact.Failed(
                    branch.Phase,
                    branch.Ordinal,
                    branch.Required,
                    error,
                    message.Inputs));
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
