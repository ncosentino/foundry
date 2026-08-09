namespace AutonomousPhasePipelineApp.Evaluation;

internal sealed record HostedEvaluationArmSummary(
    HostedEvaluationArm Arm,
    int ApplicableResults,
    int NonPoolableResults,
    int AdmissibleResults,
    int PassedResults,
    double? PassRate,
    int ProviderCalls,
    long InputTokens,
    long OutputTokens,
    long DurationMilliseconds);
