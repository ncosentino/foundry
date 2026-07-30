using System.Text.Json.Serialization;

namespace NexusLabs.Foundry.Evaluation.Harness;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(HarnessComparisonReport))]
internal sealed partial class HarnessComparisonReportJsonContext : JsonSerializerContext
{
}
