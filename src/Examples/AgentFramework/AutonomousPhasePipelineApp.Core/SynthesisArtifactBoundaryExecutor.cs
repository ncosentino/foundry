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

        if (message.Inputs.Length != 1)
        {
            throw new InvalidOperationException(
                "Synthesis artifact did not reference exactly one accepted manifest.");
        }

        ReferenceArtifactManifest manifest = artifacts.GetManifest(
            message.Inputs[0],
            runId);
        if (!ReferenceArtifactValidator.TryValidateSynthesis(
            message.CandidateContent,
            manifest,
            out string error))
        {
            string[] gaps =
            [
                .. manifest.Gaps,
                $"{message.Phase}:{error}",
            ];
            return ValueTask.FromResult(
                ReferencePhaseArtifact.Failed(
                    message.Phase,
                    message.Ordinal,
                    message.Required,
                    error,
                    message.Inputs,
                    gaps));
        }

        ReferenceArtifactReference reference = artifacts.Write(
            runId,
            message.CandidateContent!);
        return ValueTask.FromResult(
            ReferencePhaseArtifact.WithArtifact(
                message,
                reference,
                ReferencePipelineOutcome.Completed,
                manifest.Gaps));
    }
}
