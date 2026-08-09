using Microsoft.Agents.AI.Workflows;

namespace AutonomousPhasePipelineApp.Core;

internal sealed class SynthesisArtifactBoundaryExecutor(
    ReferenceArtifactStore artifacts,
    string runId) :
    Executor<ReferencePhaseArtifact, ReferencePhaseArtifact>(ExecutorId)
{
    internal const string ExecutorId = "synthesis-artifact-boundary.v1";

    public override ValueTask<ReferencePhaseArtifact> HandleAsync(
        ReferencePhaseArtifact message,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        if (message.Outcome != ReferencePipelineOutcome.Completed)
        {
            return ValueTask.FromResult(message);
        }

        if (!ReferenceArtifactValidator.TryNormalizeSynthesis(
            message.CandidateContent,
            out string normalized,
            out string error))
        {
            return ValueTask.FromResult(
                ReferencePhaseArtifact.Failed(
                    message.Phase,
                    message.Ordinal,
                    message.Required,
                    error,
                    message.Inputs));
        }

        ReferenceArtifactReference reference = artifacts.Write(
            runId,
            normalized);
        return ValueTask.FromResult(
            ReferencePhaseArtifact.WithArtifact(
                message,
                reference));
    }
}
