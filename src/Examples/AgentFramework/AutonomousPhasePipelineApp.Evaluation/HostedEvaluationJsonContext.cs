using System.Text.Json.Serialization;

namespace AutonomousPhasePipelineApp.Evaluation;

[JsonSerializable(typeof(HostedEvaluationArmResult))]
[JsonSerializable(typeof(HostedEvaluationDatasetItem))]
[JsonSerializable(typeof(IReadOnlyList<HostedEvaluationDatasetItem>))]
[JsonSerializable(typeof(HostedEvaluationDatasetPublication))]
[JsonSerializable(typeof(HostedEvaluationBlockResult))]
[JsonSerializable(typeof(HostedEvaluationCase))]
[JsonSerializable(typeof(HostedEvaluationItemFailure))]
[JsonSerializable(typeof(HostedEvaluationProvenance))]
[JsonSerializable(typeof(HostedEvaluationReport))]
[JsonSerializable(typeof(HostedEvaluationRunStatus))]
internal sealed partial class HostedEvaluationJsonContext :
    JsonSerializerContext;
