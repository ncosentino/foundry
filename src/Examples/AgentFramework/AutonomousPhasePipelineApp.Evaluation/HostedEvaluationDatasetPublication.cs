namespace AutonomousPhasePipelineApp.Evaluation;

internal sealed record HostedEvaluationDatasetPublication(
    string DatasetName,
    int ExpectedItems,
    int ObservedItems,
    DateTimeOffset? Version,
    bool ReadBackVerified);
