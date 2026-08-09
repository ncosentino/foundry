namespace AutonomousPhasePipelineApp.Evaluation;

internal sealed record HostedEvaluationItemFailure(
    string CaseId,
    int TrialIndex,
    string Status,
    string? FailureCode,
    string? Message);
