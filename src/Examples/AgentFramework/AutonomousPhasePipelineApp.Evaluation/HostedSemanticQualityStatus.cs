using System.Text.Json.Serialization;

namespace AutonomousPhasePipelineApp.Evaluation;

[JsonConverter(typeof(JsonStringEnumConverter<HostedSemanticQualityStatus>))]
internal enum HostedSemanticQualityStatus
{
    NotScoredCalibrationRequired,
    Admitted,
}
