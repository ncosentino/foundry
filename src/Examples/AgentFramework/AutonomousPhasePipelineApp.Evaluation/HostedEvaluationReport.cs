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
    int ContractFailureCount,
    int InfrastructureFailureCount,
    string EvidenceStrength,
    string Recommendation,
    string ExtractionRecommendation,
    DateTimeOffset GeneratedAtUtc,
    ProviderProbeResult ProviderProbe,
    HostedEvaluationBlockResult[] Blocks,
    HostedEvaluationItemFailure[] ItemFailures);
