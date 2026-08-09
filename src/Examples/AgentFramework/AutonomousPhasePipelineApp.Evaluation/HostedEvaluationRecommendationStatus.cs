using System.Text.Json.Serialization;

namespace AutonomousPhasePipelineApp.Evaluation;

[JsonConverter(
    typeof(JsonStringEnumConverter<HostedEvaluationRecommendationStatus>))]
internal enum HostedEvaluationRecommendationStatus
{
    ProtocolInvalid,
    InfrastructureUnreliable,
    InsufficientlyPowered,
    Pilot,
    Supported,
}
