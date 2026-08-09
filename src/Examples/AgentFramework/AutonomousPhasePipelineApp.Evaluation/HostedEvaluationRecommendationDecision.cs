namespace AutonomousPhasePipelineApp.Evaluation;

internal sealed record HostedEvaluationRecommendationDecision(
    HostedEvaluationRecommendationStatus Status,
    string Reason,
    int AdmissibleResults,
    int ExcludedResults);
