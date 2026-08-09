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
