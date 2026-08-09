using System.Text.Json.Serialization;

namespace AutonomousPhasePipelineApp.Evaluation;

[JsonSerializable(typeof(ProviderProbeResult))]
internal sealed partial class ProviderProbeJsonContext :
    JsonSerializerContext;
