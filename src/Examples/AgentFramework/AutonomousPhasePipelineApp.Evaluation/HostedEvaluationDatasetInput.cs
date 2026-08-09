namespace AutonomousPhasePipelineApp.Evaluation;

internal sealed record HostedEvaluationDatasetInput(
    int SchemaVersion,
    string ProtocolVersion,
    string CaseId,
    HostedEvaluationScenario Scenario,
    int MaxProviderCalls,
    int MaxArtifactAttempts);
