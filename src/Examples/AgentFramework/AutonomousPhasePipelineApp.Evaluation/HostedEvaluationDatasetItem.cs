namespace AutonomousPhasePipelineApp.Evaluation;

internal sealed record HostedEvaluationDatasetItem(
    string Id,
    HostedEvaluationCase Case,
    HostedEvaluationDatasetInput Input,
    HostedEvaluationExpectedOutput ExpectedOutput,
    HostedEvaluationDatasetMetadata Metadata);
