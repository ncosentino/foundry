using System.Text.Json;

namespace NexusLabs.Foundry.Evaluation.Harness;

/// <summary>
/// AOT-safe serializer for <see cref="HarnessCaseSetManifest"/> documents, backed by the
/// source-generated <see cref="HarnessCaseSetManifestJsonContext"/>. It performs only JSON
/// conversion; structural validation of the frozen invariants is performed by
/// <see cref="HarnessManifestCaseSource"/>.
/// </summary>
public static class HarnessCaseSetManifestSerializer
{
    /// <summary>
    /// Serializes a manifest to indented JSON with enum members emitted as their string names.
    /// </summary>
    /// <param name="manifest">The manifest to serialize.</param>
    /// <returns>The JSON document text.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="manifest"/> is <see langword="null"/>.</exception>
    public static string Serialize(HarnessCaseSetManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        return JsonSerializer.Serialize(manifest, IndentedContext.HarnessCaseSetManifest);
    }

    /// <summary>
    /// Deserializes a manifest from JSON without performing structural validation.
    /// </summary>
    /// <param name="json">The JSON document text.</param>
    /// <returns>The deserialized manifest.</returns>
    /// <exception cref="ArgumentException"><paramref name="json"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="HarnessCaseSetManifestException">
    /// <paramref name="json"/> is not valid JSON or does not deserialize to a manifest.
    /// </exception>
    public static HarnessCaseSetManifest Deserialize(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        HarnessCaseSetManifest? manifest;
        try
        {
            manifest = JsonSerializer.Deserialize(json, HarnessCaseSetManifestJsonContext.Default.HarnessCaseSetManifest);
        }
        catch (JsonException ex)
        {
            throw new HarnessCaseSetManifestException("The manifest JSON could not be parsed.", ex);
        }

        return manifest ?? throw new HarnessCaseSetManifestException("The manifest JSON deserialized to null.");
    }

    private static readonly HarnessCaseSetManifestJsonContext IndentedContext =
        new(new JsonSerializerOptions(HarnessCaseSetManifestJsonContext.Default.Options) { WriteIndented = true });
}
