using System.Security.Cryptography;
using System.Text.Json;

namespace NexusLabs.Foundry.Evaluation.Tests.Harness.Judging;

public sealed class HarnessJudgeCalibrationTests
{
    [Fact]
    public void JudgeAssets_FreezeIdentityAndMatchRecordedHashes()
    {
        using var manifest = ReadJson("manifest.json");
        var root = manifest.RootElement;

        Assert.Equal("1.0", root.GetProperty("schemaVersion").GetString());
        Assert.Equal("harness-001", root.GetProperty("caseSetId").GetString());
        Assert.Equal("v1.0", root.GetProperty("caseSetVersion").GetString());
        Assert.Equal("v1.0", root.GetProperty("judgeAssetsVersion").GetString());
        Assert.Equal("UNCALIBRATED", root.GetProperty("status").GetString());
        Assert.Equal(0.6, root.GetProperty("minimumKappa").GetDouble());
        Assert.Equal(
            new[] { "Position", "Verbosity", "Style", "DeterministicDisagreement" },
            root.GetProperty("biasChecks")
                .EnumerateArray()
                .Select(check => check.GetString()!)
                .ToArray());

        var governance = root.GetProperty("governance");
        Assert.Equal("Disagreement", governance.GetProperty("orderInconsistentPairResult").GetString());
        Assert.Equal("DeterministicReference", governance.GetProperty("deterministicConflictAuthority").GetString());
        Assert.False(governance.GetProperty("uncalibratedArmRankingAllowed").GetBoolean());
        Assert.True(governance.GetProperty("differentJudgeModelFamilyPreferred").GetBoolean());

        var rubrics = root.GetProperty("rubrics").EnumerateArray().ToArray();
        Assert.Equal(2, rubrics.Length);
        foreach (var rubric in rubrics)
        {
            var relativePath = rubric.GetProperty("relativePath").GetString()!;
            var expectedHash = rubric.GetProperty("sha256").GetString()!;
            Assert.Equal(expectedHash, ComputeSha256(JudgePath(relativePath)));
        }

        var calibration = root.GetProperty("calibration");
        Assert.Equal("PROVISIONAL", calibration.GetProperty("labelStatus").GetString());
        Assert.False(calibration.GetProperty("publishableAsCalibrated").GetBoolean());
        Assert.Equal(
            calibration.GetProperty("manifestSha256").GetString(),
            ComputeSha256(JudgePath(calibration.GetProperty("manifestPath").GetString()!)));
    }

    [Fact]
    public void CalibrationSet_ExcludesProvisionalLabelsUntilHumanAttested()
    {
        using var manifest = ReadJson("calibration/manifest.json");
        var root = manifest.RootElement;
        var items = ReadCalibrationItems();

        Assert.Equal("heldout-calibration", root.GetProperty("split").GetString());
        Assert.Equal("PROVISIONAL", root.GetProperty("status").GetString());
        Assert.Equal("UNCALIBRATED", root.GetProperty("judgeCalibrationState").GetString());
        Assert.Equal(items.Count, root.GetProperty("itemCount").GetInt32());
        Assert.Equal(0, root.GetProperty("eligibleItemCount").GetInt32());
        Assert.Equal(items.Count, root.GetProperty("provisionalItemCount").GetInt32());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("humanAttestation").ValueKind);
        Assert.False(root.GetProperty("labelProvenance").GetProperty("isHumanLabel").GetBoolean());
        Assert.Equal(
            root.GetProperty("heldoutSha256").GetString(),
            ComputeSha256(JudgePath(root.GetProperty("heldoutPath").GetString()!)));
        Assert.All(root.GetProperty("rubricBindings").EnumerateArray(), binding =>
        {
            var rubricId = binding.GetProperty("rubricId").GetString();
            var relativePath = rubricId switch
            {
                "harness-nominal-pairwise-preference" => "rubrics/nominal-pairwise-preference.v1.json",
                "harness-ordinal-response-quality" => "rubrics/ordinal-response-quality.v1.json",
                _ => throw new InvalidOperationException($"Unexpected rubric binding '{rubricId}'."),
            };
            Assert.Equal(
                binding.GetProperty("sha256").GetString(),
                ComputeSha256(JudgePath(relativePath)));
        });

        Assert.All(items, item =>
        {
            Assert.Equal("heldout-calibration", item.GetProperty("split").GetString());
            Assert.Equal("ai-bootstrap", item.GetProperty("provisionalLabels").GetProperty("source").GetString());
            Assert.Equal(JsonValueKind.Null, item.GetProperty("humanAttestation").ValueKind);
            Assert.False(item.GetProperty("eligibleForCalibration").GetBoolean());
        });
    }

    [Fact]
    public void PositionFixture_ContainsBothOrdersWithInvertedPreference()
    {
        var items = ReadCalibrationItems()
            .Where(item => HasFixtureKind(item, "Position"))
            .Where(item => item.GetProperty("pairId").GetString() == "position-01")
            .ToArray();

        Assert.Equal(2, items.Length);
        var canonical = Assert.Single(items, item =>
            item.GetProperty("presentationOrder").GetString() == "Canonical");
        var reversed = Assert.Single(items, item =>
            item.GetProperty("presentationOrder").GetString() == "Reversed");

        Assert.Equal(
            canonical.GetProperty("left").GetProperty("candidateId").GetString(),
            reversed.GetProperty("right").GetProperty("candidateId").GetString());
        Assert.Equal(
            canonical.GetProperty("right").GetProperty("candidateId").GetString(),
            reversed.GetProperty("left").GetProperty("candidateId").GetString());
        Assert.Equal("Left", canonical.GetProperty("provisionalLabels").GetProperty("pairwisePreference").GetString());
        Assert.Equal("Right", reversed.GetProperty("provisionalLabels").GetProperty("pairwisePreference").GetString());
        Assert.Equal(
            canonical.GetProperty("provisionalLabels").GetProperty("leftOrdinalQuality").GetInt32(),
            reversed.GetProperty("provisionalLabels").GetProperty("rightOrdinalQuality").GetInt32());
        Assert.Equal(
            canonical.GetProperty("provisionalLabels").GetProperty("rightOrdinalQuality").GetInt32(),
            reversed.GetProperty("provisionalLabels").GetProperty("leftOrdinalQuality").GetInt32());
    }

    [Fact]
    public void VerbosityFixture_ChangesLengthWithoutChangingExpectedPreference()
    {
        var items = ReadCalibrationItems()
            .Where(item => HasFixtureKind(item, "Verbosity"))
            .ToArray();

        Assert.Equal(2, items.Length);
        Assert.All(items, item =>
        {
            var left = item.GetProperty("left");
            var right = item.GetProperty("right");
            Assert.Equal(
                left.GetProperty("semanticContentId").GetString(),
                right.GetProperty("semanticContentId").GetString());
            Assert.NotEqual(
                left.GetProperty("text").GetString()!.Length,
                right.GetProperty("text").GetString()!.Length);
            Assert.Equal("Tie", item.GetProperty("provisionalLabels").GetProperty("pairwisePreference").GetString());
            Assert.Equal(
                item.GetProperty("provisionalLabels").GetProperty("leftOrdinalQuality").GetInt32(),
                item.GetProperty("provisionalLabels").GetProperty("rightOrdinalQuality").GetInt32());
        });
    }

    [Fact]
    public void StyleFixture_ChangesSurfaceOnly()
    {
        var items = ReadCalibrationItems()
            .Where(item => HasFixtureKind(item, "Style"))
            .ToArray();

        Assert.Equal(2, items.Length);
        Assert.All(items, item =>
        {
            var left = item.GetProperty("left");
            var right = item.GetProperty("right");
            Assert.Equal(
                left.GetProperty("semanticContentId").GetString(),
                right.GetProperty("semanticContentId").GetString());
            Assert.NotEqual(
                left.GetProperty("surfaceStyle").GetString(),
                right.GetProperty("surfaceStyle").GetString());
            Assert.NotEqual(left.GetProperty("text").GetString(), right.GetProperty("text").GetString());
            Assert.Equal("Tie", item.GetProperty("provisionalLabels").GetProperty("pairwisePreference").GetString());
            Assert.Equal(
                item.GetProperty("provisionalLabels").GetProperty("leftOrdinalQuality").GetInt32(),
                item.GetProperty("provisionalLabels").GetProperty("rightOrdinalQuality").GetInt32());
        });
    }

    [Fact]
    public void DeterministicDisagreementFixture_PreservesDeterministicAuthority()
    {
        var item = Assert.Single(
            ReadCalibrationItems(),
            candidate => HasFixtureKind(candidate, "DeterministicDisagreement"));

        var deterministicWinner = item.GetProperty("deterministicReference").GetProperty("winner").GetString();
        var judgeWinner = item.GetProperty("mockJudgeOutput").GetProperty("pairwisePreference").GetString();

        Assert.True(item.GetProperty("deterministicReference").GetProperty("governsDecision").GetBoolean());
        Assert.True(item.GetProperty("expectedDeterministicDisagreement").GetBoolean());
        Assert.NotEqual(deterministicWinner, judgeWinner);
        Assert.Equal(
            deterministicWinner,
            item.GetProperty("provisionalLabels").GetProperty("pairwisePreference").GetString());
    }

    [Fact]
    public void Rubrics_DeclareClosedNominalLabelsAndOrderedOrdinalScale()
    {
        using var nominal = ReadJson("rubrics/nominal-pairwise-preference.v1.json");
        using var ordinal = ReadJson("rubrics/ordinal-response-quality.v1.json");

        Assert.Equal(
            new[] { "Left", "Tie", "Right", "Abstain" },
            nominal.RootElement.GetProperty("labels").EnumerateArray().Select(label => label.GetString()!).ToArray());

        var scale = ordinal.RootElement.GetProperty("scale")
            .EnumerateArray()
            .Select(anchor => anchor.GetProperty("value").GetInt32())
            .ToArray();
        Assert.Equal([1, 2, 3, 4, 5], scale);
        Assert.Equal("Quadratic", ordinal.RootElement.GetProperty("weighting").GetString());
    }

    private static bool HasFixtureKind(JsonElement item, string kind) =>
        item.GetProperty("fixtureKinds")
            .EnumerateArray()
            .Any(value => value.GetString() == kind);

    private static IReadOnlyList<JsonElement> ReadCalibrationItems() =>
        File.ReadLines(JudgePath("calibration/heldout.provisional.jsonl"))
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line =>
            {
                using var document = JsonDocument.Parse(line);
                return document.RootElement.Clone();
            })
            .ToArray();

    private static JsonDocument ReadJson(string relativePath) =>
        JsonDocument.Parse(File.ReadAllText(JudgePath(relativePath)));

    private static string ComputeSha256(string path) =>
        Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();

    private static string JudgePath(string relativePath) =>
        Path.Combine(
            FindRepositoryRoot(),
            "artifacts",
            "eval",
            "case-sets",
            "harness-001",
            "v1.0",
            "judges",
            relativePath.Replace('/', Path.DirectorySeparatorChar));

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(
                directory.FullName,
                "artifacts",
                "eval",
                "case-sets",
                "harness-001",
                "v1.0",
                "analysis-plan.md")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the repository root from the test output path.");
    }
}
