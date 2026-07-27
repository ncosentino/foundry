using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace NexusLabs.Foundry.Evaluation.Tests.Harness;

public sealed class HarnessPublishedComparisonArtifactTests
{
    [Fact]
    public void PublicationManifest_HashesEveryPublishedIndexArtifact()
    {
        var reportRoot = FindReportRoot();
        using var manifest = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(reportRoot, "run-30273935931-publication-manifest.json")));

        Assert.Equal("PendingHumanReview", manifest.RootElement.GetProperty("status").GetString());
        foreach (var file in manifest.RootElement.GetProperty("files").EnumerateArray())
        {
            var relativePath = file.GetProperty("relativePath").GetString()!;
            var path = Path.Combine(
                reportRoot,
                relativePath.Replace('/', Path.DirectorySeparatorChar));
            Assert.True(File.Exists(path), $"Published artifact '{relativePath}' does not exist.");
            Assert.Equal(file.GetProperty("sha256").GetString(), CanonicalSha256(path));
        }
    }

    [Fact]
    public void PublicationManifest_HashesEveryFrozenInput()
    {
        var repositoryRoot = FindRepositoryRoot();
        var reportRoot = FindReportRoot();
        using var manifest = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(reportRoot, "run-30273935931-publication-manifest.json")));
        var inputs = manifest.RootElement.GetProperty("inputs").EnumerateArray().ToArray();
        var expectedPaths = new[]
        {
            "artifacts/eval/case-sets/harness-001/v1.0/manifest.json",
            "artifacts/eval/case-sets/harness-001/v1.0/analysis-plan.md",
            "artifacts/eval/case-sets/harness-001/v1.0/pricing/github-models.v1.json",
            "artifacts/eval/case-sets/harness-001/v1.0/judges/manifest.json",
        };

        Assert.Equal(
            expectedPaths,
            inputs.Select(input => input.GetProperty("path").GetString()).ToArray());

        var publication = File.ReadAllText(
            Path.Combine(reportRoot, "run-30273935931-publication.md"));
        foreach (var input in inputs)
        {
            var relativePath = input.GetProperty("path").GetString()!;
            var path = Path.Combine(
                repositoryRoot,
                relativePath.Replace('/', Path.DirectorySeparatorChar));
            var expectedHash = input.GetProperty("sha256").GetString()!;

            Assert.True(File.Exists(path), $"Frozen input '{relativePath}' does not exist.");
            Assert.Equal(expectedHash, CanonicalSha256(path));
            Assert.Contains(expectedHash, publication);
        }
    }

    [Fact]
    public void HumanReviewBlock_IsCompleteButUnsigned()
    {
        var reportRoot = FindReportRoot();
        using var signature = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(reportRoot, "run-30273935931-human-review.json")));
        var root = signature.RootElement;

        Assert.Equal("PendingHumanReview", root.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("reviewerIdentity").ValueKind);
        Assert.Equal(JsonValueKind.Null, root.GetProperty("reviewedAtUtc").ValueKind);
        Assert.False(root.GetProperty("deterministicAnchorsAcknowledged").GetBoolean());
        Assert.False(root.GetProperty("pairedUncertaintyAcknowledged").GetBoolean());
        Assert.False(root.GetProperty("diagnosticsParityAcknowledged").GetBoolean());
        Assert.False(root.GetProperty("judgeCalibrationAcknowledged").GetBoolean());
        Assert.False(root.GetProperty("truncationStatusAcknowledged").GetBoolean());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("retentionRecommendation").ValueKind);
        Assert.Equal(JsonValueKind.Null, root.GetProperty("signature").ValueKind);

        var checksumPath = Path.Combine(reportRoot, "run-30273935931", "checksums.sha256");
        Assert.Equal(
            CanonicalSha256(checksumPath),
            root.GetProperty("artifactBundleChecksumSha256").GetString());
    }

    [Fact]
    public void Publication_RemainsAdvisoryAndMakesNoRetentionDecision()
    {
        var reportRoot = FindReportRoot();
        var publication = File.ReadAllText(
            Path.Combine(reportRoot, "run-30273935931-publication.md"));

        Assert.Contains("Human review status: `PendingHumanReview`", publication);
        Assert.Contains("No retention or removal decision is published", publication);
        Assert.Contains("Every completion interval includes zero", publication);
        Assert.Contains("Judge evidence is `UNCALIBRATED`", publication);
    }

    private static string FindReportRoot()
    {
        return Path.Combine(
            FindRepositoryRoot(),
            "artifacts",
            "eval",
            "reports",
            "harness-001");
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(
                directory.FullName,
                "artifacts",
                "eval",
                "reports",
                "harness-001",
                "run-30273935931-summary.md");
            if (File.Exists(candidate))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the Foundry repository root.");
    }

    private static string CanonicalSha256(string path)
    {
        var text = File.ReadAllText(path).ReplaceLineEndings("\n");
        return Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(text)))
            .ToLowerInvariant();
    }
}
