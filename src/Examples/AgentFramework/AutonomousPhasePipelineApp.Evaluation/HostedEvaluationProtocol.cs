namespace AutonomousPhasePipelineApp.Evaluation;

internal sealed record HostedEvaluationProtocol
{
    internal const string Version = "autonomous-phase-eval-v2";

    internal required string Repository { get; init; }

    internal required string CommitSha { get; init; }

    internal required string Ref { get; init; }

    internal required string Model { get; init; }

    internal required int TrialCount { get; init; }

    internal required string OutputDirectory { get; init; }

    internal required string RunId { get; init; }

    internal required string DatasetName { get; init; }

    internal static HostedEvaluationProtocol FromEnvironment()
    {
        int trialCount = int.TryParse(
            Environment.GetEnvironmentVariable("EVAL_TRIALS"),
            out int configuredTrials)
            ? configuredTrials
            : 1;
        if (trialCount is < 1 or > 6)
        {
            throw new ArgumentOutOfRangeException(
                nameof(trialCount),
                trialCount,
                "EVAL_TRIALS must be between 1 and 6.");
        }

        string commitSha = Environment.GetEnvironmentVariable("EVAL_COMMIT_SHA")
            ?? Environment.GetEnvironmentVariable("GITHUB_SHA")
            ?? "local-uncommitted";
        string runId = Environment.GetEnvironmentVariable("EVAL_RUN_ID")
            ?? $"autonomous-phase-{commitSha[..Math.Min(12, commitSha.Length)]}-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}";
        return new HostedEvaluationProtocol
        {
            Repository = Environment.GetEnvironmentVariable("GITHUB_REPOSITORY")
                ?? "ncosentino/foundry",
            CommitSha = commitSha,
            Ref = Environment.GetEnvironmentVariable("EVAL_SOURCE_REF")
                ?? Environment.GetEnvironmentVariable("GITHUB_REF")
                ?? "local",
            Model = Environment.GetEnvironmentVariable("EVAL_MODEL")
                ?? "gpt-4.1",
            TrialCount = trialCount,
            OutputDirectory =
                Environment.GetEnvironmentVariable("EVAL_OUTPUT_DIRECTORY")
                ?? Path.Combine(
                    "artifacts",
                    "autonomous-phase-evaluation"),
            RunId = runId,
            DatasetName =
                Environment.GetEnvironmentVariable(
                    "EVAL_LANGFUSE_DATASET")
                ?? "foundry/autonomous-phase-synthesis",
        };
    }
}
