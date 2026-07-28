using System.Globalization;
using System.Security.Cryptography;
using System.Text;
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
        Assert.Equal("HUMAN_ATTESTED", calibration.GetProperty("labelStatus").GetString());
        Assert.False(calibration.GetProperty("publishableAsCalibrated").GetBoolean());
        Assert.Equal(
            calibration.GetProperty("manifestSha256").GetString(),
            ComputeSha256(JudgePath(calibration.GetProperty("manifestPath").GetString()!)));
    }

    [Fact]
    public void CalibrationSet_BindsHumanAttestedLabelsWithoutClaimingAgreement()
    {
        using var manifest = ReadJson("calibration/manifest.json");
        var root = manifest.RootElement;
        var items = ReadCalibrationItems();

        Assert.Equal("heldout-calibration", root.GetProperty("split").GetString());
        Assert.Equal("HUMAN_ATTESTED", root.GetProperty("status").GetString());
        Assert.Equal("UNCALIBRATED", root.GetProperty("judgeCalibrationState").GetString());
        Assert.Equal(items.Count, root.GetProperty("itemCount").GetInt32());
        Assert.Equal(items.Count, root.GetProperty("eligibleItemCount").GetInt32());
        Assert.Equal(0, root.GetProperty("provisionalItemCount").GetInt32());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("observedKappa").ValueKind);
        var labelProvenance = root.GetProperty("labelProvenance");
        Assert.Equal("human-attestation", labelProvenance.GetProperty("type").GetString());
        Assert.Equal("@ncosentino", labelProvenance.GetProperty("attestedBy").GetString());
        Assert.True(labelProvenance.GetProperty("isHumanLabel").GetBoolean());
        Assert.True(labelProvenance.GetProperty("provisionalLabelsRetainedForAudit").GetBoolean());
        Assert.Equal(
            root.GetProperty("heldoutSha256").GetString(),
            ComputeSha256(JudgePath(root.GetProperty("heldoutPath").GetString()!)));
        Assert.Equal(
            root.GetProperty("provisionalSourceSha256").GetString(),
            ComputeSha256(JudgePath(root.GetProperty("provisionalSourcePath").GetString()!)));
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

        var subsetAttestation = root.GetProperty("humanAttestation");
        Assert.Equal("@ncosentino", subsetAttestation.GetProperty("attestedBy").GetString());
        Assert.True(DateTimeOffset.TryParse(
            subsetAttestation.GetProperty("attestedAtUtc").GetString(),
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal,
            out _));
        Assert.Equal(
            root.GetProperty("requiredHumanAttestation").GetProperty("subsetStatement").GetString(),
            subsetAttestation.GetProperty("statement").GetString());
        Assert.Equal(items.Count, subsetAttestation.GetProperty("eligibleItemCount").GetInt32());

        var itemStatement = root
            .GetProperty("requiredHumanAttestation")
            .GetProperty("itemStatement")
            .GetString();
        var attestedAtUtc = subsetAttestation.GetProperty("attestedAtUtc").GetString();
        Assert.All(items, item =>
        {
            Assert.Equal("heldout-calibration", item.GetProperty("split").GetString());
            Assert.Equal("ai-bootstrap", item.GetProperty("provisionalLabels").GetProperty("source").GetString());
            Assert.Equal("human-attested", item.GetProperty("finalLabels").GetProperty("source").GetString());
            Assert.True(item.GetProperty("eligibleForCalibration").GetBoolean());

            var attestations = item.GetProperty("humanAttestation").EnumerateArray().ToArray();
            Assert.Equal(3, attestations.Length);
            Assert.All(attestations, attestation =>
            {
                Assert.Equal("@ncosentino", attestation.GetProperty("attestedBy").GetString());
                Assert.Equal(attestedAtUtc, attestation.GetProperty("attestedAtUtc").GetString());
                Assert.Equal(itemStatement, attestation.GetProperty("statement").GetString());
                Assert.Equal("v1.0", attestation.GetProperty("rubricVersion").GetString());
            });

            var finalLabels = item.GetProperty("finalLabels");
            var nominal = Assert.Single(
                attestations,
                attestation => attestation.GetProperty("target").GetString() == "pairwisePreference");
            Assert.Equal("harness-nominal-pairwise-preference", nominal.GetProperty("rubricId").GetString());
            Assert.Equal(
                "ed39fcf321e10f33ac63a5fd9edd77c42cff0f5c970410ae9dfd32e71036763d",
                nominal.GetProperty("rubricSha256").GetString());
            Assert.Equal(
                finalLabels.GetProperty("pairwisePreference").GetString(),
                nominal.GetProperty("finalLabel").GetString());

            foreach (var target in new[] { "leftOrdinalQuality", "rightOrdinalQuality" })
            {
                var ordinal = Assert.Single(
                    attestations,
                    attestation => attestation.GetProperty("target").GetString() == target);
                Assert.Equal("harness-ordinal-response-quality", ordinal.GetProperty("rubricId").GetString());
                Assert.Equal(
                    "94196e2ad78b494b8a2a4036247053d53a2facf559aefa043aeeb4fe2e82a448",
                    ordinal.GetProperty("rubricSha256").GetString());
                Assert.Equal(
                    finalLabels.GetProperty(target).GetInt32(),
                    ordinal.GetProperty("finalLabel").GetInt32());
            }
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
        var canonicalLabels = canonical.GetProperty("finalLabels");
        var reversedLabels = reversed.GetProperty("finalLabels");
        Assert.Equal("Left", canonicalLabels.GetProperty("pairwisePreference").GetString());
        Assert.Equal(3, canonicalLabels.GetProperty("leftOrdinalQuality").GetInt32());
        Assert.Equal(1, canonicalLabels.GetProperty("rightOrdinalQuality").GetInt32());
        Assert.Equal("Right", reversedLabels.GetProperty("pairwisePreference").GetString());
        Assert.Equal(1, reversedLabels.GetProperty("leftOrdinalQuality").GetInt32());
        Assert.Equal(4, reversedLabels.GetProperty("rightOrdinalQuality").GetInt32());
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
            var finalLabels = item.GetProperty("finalLabels");
            Assert.Equal("Tie", finalLabels.GetProperty("pairwisePreference").GetString());
            Assert.Equal(3, finalLabels.GetProperty("leftOrdinalQuality").GetInt32());
            Assert.Equal(3, finalLabels.GetProperty("rightOrdinalQuality").GetInt32());
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
            var finalLabels = item.GetProperty("finalLabels");
            var formattedIsLeft = left.GetProperty("surfaceStyle").GetString() == "formatted";
            Assert.Equal(formattedIsLeft ? "Left" : "Right", finalLabels.GetProperty("pairwisePreference").GetString());
            Assert.Equal(formattedIsLeft ? 4 : 3, finalLabels.GetProperty("leftOrdinalQuality").GetInt32());
            Assert.Equal(formattedIsLeft ? 3 : 4, finalLabels.GetProperty("rightOrdinalQuality").GetInt32());
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
            item.GetProperty("finalLabels").GetProperty("pairwisePreference").GetString());
        Assert.Equal(5, item.GetProperty("finalLabels").GetProperty("leftOrdinalQuality").GetInt32());
        Assert.Equal(1, item.GetProperty("finalLabels").GetProperty("rightOrdinalQuality").GetInt32());
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
        File.ReadLines(JudgePath("calibration/heldout.human-attested.jsonl"))
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
        Convert.ToHexString(
            SHA256.HashData(
                Encoding.UTF8.GetBytes(
                    File.ReadAllText(path).ReplaceLineEndings("\n"))))
            .ToLowerInvariant();

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
