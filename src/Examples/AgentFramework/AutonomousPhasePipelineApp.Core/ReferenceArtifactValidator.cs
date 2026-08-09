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

    internal static bool TryNormalizeSynthesis(
        string? content,
        out string normalized,
        out string error)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(content))
        {
            error = "empty-artifact";
            return false;
        }

        string candidate = content.Trim();
        if (candidate.StartsWith("```", StringComparison.Ordinal))
        {
            int firstLineEnd = candidate.IndexOf('\n');
            int fenceEnd = candidate.LastIndexOf(
                "```",
                StringComparison.Ordinal);
            if (firstLineEnd >= 0 && fenceEnd > firstLineEnd)
            {
                candidate = candidate[
                    (firstLineEnd + 1)..fenceEnd].Trim();
            }
        }
        else
        {
            int objectStart = candidate.IndexOf('{');
            int objectEnd = candidate.LastIndexOf('}');
            if (objectStart >= 0 && objectEnd > objectStart)
            {
                candidate = candidate[objectStart..(objectEnd + 1)];
            }
        }

        if (!TryValidateSynthesis(candidate, out error))
        {
            return false;
        }

        using JsonDocument document = JsonDocument.Parse(candidate);
        normalized = document.RootElement.GetRawText();
        return true;
    }

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
