namespace AutonomousPhasePipelineApp.Evaluation;

internal sealed record HostedEvaluationReport(
    int SchemaVersion,
    string ProtocolVersion,
    string RunId,
    string CommitSha,
    string Model,
    int TrialCount,
    string RunState,
    int TotalItems,
    int CompletedBlocks,
    int ScenarioFailureCount,
    int InfrastructureFailureCount,
    DateTimeOffset GeneratedAtUtc,
    HostedEvaluationRecommendationDecision Recommendation,
    HostedEvaluationArmSummary[] ArmSummaries,
    HostedEvaluationScenarioSummary[] ScenarioSummaries,
    HostedEvaluationBlockResult[] Blocks,
    HostedEvaluationItemFailure[] ItemFailures);
