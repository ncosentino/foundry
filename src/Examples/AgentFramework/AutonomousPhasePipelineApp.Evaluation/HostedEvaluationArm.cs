using System.Text.Json.Serialization;

namespace AutonomousPhasePipelineApp.Evaluation;

[JsonConverter(typeof(JsonStringEnumConverter<HostedEvaluationArm>))]
internal enum HostedEvaluationArm
{
    HarnessPlain,
    HarnessDelegated,
    Magentic,
}
