using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace NexusLabs.Foundry.Evaluation.Tests.Harness;

public sealed class HarnessRetainedHostedReportTests
{
    [Fact]
    public void HistoricalGitHubModelsRun_BundleChecksumsRemainValid()
    {
        var reportRoot = FindReportRoot();
        var bundle = Path.Combine(reportRoot, "run-30273935931");
        AssertBundleChecksums(bundle, expectedCount: 323);

        using var status = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(bundle, "run-status.json")));
        Assert.Equal("Completed", status.RootElement.GetProperty("State").GetString());
        Assert.Equal(24, status.RootElement.GetProperty("ScheduledBatchCount").GetInt32());
        Assert.Equal(72, status.RootElement.GetProperty("AttemptsUsed").GetInt32());
        Assert.Equal(180, status.RootElement.GetProperty("ProviderRequestsUsed").GetInt32());
        Assert.Equal(3.6m, status.RootElement.GetProperty("EstimatedCostUsd").GetDecimal());

        using var provenance = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(reportRoot, "run-30273935931-provenance.json")));
        Assert.Equal(
            "30273935931",
            provenance.RootElement.GetProperty("authoritativeWorkflowRunId").GetString());
        Assert.Equal(
            "30270567078",
            provenance.RootElement.GetProperty("excludedRuns")[0]
                .GetProperty("workflowRunId")
                .GetString());
        Assert.Equal(
            0,
            provenance.RootElement.GetProperty("bundleVerification")
                .GetProperty("hashMismatches")
                .GetInt32());

        using var attemptMetadata = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(reportRoot, "run-30273935931-attempt-metadata.json")));
        var attempts = attemptMetadata.RootElement.GetProperty("rows").EnumerateArray().ToArray();
        Assert.Equal(72, attempts.Length);
        Assert.Equal(
            180,
            attempts.Sum(row => row.GetProperty("providerRequestCount").GetInt32()));

        var summary = File.ReadAllText(
            Path.Combine(reportRoot, "run-30273935931-summary.md"));
        Assert.Contains("Underpowered", summary);
        Assert.Contains("It is not a retention recommendation.", summary);
    }

    [Fact]
    public void ApprovedCopilotRun_BundleChecksumsAndProvenanceAreValid()
    {
        var reportRoot = FindReportRoot();
        var bundle = Path.Combine(reportRoot, "run-30513567405");
        AssertBundleChecksums(bundle, expectedCount: 307);

        using var status = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(bundle, "run-status.json")));
        Assert.Equal("Completed", status.RootElement.GetProperty("State").GetString());
        Assert.Equal(24, status.RootElement.GetProperty("ScheduledBatchCount").GetInt32());
        Assert.Equal(72, status.RootElement.GetProperty("AttemptsUsed").GetInt32());
        Assert.Equal(164, status.RootElement.GetProperty("ProviderRequestsUsed").GetInt32());
        Assert.Equal(3.28m, status.RootElement.GetProperty("EstimatedCostUsd").GetDecimal());

        using var immutableInputs = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(bundle, "inputs", "immutable-inputs.json")));
        var inputs = immutableInputs.RootElement;
        Assert.Equal("github-copilot", inputs.GetProperty("provider").GetString());
        Assert.Equal(
            "GitHub Copilot Enterprise",
            inputs.GetProperty("billingProduct").GetString());
        Assert.Equal("foundry,foundry-ci", inputs.GetProperty("runnerLabels").GetString());
        Assert.Equal(
            "2ac9d32cdf31d17e2fb937ffca567562a6d069c5",
            inputs.GetProperty("gitSha").GetString());

        using var provenance = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(reportRoot, "run-30513567405-provenance.json")));
        Assert.Equal(
            "30513567405",
            provenance.RootElement.GetProperty("authoritativeWorkflowRunId").GetString());
        var excludedRunIds = provenance.RootElement.GetProperty("excludedRuns")
            .EnumerateArray()
            .Select(run => Assert.IsType<string>(
                run.GetProperty("workflowRunId").GetString()))
            .ToArray();
        Assert.Equal(
            [
                "30270567078",
                "30273935931",
                "30400286731",
                "30511810798",
            ],
            excludedRunIds);
        Assert.Equal(
            0,
            provenance.RootElement.GetProperty("bundleVerification")
                .GetProperty("hashMismatches")
                .GetInt32());

        using var attemptMetadata = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(reportRoot, "run-30513567405-attempt-metadata.json")));
        var attempts = attemptMetadata.RootElement.GetProperty("rows").EnumerateArray().ToArray();
        Assert.Equal(72, attempts.Length);
        Assert.Equal(
            164,
            attempts.Sum(row => row.GetProperty("providerRequestCount").GetInt32()));

        var summary = File.ReadAllText(
            Path.Combine(reportRoot, "run-30513567405-summary.md"));
        Assert.Contains("GitHub Copilot Enterprise", summary);
        Assert.Contains("Underpowered", summary);
        Assert.Contains("It is not a retention recommendation.", summary);
    }

    [Fact]
    public void FailedCopilotSdkProbe_IsExcludedBeforeComparisonScheduling()
    {
        var exclusion = File.ReadAllText(Path.Combine(
            FindReportRoot(),
            "run-30511810798-exclusion.md"));

        Assert.Contains("Excluded and not pooled", exclusion);
        Assert.Contains(
            "The full paired scheduler did not start.",
            exclusion);
        Assert.Contains(
            "two probe requests",
            exclusion,
            StringComparison.OrdinalIgnoreCase);
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
            if (Directory.Exists(Path.Combine(candidate, "run-30273935931")))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the retained harness-001 report bundle.");
    }

    private static string CanonicalSha256(string path)
    {
        var text = File.ReadAllText(path).ReplaceLineEndings("\n");
        return Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(text)))
            .ToLowerInvariant();
    }

    private static void AssertBundleChecksums(
        string bundle,
        int expectedCount)
    {
        var checksumLines = File.ReadAllLines(Path.Combine(bundle, "checksums.sha256"));
        Assert.Equal(expectedCount, checksumLines.Length);
        foreach (var line in checksumLines)
        {
            var separator = line.IndexOf("  ", StringComparison.Ordinal);
            Assert.Equal(64, separator);
            var expected = line[..separator];
            var relativePath = line[(separator + 2)..];
            var path = Path.Combine(
                bundle,
                relativePath.Replace('/', Path.DirectorySeparatorChar));
            Assert.True(File.Exists(path), $"Missing retained artifact '{relativePath}'.");
            Assert.Equal(expected, CanonicalSha256(path));
        }
    }
}
