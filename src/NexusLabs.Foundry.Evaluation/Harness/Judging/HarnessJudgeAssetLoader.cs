using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace NexusLabs.Foundry.Evaluation.Harness.Judging;

/// <summary>
/// Loads and validates the versioned Harness judge assets from a case-set judge directory.
/// </summary>
public static class HarnessJudgeAssetLoader
{
    /// <summary>
    /// Loads the judge manifest, rubrics, and calibration manifest.
    /// </summary>
    /// <param name="judgeDirectory">The directory containing <c>manifest.json</c>.</param>
    /// <returns>The validated judge assets.</returns>
    /// <exception cref="ArgumentException"><paramref name="judgeDirectory"/> is blank.</exception>
    /// <exception cref="DirectoryNotFoundException">The directory does not exist.</exception>
    /// <exception cref="FileNotFoundException">A declared asset does not exist.</exception>
    /// <exception cref="InvalidDataException">An asset identity, hash, schema, or calibration state is invalid.</exception>
    public static HarnessJudgeAssets Load(string judgeDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(judgeDirectory);
        var root = Path.GetFullPath(judgeDirectory);
        if (!Directory.Exists(root))
        {
            throw new DirectoryNotFoundException($"Judge asset directory '{root}' does not exist.");
        }

        using var manifestDocument = ReadJson(Path.Combine(root, "manifest.json"));
        var manifest = manifestDocument.RootElement;
        RequireString(manifest, "schemaVersion", "1.0");
        RequireString(manifest, "caseSetId", "harness-001");
        RequireString(manifest, "caseSetVersion", "v1.0");
        var minimumKappa = manifest.GetProperty("minimumKappa").GetDouble();
        if (!double.IsFinite(minimumKappa) || minimumKappa < 0 || minimumKappa > 1)
        {
            throw new InvalidDataException("The minimum kappa must be finite and from zero through one.");
        }

        HarnessJudgeRubric? nominal = null;
        HarnessJudgeRubric? ordinal = null;
        foreach (var rubricReference in manifest.GetProperty("rubrics").EnumerateArray())
        {
            var rubric = LoadRubric(root, rubricReference);
            switch (rubric.Kind)
            {
                case HarnessJudgeRubricKind.Nominal when nominal is null:
                    nominal = rubric;
                    break;
                case HarnessJudgeRubricKind.Ordinal when ordinal is null:
                    ordinal = rubric;
                    break;
                default:
                    throw new InvalidDataException($"Rubric kind '{rubric.Kind}' is duplicated.");
            }
        }

        if (nominal is null || ordinal is null)
        {
            throw new InvalidDataException("The judge manifest must declare one nominal and one ordinal rubric.");
        }

        var calibrationReference = manifest.GetProperty("calibration");
        var calibrationPath = ResolveContainedPath(
            root,
            calibrationReference.GetProperty("manifestPath").GetString()!);
        VerifyHash(
            calibrationPath,
            calibrationReference.GetProperty("manifestSha256").GetString()!);
        using var calibrationDocument = ReadJson(calibrationPath);
        var calibration = calibrationDocument.RootElement;
        RequireString(calibration, "schemaVersion", "1.0");
        var eligibleCount = calibration.GetProperty("eligibleItemCount").GetInt32();
        var provisionalCount = calibration.GetProperty("provisionalItemCount").GetInt32();
        var itemCount = calibration.GetProperty("itemCount").GetInt32();
        if (eligibleCount < 0 ||
            provisionalCount < 0 ||
            itemCount < 0 ||
            itemCount != eligibleCount + provisionalCount)
        {
            throw new InvalidDataException(
                "Calibration item counts must be non-negative and partition the full set.");
        }

        var heldoutPath = ResolveContainedPath(
            root,
            calibration.GetProperty("heldoutPath").GetString()!);
        VerifyHash(heldoutPath, calibration.GetProperty("heldoutSha256").GetString()!);
        ValidateRubricBindings(calibration, nominal, ordinal);

        var observedKappa = calibration.TryGetProperty("observedKappa", out var kappaElement) &&
            kappaElement.ValueKind == JsonValueKind.Number
            ? kappaElement.GetDouble()
            : (double?)null;
        if (observedKappa.HasValue &&
            (!double.IsFinite(observedKappa.Value) || observedKappa < -1 || observedKappa > 1))
        {
            throw new InvalidDataException("Observed kappa must be finite and from minus one through one.");
        }

        var calibrated =
            string.Equals(manifest.GetProperty("status").GetString(), "CALIBRATED", StringComparison.Ordinal) &&
            calibrationReference.GetProperty("publishableAsCalibrated").GetBoolean() &&
            string.Equals(
                calibrationReference.GetProperty("labelStatus").GetString(),
                "HUMAN_ATTESTED",
                StringComparison.Ordinal) &&
            string.Equals(
                calibration.GetProperty("status").GetString(),
                "HUMAN_ATTESTED",
                StringComparison.Ordinal) &&
            eligibleCount > 0 &&
            observedKappa is { } kappa &&
            kappa >= minimumKappa;

        return new HarnessJudgeAssets(
            nominal,
            ordinal,
            calibrated
                ? HarnessJudgeCalibrationState.Calibrated
                : HarnessJudgeCalibrationState.Uncalibrated,
            minimumKappa,
            observedKappa,
            eligibleCount,
            provisionalCount);
    }

    private static void ValidateRubricBindings(
        JsonElement calibration,
        HarnessJudgeRubric nominal,
        HarnessJudgeRubric ordinal)
    {
        var expected = new Dictionary<string, HarnessJudgeRubric>(StringComparer.Ordinal)
        {
            [nominal.Id] = nominal,
            [ordinal.Id] = ordinal,
        };
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var binding in calibration.GetProperty("rubricBindings").EnumerateArray())
        {
            var id = binding.GetProperty("rubricId").GetString()!;
            if (!expected.TryGetValue(id, out var rubric) || !seen.Add(id))
            {
                throw new InvalidDataException($"Calibration rubric binding '{id}' is unexpected or duplicated.");
            }

            if (!string.Equals(
                    binding.GetProperty("version").GetString(),
                    rubric.Version,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    binding.GetProperty("sha256").GetString(),
                    rubric.Sha256,
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException($"Calibration rubric binding '{id}' does not match the loaded rubric.");
            }
        }

        if (seen.Count != expected.Count)
        {
            throw new InvalidDataException("The calibration manifest does not bind every loaded rubric.");
        }
    }

    private static HarnessJudgeRubric LoadRubric(
        string root,
        JsonElement reference)
    {
        var path = ResolveContainedPath(root, reference.GetProperty("relativePath").GetString()!);
        var expectedHash = reference.GetProperty("sha256").GetString()!;
        VerifyHash(path, expectedHash);
        using var rubricDocument = ReadJson(path);
        var rubric = rubricDocument.RootElement;
        var id = rubric.GetProperty("rubricId").GetString()!;
        var version = rubric.GetProperty("version").GetString()!;
        if (!string.Equals(id, reference.GetProperty("rubricId").GetString(), StringComparison.Ordinal) ||
            !string.Equals(version, reference.GetProperty("version").GetString(), StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Rubric '{path}' does not match its manifest identity.");
        }

        var kind = Enum.Parse<HarnessJudgeRubricKind>(
            rubric.GetProperty("kind").GetString()!,
            ignoreCase: false);
        var instructions = rubric.GetProperty("instructions")
            .EnumerateArray()
            .Select(item => item.GetString()!)
            .ToArray();
        if (instructions.Length == 0 || instructions.Any(string.IsNullOrWhiteSpace))
        {
            throw new InvalidDataException($"Rubric '{id}' must declare non-blank instructions.");
        }

        var labels = kind == HarnessJudgeRubricKind.Nominal
            ? rubric.GetProperty("labels").EnumerateArray().Select(item => item.GetString()!).ToArray()
            : [];
        var scale = kind == HarnessJudgeRubricKind.Ordinal
            ? rubric.GetProperty("scale")
                .EnumerateArray()
                .Select(item => item.GetProperty("value").GetInt32())
                .ToArray()
            : [];
        if (kind == HarnessJudgeRubricKind.Nominal &&
            (labels.Length == 0 || labels.Distinct(StringComparer.Ordinal).Count() != labels.Length))
        {
            throw new InvalidDataException($"Nominal rubric '{id}' must declare unique labels.");
        }

        if (kind == HarnessJudgeRubricKind.Ordinal &&
            (scale.Length == 0 || !scale.SequenceEqual(scale.Order())))
        {
            throw new InvalidDataException($"Ordinal rubric '{id}' must declare an ordered scale.");
        }

        return new HarnessJudgeRubric(
            id,
            version,
            kind,
            expectedHash,
            instructions,
            labels,
            scale);
    }

    private static string ResolveContainedPath(string root, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            throw new InvalidDataException("An asset path cannot be blank.");
        }

        var path = Path.GetFullPath(
            Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        var relative = Path.GetRelativePath(root, path);
        if (Path.IsPathRooted(relative) ||
            relative.Equals("..", StringComparison.Ordinal) ||
            relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Asset path '{relativePath}' escapes the judge directory.");
        }

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Judge asset '{relativePath}' does not exist.", path);
        }

        return path;
    }

    private static JsonDocument ReadJson(string path)
    {
        try
        {
            return JsonDocument.Parse(File.ReadAllText(path));
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"Judge asset '{path}' is not valid JSON.", exception);
        }
    }

    private static void VerifyHash(string path, string expectedHash)
    {
        var canonicalText = File.ReadAllText(path).ReplaceLineEndings("\n");
        var actualHash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(canonicalText)))
            .ToLowerInvariant();
        if (!string.Equals(actualHash, expectedHash, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Judge asset '{path}' does not match its recorded SHA-256 digest.");
        }
    }

    private static void RequireString(
        JsonElement element,
        string propertyName,
        string expected)
    {
        if (!string.Equals(
                element.GetProperty(propertyName).GetString(),
                expected,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Judge manifest property '{propertyName}' must be '{expected}'.");
        }
    }
}
