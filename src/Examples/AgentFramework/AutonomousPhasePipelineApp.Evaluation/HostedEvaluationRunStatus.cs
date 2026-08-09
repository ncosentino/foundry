namespace AutonomousPhasePipelineApp.Evaluation;

internal sealed record HostedEvaluationRunStatus(
    int SchemaVersion,
    string RunId,
    string State,
    int CompletedBlocks,
    int TotalBlocks,
    DateTimeOffset UpdatedAtUtc);
