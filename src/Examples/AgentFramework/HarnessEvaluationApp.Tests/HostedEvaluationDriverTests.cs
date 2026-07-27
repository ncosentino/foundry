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
                MaximumOutputTokens: 2000,
                MinimumProviderRequestIntervalMilliseconds: 0,
                WorkflowTimeoutMinutes: 60,
                SchedulingDeadlineMinutes: 50,
                AttemptTimeoutSeconds: 3,
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
            Assert.All(ledger.RootElement.EnumerateArray(), row =>
            {
                Assert.True(row.GetProperty("Scheduled").GetBoolean());
                var evidenceReference = row.GetProperty("EvidenceArtifactReference").GetString();
                Assert.False(string.IsNullOrWhiteSpace(evidenceReference));
                Assert.True(File.Exists(Path.Combine(
                    outputDirectory,
                    evidenceReference!.Replace('/', Path.DirectorySeparatorChar))));
                var captureReference = row.GetProperty("ResponseCaptureReference").GetString();
                Assert.False(string.IsNullOrWhiteSpace(captureReference));
                var capturePath = Path.Combine(
                    outputDirectory,
                    captureReference!.Replace('/', Path.DirectorySeparatorChar));
                Assert.True(File.Exists(capturePath));
                using var captureManifest = JsonDocument.Parse(File.ReadAllText(capturePath));
                Assert.NotEmpty(captureManifest.RootElement.GetProperty("AttemptDirectories").EnumerateArray());
            });
            var batchReferenceCounts = ledger.RootElement
                .EnumerateArray()
                .GroupBy(row => row.GetProperty("EvidenceArtifactReference").GetString())
                .ToArray();
            Assert.Equal(24, batchReferenceCounts.Length);
            Assert.All(batchReferenceCounts, group => Assert.Equal(3, group.Count()));
            using var runPlan = JsonDocument.Parse(
                File.ReadAllText(Path.Combine(outputDirectory, "inputs", "run-plan.json")));
            Assert.Equal(1152, runPlan.RootElement.GetProperty("MaximumRequests").GetInt32());
            Assert.Equal(2000, runPlan.RootElement.GetProperty("MaximumOutputTokens").GetInt32());
            Assert.Equal(25, runPlan.RootElement.GetProperty("CostCapUsd").GetDecimal());
            var iterativeTimeoutRows = ledger.RootElement
                .EnumerateArray()
                .Where(row =>
                    row.GetProperty("Arm").GetString() == "Iterative" &&
                    row.GetProperty("CaseId").GetString() == "h001-05")
                .ToArray();
            Assert.Equal(3, iterativeTimeoutRows.Length);
            Assert.All(iterativeTimeoutRows, row =>
            {
                var completion = Assert.Single(
                    row.GetProperty("BinaryValues").EnumerateArray(),
                    value => value.GetProperty("Dimension").GetString() == "Completion");
                Assert.True(completion.GetProperty("Value").GetBoolean());
            });
            using var status = JsonDocument.Parse(
                File.ReadAllText(Path.Combine(outputDirectory, "run-status.json")));
            Assert.Equal("Completed", status.RootElement.GetProperty("State").GetString());
            Assert.Equal(24, status.RootElement.GetProperty("ScheduledBatchCount").GetInt32());
            Assert.InRange(
                status.RootElement.GetProperty("ProviderRequestsUsed").GetInt32(),
                low: 1,
                high: 150);
            Assert.True(File.Exists(Path.Combine(outputDirectory, "comparison-artifact.json")));
            Assert.True(File.Exists(Path.Combine(outputDirectory, "judge", "omission.json")));
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

    [Fact]
    public async Task CanceledCaller_FinalizesExplicitUnscheduledBundle()
    {
        var outputDirectory = Path.Combine(
            Path.GetTempPath(),
            $"foundry-harness-evaluation-canceled-{Guid.NewGuid():N}");
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
                MaximumOutputTokens: 2000,
                MinimumProviderRequestIntervalMilliseconds: 0,
                WorkflowTimeoutMinutes: 60,
                SchedulingDeadlineMinutes: 50,
                AttemptTimeoutSeconds: 3,
                MaximumConcurrency: 3,
                CostCapUsd: 25,
                EstimatedCostPerRequest: 0.02m);
            var driver = new HostedEvaluationDriver(options, realChatClientFactory: null);
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            var state = await driver.RunAsync(cancellation.Token);

            Assert.Equal(HarnessHostedRunState.CanceledByCaller, state);
            using var ledger = JsonDocument.Parse(
                File.ReadAllText(Path.Combine(outputDirectory, "ledger", "trial-records.json")));
            Assert.Equal(72, ledger.RootElement.GetArrayLength());
            Assert.All(
                ledger.RootElement.EnumerateArray(),
                row => Assert.False(row.GetProperty("Scheduled").GetBoolean()));
            Assert.True(File.Exists(Path.Combine(outputDirectory, "comparison-artifact.json")));
            Assert.True(File.Exists(Path.Combine(outputDirectory, "run-status.json")));
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

    [Fact]
    public void TransientProviderFailure_RecognizesWrappedStatusCodes()
    {
        var wrapped429 = new InvalidOperationException(
            "wrapped",
            new HttpRequestException(
                "rate limited",
                inner: null,
                System.Net.HttpStatusCode.TooManyRequests));
        var wrapped503 = new InvalidOperationException(
            "wrapped",
            new HttpRequestException(
                "unavailable",
                inner: null,
                System.Net.HttpStatusCode.ServiceUnavailable));

        Assert.True(HostedEvaluationDriver.IsTransientProviderFailure(wrapped429));
        Assert.True(HostedEvaluationDriver.IsTransientProviderFailure(wrapped503));
        Assert.False(HostedEvaluationDriver.IsTransientProviderFailure(
            new InvalidOperationException("deterministic failure")));
    }
}
