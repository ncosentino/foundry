using System.Text.Json;

namespace AutonomousPhasePipelineApp.Core;

internal static class ReferenceArtifactValidator
{
    internal const string SynthesisCorrectionCode =
        "correction_code=SYNTHESIS_ARTIFACT_INVALID";

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
        ReferenceArtifactManifest manifest,
        out string error)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        if (string.IsNullOrWhiteSpace(content))
        {
            error = "empty-artifact";
            return false;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(content);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                error = "artifact-not-object";
                return false;
            }

            if (!TryValidateRequiredString(root, "summary", out error) ||
                !TryValidateRequiredString(root, "recommendation", out error) ||
                !TryReadStringArray(
                    root,
                    "evidence",
                    allowEmpty: false,
                    out string[] evidence,
                    out error) ||
                !TryReadStringArray(
                    root,
                    "gaps",
                    allowEmpty: true,
                    out string[] gaps,
                    out error))
            {
                return false;
            }

            if (!TryValidateUniqueValues(
                evidence,
                "duplicate-evidence",
                out error))
            {
                return false;
            }

            ReferenceArtifactReference? invalidBranchArtifact =
                manifest.Branches
                    .Where(branch => branch.Outcome is
                        ReferencePipelineOutcome.Failed or
                        ReferencePipelineOutcome.Skipped)
                    .Select(branch => branch.Artifact)
                    .FirstOrDefault(artifact => artifact is not null);
            if (invalidBranchArtifact is not null)
            {
                error =
                    $"manifest-incomplete-branch-artifact:{invalidBranchArtifact.Id}";
                return false;
            }

            ReferencePhaseArtifact? acceptedBranchWithoutArtifact =
                manifest.Branches.FirstOrDefault(
                    branch =>
                        (branch.Outcome is
                            ReferencePipelineOutcome.Completed or
                            ReferencePipelineOutcome.Partial) &&
                        branch.Artifact is null);
            if (acceptedBranchWithoutArtifact is not null)
            {
                error =
                    $"manifest-missing-branch-artifact:{acceptedBranchWithoutArtifact.Phase}";
                return false;
            }

            string[] expectedEvidence =
            [
                manifest.Research.Id,
                .. manifest.Branches
                    .Where(branch => branch.Outcome is
                        ReferencePipelineOutcome.Completed or
                        ReferencePipelineOutcome.Partial)
                    .Select(branch => branch.Artifact!.Id),
            ];
            if (!TryValidateExactSet(
                evidence,
                expectedEvidence,
                "unexpected-evidence",
                "missing-required-evidence",
                out error))
            {
                return false;
            }

            if (!TryValidateUniqueValues(
                gaps,
                "duplicate-gap",
                out error))
            {
                return false;
            }

            if (!TryValidateExactSet(
                gaps,
                manifest.Gaps,
                "unexpected-gap",
                "missing-gap",
                out error))
            {
                return false;
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
                if (!TryValidateRequiredString(
                    document.RootElement,
                    propertyName,
                    out error))
                {
                    return false;
                }
            }

            foreach (string propertyName in requiredArrays)
            {
                if (!TryReadStringArray(
                    document.RootElement,
                    propertyName,
                    allowEmpty: false,
                    out _,
                    out error))
                {
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

    private static bool TryValidateRequiredString(
        JsonElement root,
        string propertyName,
        out string error)
    {
        if (!root.TryGetProperty(
            propertyName,
            out JsonElement value) ||
            value.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(value.GetString()))
        {
            error = $"missing-{propertyName}";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static bool TryReadStringArray(
        JsonElement root,
        string propertyName,
        bool allowEmpty,
        out string[] values,
        out string error)
    {
        if (!root.TryGetProperty(
            propertyName,
            out JsonElement value) ||
            value.ValueKind != JsonValueKind.Array)
        {
            values = [];
            error = $"missing-{propertyName}";
            return false;
        }

        values = value
            .EnumerateArray()
            .Select(item =>
                item.ValueKind == JsonValueKind.String
                    ? item.GetString()
                    : null)
            .OfType<string>()
            .ToArray();
        if (values.Length != value.GetArrayLength() ||
            values.Any(string.IsNullOrWhiteSpace))
        {
            values = [];
            error = $"invalid-{propertyName}";
            return false;
        }

        if (!allowEmpty && values.Length == 0)
        {
            error = $"missing-{propertyName}";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static bool TryValidateUniqueValues(
        IReadOnlyList<string> values,
        string errorCode,
        out string error)
    {
        string? duplicate = values
            .GroupBy(value => value, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .Order(StringComparer.Ordinal)
            .FirstOrDefault();
        if (duplicate is not null)
        {
            error = $"{errorCode}:{duplicate}";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static bool TryValidateExactSet(
        IReadOnlyCollection<string> actual,
        IReadOnlyCollection<string> expected,
        string unexpectedCode,
        string missingCode,
        out string error)
    {
        var expectedSet = new HashSet<string>(
            expected,
            StringComparer.Ordinal);
        string? unexpected = actual
            .Where(value => !expectedSet.Contains(value))
            .Order(StringComparer.Ordinal)
            .FirstOrDefault();
        if (unexpected is not null)
        {
            error = $"{unexpectedCode}:{unexpected}";
            return false;
        }

        var actualSet = new HashSet<string>(
            actual,
            StringComparer.Ordinal);
        string? missing = expected
            .Where(value => !actualSet.Contains(value))
            .Order(StringComparer.Ordinal)
            .FirstOrDefault();
        if (missing is not null)
        {
            error = $"{missingCode}:{missing}";
            return false;
        }

        error = string.Empty;
        return true;
    }
}
