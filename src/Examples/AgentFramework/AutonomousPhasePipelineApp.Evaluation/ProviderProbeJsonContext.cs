using System.Text.Json.Serialization;

namespace AutonomousPhasePipelineApp.Evaluation;

[JsonSerializable(typeof(ProviderProbeResult))]
[JsonSerializable(typeof(HostedEvaluationArmResult))]
[JsonSerializable(typeof(HostedEvaluationBlockResult))]
[JsonSerializable(typeof(HostedEvaluationCase))]
[JsonSerializable(typeof(HostedEvaluationProvenance))]
[JsonSerializable(typeof(HostedEvaluationReport))]
[JsonSerializable(typeof(HostedEvaluationRunStatus))]
internal sealed partial class ProviderProbeJsonContext :
    JsonSerializerContext;
