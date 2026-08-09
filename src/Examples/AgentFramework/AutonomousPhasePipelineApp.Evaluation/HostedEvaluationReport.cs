namespace AutonomousPhasePipelineApp.Evaluation;

internal sealed record HostedEvaluationReport(
    int SchemaVersion,
    string ProtocolVersion,
    string RunId,
    string CommitSha,
    string Model,
    int TrialCount,
    string EvidenceStrength,
    string Recommendation,
    string ExtractionRecommendation,
    DateTimeOffset GeneratedAtUtc,
    ProviderProbeResult ProviderProbe,
    HostedEvaluationBlockResult[] Blocks);
