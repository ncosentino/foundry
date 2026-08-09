namespace AutonomousPhasePipelineApp.Evaluation;

internal sealed record HostedEvaluationTelemetrySnapshot(
    int ModelCalls,
    int ToolCalls,
    long InputTokens,
    long OutputTokens,
    long? CachedInputTokens,
    int ChildSessionCount,
    int ProviderFailures,
    string? ObservedModel);
