using Microsoft.Agents.AI.Workflows;

namespace AutonomousPhasePipelineApp.Core;

internal sealed class MagenticSynthesisPhaseExecutor(
    ReferenceArtifactStore artifacts,
    string runId,
    Func<MagenticPhaseRuntime> runtimeFactory) :
    Executor<ReferencePhaseArtifact, ReferencePhaseArtifact>(
        SynthesisPhaseExecutor.ExecutorId)
{
    public override async ValueTask<ReferencePhaseArtifact> HandleAsync(
        ReferencePhaseArtifact message,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        if (message.Artifact is not { } manifestReference)
        {
            return ReferencePhaseArtifact.Skipped(
                SynthesisPhaseExecutor.Phase,
                ordinal: 200,
                required: true,
                "manifest-missing");
        }

        if (message.Outcome == ReferencePipelineOutcome.Failed)
        {
            return ReferencePhaseArtifact.Skipped(
                SynthesisPhaseExecutor.Phase,
                ordinal: 200,
                required: true,
                "blocked-by-required-branch",
                manifestReference);
        }

        ReferenceArtifactManifest manifest = artifacts.GetManifest(
            manifestReference,
            runId);
        string task = ReferenceSynthesisPrompt.Build(
            manifestReference,
            manifest);
        MagenticPhaseRunResult result =
            await MagenticPhaseRunner.RunAsync(
                runtimeFactory(),
                task,
                cancellationToken);
        if (!result.Succeeded || string.IsNullOrWhiteSpace(result.FinalText))
        {
            return ReferencePhaseArtifact.Failed(
                SynthesisPhaseExecutor.Phase,
                ordinal: 200,
                required: true,
                result.FailureCode ?? "magentic-failed",
                [manifestReference]);
        }

        return ReferencePhaseArtifact.Candidate(
            SynthesisPhaseExecutor.Phase,
            ordinal: 200,
            required: true,
            result.FinalText,
            manifestReference);
    }
}
