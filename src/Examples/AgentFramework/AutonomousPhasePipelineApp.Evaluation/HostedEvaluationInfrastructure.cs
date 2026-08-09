namespace AutonomousPhasePipelineApp.Evaluation;

internal sealed record HostedEvaluationInfrastructure(
    HostedEvaluationExecutionStatus ExecutionStatus,
    int ProviderFailureCount,
    int PhaseFailureCount,
    bool PostFaultOutputCaptured,
    HostedSemanticQualityStatus SemanticQualityStatus);
