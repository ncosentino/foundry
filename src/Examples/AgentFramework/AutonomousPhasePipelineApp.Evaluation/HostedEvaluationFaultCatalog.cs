namespace AutonomousPhasePipelineApp.Evaluation;

internal static class HostedEvaluationFaultCatalog
{
    internal static HostedFaultPlan GetPlan(
        HostedEvaluationScenario scenario,
        HostedEvaluationArm arm)
    {
        HostedEvaluationAgentRole primaryRole =
            arm == HostedEvaluationArm.Magentic
                ? HostedEvaluationAgentRole.MagenticManager
                : HostedEvaluationAgentRole.SynthesisParent;
        return scenario switch
        {
            HostedEvaluationScenario.CorrectionSucceeds => new(
                HostedFaultMode.InvalidFirstTerminal,
                HostedFaultActivationPoint.AfterTerminalProviderResponse,
                primaryRole,
                MustActivate: true),
            HostedEvaluationScenario.CorrectionExhausted => new(
                HostedFaultMode.InvalidEveryTerminal,
                HostedFaultActivationPoint.AfterTerminalProviderResponse,
                primaryRole,
                MustActivate: true),
            HostedEvaluationScenario.Cancellation => new(
                HostedFaultMode.DelayFirstTerminalUntilCanceled,
                HostedFaultActivationPoint.AfterTerminalProviderResponse,
                primaryRole,
                MustActivate: true),
            HostedEvaluationScenario.IneffectiveProgress
                when arm == HostedEvaluationArm.Magentic => new(
                    HostedFaultMode.ForceFirstMagenticStall,
                    HostedFaultActivationPoint.AfterMagenticProgressLedger,
                    HostedEvaluationAgentRole.MagenticManager,
                    MustActivate: true),
            HostedEvaluationScenario.IneffectiveProgress => new(
                HostedFaultMode.InvalidFirstTerminal,
                HostedFaultActivationPoint.AfterTerminalProviderResponse,
                primaryRole,
                MustActivate: true),
            _ => HostedFaultPlan.None,
        };
    }
}
