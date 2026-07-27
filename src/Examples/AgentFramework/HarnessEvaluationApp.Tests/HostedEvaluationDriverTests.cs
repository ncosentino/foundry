using System.Text.Json;

using NexusLabs.Foundry.Evaluation.Harness;

namespace HarnessEvaluationApp.Tests;

public sealed class HostedEvaluationDriverTests
{
    [Fact]
    public async Task DryRun_ProducesCompletePairedArtifactBundle()
    {
        var outputDirectory = Path.Combine(
            Path.GetTempPath(),
            $"foundry-harness-evaluation-{Guid.NewGuid():N}");
        try
        {
            var options = new HostedEvaluationOptions(
                outputDirectory,
                ModelId: "scripted",
                DryRun: true,
                GlobalRunSeed: 137,
                BatchOrderingSeed: 104729,
                ArmOrderingSeed: 130363,
                BootstrapSeed: 155921,
                MaximumAttempts: 144,
                MaximumRequests: 1152,
                MaximumRequestsPerAttempt: 8,
                SchedulingDeadlineMinutes: 50,
                AttemptTimeoutSeconds: 1,
                MaximumConcurrency: 3,
                CostCapUsd: 25,
                EstimatedCostPerRequest: 0.02m);
            var driver = new HostedEvaluationDriver(options, realChatClientFactory: null);

            var state = await driver.RunAsync(CancellationToken.None);

            Assert.Equal(HarnessHostedRunState.Completed, state);
            Assert.Equal(24, Directory.GetFiles(
                Path.Combine(outputDirectory, "batches"),
                "*.json").Length);
            using var ledger = JsonDocument.Parse(
                File.ReadAllText(Path.Combine(outputDirectory, "ledger", "trial-records.json")));
            Assert.Equal(72, ledger.RootElement.GetArrayLength());
            using var status = JsonDocument.Parse(
                File.ReadAllText(Path.Combine(outputDirectory, "run-status.json")));
            Assert.Equal("Completed", status.RootElement.GetProperty("State").GetString());
            Assert.Equal(24, status.RootElement.GetProperty("ScheduledBatchCount").GetInt32());
            Assert.InRange(
                status.RootElement.GetProperty("ProviderRequestsUsed").GetInt32(),
                low: 1,
                high: 150);
            Assert.True(File.Exists(Path.Combine(outputDirectory, "comparison-artifact.json")));
            Assert.True(File.Exists(Path.Combine(outputDirectory, "checksums.sha256")));
        }
        finally
        {
            if (Directory.Exists(outputDirectory))
            {
                Directory.Delete(outputDirectory, recursive: true);
            }
        }
    }
}
