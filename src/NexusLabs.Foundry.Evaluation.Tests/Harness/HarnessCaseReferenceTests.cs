using System.Security.Cryptography;
using System.Text.Json;

using NexusLabs.Foundry.Evaluation.Harness;

namespace NexusLabs.Foundry.Evaluation.Tests.Harness;

public sealed class HarnessCaseReferenceTests
{
    private static readonly HashSet<string> KnownMetricNames = new(StringComparer.Ordinal)
    {
        HarnessArtifactRehydrationEvaluator.DigestVerifiedMetricName,
        HarnessArtifactRehydrationEvaluator.RehydrationConsistentMetricName,
        HarnessArtifactRehydrationEvaluator.ResolvedCountMetricName,
        HarnessArtifactReuseEvaluator.ByteSavingsMetricName,
        HarnessArtifactReuseEvaluator.OffloadConsistentMetricName,
        HarnessArtifactReuseEvaluator.OffloadCountMetricName,
        HarnessArtifactReuseEvaluator.ReuseCountMetricName,
        HarnessCancellationEvaluator.AppropriateMetricName,
        HarnessCancellationEvaluator.CategoryMatchMetricName,
        HarnessCancellationEvaluator.NoSuccessShapedOutputMetricName,
        HarnessCompactionValidityEvaluator.OutcomeConsistentMetricName,
        HarnessCompactionValidityEvaluator.ReducedMonotonicMetricName,
        HarnessContextSafetyEvaluator.NoOverflowMetricName,
        HarnessContextSafetyEvaluator.StructurallyValidMetricName,
        HarnessCostAttributionEvaluator.AttributionValidMetricName,
        HarnessDiagnosticsSchemaProfileEvaluator.SchemaCompleteMetricName,
        HarnessSessionContinuityEvaluator.ContinuityPreservedMetricName,
        HarnessToolTrajectoryEvaluator.TrajectoryCompliantMetricName,
        TerminationAppropriatenessEvaluator.RunSucceededMetricName,
        TerminationAppropriatenessEvaluator.TerminationConsistentMetricName,
    };

    [Fact]
    public void HostedReferences_ExistMatchManifestIdentityAndPinnedDigest()
    {
        var manifestPath = HarnessManifestTestFiles.TryFindManifestPath();
        Assert.SkipWhen(manifestPath is null, "The on-disk harness-001 v1.0 manifest was not found.");

        using var manifestDocument = JsonDocument.Parse(File.ReadAllText(manifestPath!));
        var caseSetDirectory = Path.GetDirectoryName(manifestPath!)!;
        var hostedReferenceCount = 0;

        foreach (var manifestCase in manifestDocument.RootElement.GetProperty("cases").EnumerateArray())
        {
            if (manifestCase.GetProperty("development").GetBoolean())
            {
                continue;
            }

            var caseId = manifestCase.GetProperty("id").GetString()!;
            foreach (var reference in manifestCase.GetProperty("deterministicReferences").EnumerateArray())
            {
                hostedReferenceCount++;
                var referenceId = reference.GetProperty("referenceId").GetString()!;
                var dimension = reference.GetProperty("dimension").GetString()!;
                var relativePath = reference.GetProperty("relativePath").GetString()!;
                Assert.True(
                    reference.TryGetProperty("sha256", out var sha256),
                    $"Reference '{referenceId}' does not pin a SHA-256 digest.");
                var expectedDigest = sha256.GetString();

                Assert.False(string.IsNullOrWhiteSpace(expectedDigest));

                var absolutePath = Path.GetFullPath(
                    Path.Combine(caseSetDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar)));
                Assert.StartsWith(
                    Path.GetFullPath(Path.Combine(caseSetDirectory, "cases")) + Path.DirectorySeparatorChar,
                    absolutePath,
                    StringComparison.OrdinalIgnoreCase);
                Assert.True(File.Exists(absolutePath), $"Reference file '{relativePath}' does not exist.");
                Assert.Equal(expectedDigest, ComputeSha256(absolutePath));

                using var referenceDocument = JsonDocument.Parse(File.ReadAllText(absolutePath));
                var root = referenceDocument.RootElement;
                Assert.Equal("1.0", root.GetProperty("schemaVersion").GetString());
                Assert.Equal("harness-001", root.GetProperty("caseSetId").GetString());
                Assert.Equal("v1.0", root.GetProperty("caseSetVersion").GetString());
                Assert.Equal(caseId, root.GetProperty("caseId").GetString());
                Assert.Equal(referenceId, root.GetProperty("referenceId").GetString());
                Assert.Equal(dimension, root.GetProperty("dimension").GetString());
                Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("aggregation").GetString()));

                ValidateContract(root, relativePath);
            }
        }

        Assert.Equal(30, hostedReferenceCount);
    }

    private static void ValidateContract(JsonElement root, string relativePath)
    {
        var contractKind = root.GetProperty("contractKind").GetString();
        switch (contractKind)
        {
            case "DeterministicAssertions":
                var assertions = root.GetProperty("assertions").EnumerateArray().ToArray();
                Assert.NotEmpty(assertions);
                foreach (var assertion in assertions)
                {
                    Assert.Contains(
                        assertion.GetProperty("operator").GetString(),
                        new[] { "Contains", "Equals", "Exists", "GreaterThanOrEqual" });
                    if (assertion.GetProperty("source").GetString() == "EvaluationMetric")
                    {
                        var metricName = assertion.GetProperty("metricName").GetString();
                        Assert.NotNull(metricName);
                        Assert.Contains(metricName, KnownMetricNames);
                    }
                }

                break;

            case "ContinuousMeasurement":
                var measurement = root.GetProperty("measurement");
                Assert.Equal("LowerIsBetter", measurement.GetProperty("direction").GetString());
                Assert.Equal("DualFullSuccess", measurement.GetProperty("conditionalEligibility").GetString());
                Assert.Equal(
                    "ExcludeSymmetrically",
                    measurement.GetProperty("unscheduledTreatment").GetString());
                Assert.False(string.IsNullOrWhiteSpace(
                    measurement.GetProperty("pessimisticScheduledFailureValueSource").GetString()));
                break;

            case "DiagnosticsParity":
                Assert.NotEmpty(root.GetProperty("requiredCoreFields").EnumerateArray());
                Assert.Equal(
                    "ExactCoreFieldComparison",
                    root.GetProperty("comparisonRule").GetString());
                break;

            default:
                Assert.Fail($"Reference '{relativePath}' has unknown contract kind '{contractKind}'.");
                break;
        }
    }

    private static string ComputeSha256(string path) =>
        Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
}
