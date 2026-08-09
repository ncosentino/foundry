namespace AutonomousPhasePipelineApp.Evaluation;

internal sealed record HostedEvaluationTelemetrySnapshot(
    int ModelCalls,
    int ToolCalls,
    long InputTokens,
    long OutputTokens,
    long? CachedInputTokens,
    int ChildSessionCount,
    int ChildFailureCount,
    int ProviderFailures,
    int CompletedModelCalls,
    bool FaultActivated,
    HostedEvaluationAgentRole? FaultRole,
    int CompletedModelCallsAtFault,
    int CompletedChildSessionCountAtFault,
    long FaultActivationOrdinal,
    long BoundaryOutputOrdinal,
    string? ObservedModel);
