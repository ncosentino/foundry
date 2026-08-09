namespace AutonomousPhasePipelineApp.Evaluation;

internal static class HostedEvaluationSchedule
{
    private static readonly HostedEvaluationArm[][] s_orders =
    [
        [HostedEvaluationArm.HarnessPlain, HostedEvaluationArm.HarnessDelegated, HostedEvaluationArm.Magentic],
        [HostedEvaluationArm.HarnessPlain, HostedEvaluationArm.Magentic, HostedEvaluationArm.HarnessDelegated],
        [HostedEvaluationArm.HarnessDelegated, HostedEvaluationArm.HarnessPlain, HostedEvaluationArm.Magentic],
        [HostedEvaluationArm.HarnessDelegated, HostedEvaluationArm.Magentic, HostedEvaluationArm.HarnessPlain],
        [HostedEvaluationArm.Magentic, HostedEvaluationArm.HarnessPlain, HostedEvaluationArm.HarnessDelegated],
        [HostedEvaluationArm.Magentic, HostedEvaluationArm.HarnessDelegated, HostedEvaluationArm.HarnessPlain],
    ];

    internal static HostedEvaluationArm[] GetOrder(
        int scenarioOrdinal,
        int trialIndex)
    {
        int index = Math.Abs(
            scenarioOrdinal + trialIndex - 1) % s_orders.Length;
        return [.. s_orders[index]];
    }
}
