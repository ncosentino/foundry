using System.Text.Json.Serialization;

namespace AutonomousPhasePipelineApp.Evaluation;

[JsonConverter(typeof(JsonStringEnumConverter<HostedEvaluationExecutionStatus>))]
internal enum HostedEvaluationExecutionStatus
{
    Completed,
    Failed,
    Canceled,
    NotApplicable,
    BlockedProvider,
}
