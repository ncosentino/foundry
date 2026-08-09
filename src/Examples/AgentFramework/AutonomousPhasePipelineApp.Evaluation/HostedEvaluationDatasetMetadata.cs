namespace AutonomousPhasePipelineApp.Evaluation;

internal sealed record HostedEvaluationDatasetMetadata(
    string Split,
    string ScenarioFamily,
    bool PoolableForArmComparison,
    string FixtureDigest);
