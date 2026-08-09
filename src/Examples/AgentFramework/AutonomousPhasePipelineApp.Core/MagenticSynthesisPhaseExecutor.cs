using Microsoft.Agents.AI.Workflows;

namespace AutonomousPhasePipelineApp.Core;

internal sealed class MagenticSynthesisPhaseExecutor(
    ReferenceArtifactStore artifacts,
    string runId,
    Func<MagenticPhaseRuntime> runtimeFactory,
    ReferenceSynthesisBudget budget,
    int maxArtifactAttempts) :
    Executor<ReferencePhaseArtifact, ReferencePhaseArtifact>(
        SynthesisPhaseExecutor.ExecutorId)
{
    private readonly int _maxArtifactAttempts =
        ValidateMaxArtifactAttempts(maxArtifactAttempts);

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
        string baseTask = ReferenceSynthesisPrompt.Build(
            manifestReference,
            manifest);
        budget.BeginExecution();
        string task = baseTask;
        string? finalText = null;
        for (int attempt = 1; attempt <= _maxArtifactAttempts; attempt++)
        {
            MagenticPhaseRuntime runtime = runtimeFactory();
            if (!ReferenceEquals(runtime.Budget, budget))
            {
                throw new InvalidOperationException(
                    "The Magentic runtime did not use the synthesis execution budget.");
            }

            MagenticPhaseRunResult result =
                await MagenticPhaseRunner.RunAsync(
                    runtime,
                    task,
                    cancellationToken);
            if (!result.Succeeded ||
                string.IsNullOrWhiteSpace(result.FinalText))
            {
                return ReferencePhaseArtifact.Failed(
                    SynthesisPhaseExecutor.Phase,
                    ordinal: 200,
                    required: true,
                    result.FailureCode ?? "magentic-failed",
                    [manifestReference]);
            }

            finalText = result.FinalText;
            if (ReferenceArtifactValidator.TryValidateSynthesis(
                finalText,
                manifest,
                out string error))
            {
                break;
            }

            if (attempt < _maxArtifactAttempts)
            {
                task = ReferenceSynthesisPrompt.WithCorrection(
                    baseTask,
                    finalText,
                    error);
            }
        }

        return ReferencePhaseArtifact.Candidate(
            SynthesisPhaseExecutor.Phase,
            ordinal: 200,
            required: true,
            finalText!,
            manifestReference);
    }

    private static int ValidateMaxArtifactAttempts(
        int maxArtifactAttempts)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(
            maxArtifactAttempts);
        return maxArtifactAttempts;
    }
}
