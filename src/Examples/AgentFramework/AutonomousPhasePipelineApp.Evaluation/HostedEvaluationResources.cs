namespace AutonomousPhasePipelineApp.Evaluation;

internal sealed record HostedEvaluationResources(
    int ProviderCallLimit,
    int ProviderCallsUsed,
    int ModelCalls,
    int ToolCalls,
    long InputTokens,
    long OutputTokens,
    long? CachedInputTokens,
    int ChildSessionCount,
    int ChildFailureCount,
    int CheckpointCount,
    int RestoreEventCount,
    int PlanCount,
    int ReplanCount,
    int ProgressEventCount,
    long DurationMilliseconds);
