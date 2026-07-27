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
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(
                directory.FullName,
                "artifacts",
                "eval",
                "reports",
                "harness-001");
            if (File.Exists(Path.Combine(candidate, "run-30273935931-summary.md")))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the harness-001 report publication.");
    }

    private static string CanonicalSha256(string path)
    {
        var text = File.ReadAllText(path).ReplaceLineEndings("\n");
        return Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(text)))
            .ToLowerInvariant();
    }
}
