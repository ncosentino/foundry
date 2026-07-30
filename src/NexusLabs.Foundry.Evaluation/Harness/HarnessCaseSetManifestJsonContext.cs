using System.Text.Json.Serialization;

namespace NexusLabs.Foundry.Evaluation.Harness;

/// <summary>
/// Source-generated <see cref="JsonSerializerContext"/> for <see cref="HarnessCaseSetManifest"/> and
/// its nested types. Enum members are emitted as their string names so the manifest is human-readable
/// and NativeAOT-safe without a reflection-based serializer fallback.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(HarnessCaseSetManifest))]
internal sealed partial class HarnessCaseSetManifestJsonContext : JsonSerializerContext
{
}
