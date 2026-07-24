using System.Text.Json.Serialization;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness;

/// <summary>
/// Source-generated <see cref="JsonSerializerContext"/> for <see cref="HarnessSessionEnvelope"/>.
/// Required so the envelope can be serialized/deserialized without a reflection-based
/// fallback, keeping the Harness session-persistence path NativeAOT-safe.
/// </summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(HarnessSessionEnvelope))]
internal sealed partial class HarnessSessionEnvelopeJsonContext : JsonSerializerContext
{
}
