using System.Text.Json;

namespace AutonomousPhasePipelineApp.Core;

internal static class ReferenceArtifactValidator
{
    internal const string SynthesisCorrectionCode =
        "correction_code=SYNTHESIS_RECOMMENDATION_REQUIRED";

    internal static bool TryValidateResearch(
        string? content,
        out string error) =>
        TryValidate(
            content,
            ["topic"],
            ["evidence"],
            out error);

    internal static bool TryValidateSpecialist(
        string? content,
        out string error) =>
        TryValidate(
            content,
            ["summary"],
            ["evidence"],
            out error);

    internal static bool TryValidateSynthesis(
        string? content,
        out string error) =>
        TryValidate(
            content,
            ["summary", "recommendation"],
            ["evidence"],
            out error);

    private static bool TryValidate(
        string? content,
        string[] requiredStrings,
        string[] requiredArrays,
        out string error)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            error = "empty-artifact";
            return false;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(content);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                error = "artifact-not-object";
                return false;
            }

            foreach (string propertyName in requiredStrings)
            {
                if (!document.RootElement.TryGetProperty(
                    propertyName,
                    out JsonElement value) ||
                    value.ValueKind != JsonValueKind.String ||
                    string.IsNullOrWhiteSpace(value.GetString()))
                {
                    error = $"missing-{propertyName}";
                    return false;
                }
            }

            foreach (string propertyName in requiredArrays)
            {
                if (!document.RootElement.TryGetProperty(
                    propertyName,
                    out JsonElement value) ||
                    value.ValueKind != JsonValueKind.Array ||
                    value.GetArrayLength() == 0 ||
                    value.EnumerateArray().Any(
                        item =>
                            item.ValueKind != JsonValueKind.String ||
                            string.IsNullOrWhiteSpace(item.GetString())))
                {
                    error = $"missing-{propertyName}";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }
        catch (JsonException)
        {
            error = "invalid-json";
            return false;
        }
    }
}
