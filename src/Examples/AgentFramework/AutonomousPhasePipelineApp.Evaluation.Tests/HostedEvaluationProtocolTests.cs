using System.Text.Json;

using AutonomousPhasePipelineApp.Evaluation;

namespace AutonomousPhasePipelineApp.Evaluation.Tests;

public sealed class HostedEvaluationProtocolTests
{
    [Fact]
    public void CaseCatalog_CoversEveryScenario()
    {
        var cases = HostedEvaluationCaseCatalog.Create(
            trialCount: 1);

        Assert.Equal(
            Enum.GetValues<HostedEvaluationScenario>().Length,
            cases.Count);
        Assert.Equal(
            Enum.GetValues<HostedEvaluationScenario>(),
            cases.Select(@case => @case.Value.Scenario));
        Assert.Equal(
            "autonomous-phase-eval-v2",
            HostedEvaluationProtocol.Version);
    }

    [Fact]
    public void DatasetCatalog_IsStableAndDoesNotLeakExpectedOutputIntoInput()
    {
        IReadOnlyList<HostedEvaluationDatasetItem> first =
            HostedEvaluationDatasetCatalog.Create();
        IReadOnlyList<HostedEvaluationDatasetItem> second =
            HostedEvaluationDatasetCatalog.Create();

        Assert.Equal(
            Enum.GetValues<HostedEvaluationScenario>().Length,
            first.Count);
        Assert.Equal(
            first.Select(item => item.Id),
            second.Select(item => item.Id));
        Assert.Equal(
            first.Select(item => item.Metadata.FixtureDigest),
            second.Select(item => item.Metadata.FixtureDigest));
        Assert.Equal(
            first.Count,
            first.Select(item => item.Id)
                .Distinct(StringComparer.Ordinal)
                .Count());
        Assert.All(
            first,
            item =>
            {
                Assert.Equal(
                    HostedSynthesisArmFactory.MaxProviderCalls,
                    item.Input.MaxProviderCalls);
                Assert.Equal(
                    HostedSynthesisArmFactory.MaxArtifactAttempts,
                    item.Input.MaxArtifactAttempts);
                Assert.Equal("validation", item.Metadata.Split);
                string inputJson = JsonSerializer.Serialize(
                    item.Input);
                Assert.All(
                    item.ExpectedOutput.EvidenceIds,
                    evidence => Assert.DoesNotContain(
                        evidence,
                        inputJson,
                        StringComparison.Ordinal));
            });
    }

    [Fact]
    public void Schedule_BalancesAllSixArmOrders()
    {
        HostedEvaluationArm[][] orders = Enumerable
            .Range(1, 6)
            .Select(trial => HostedEvaluationSchedule.GetOrder(
                scenarioOrdinal: 0,
                trial))
            .ToArray();

        Assert.Equal(
            6,
            orders
                .Select(order => string.Join(",", order))
                .Distinct(StringComparer.Ordinal)
                .Count());
        Assert.All(
            orders,
            order => Assert.Equal(3, order.Distinct().Count()));
    }
}
