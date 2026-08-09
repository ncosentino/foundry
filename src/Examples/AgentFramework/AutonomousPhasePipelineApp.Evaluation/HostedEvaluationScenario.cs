using System.Text.Json.Serialization;

namespace AutonomousPhasePipelineApp.Evaluation;

[JsonConverter(typeof(JsonStringEnumConverter<HostedEvaluationScenario>))]
internal enum HostedEvaluationScenario
{
    Success,
    OptionalBranchFailure,
    RequiredBranchFailure,
    CorrectionSucceeds,
    CorrectionExhausted,
    Cancellation,
    CheckpointRestore,
    DeliveryReplay,
    IneffectiveProgress,
}
