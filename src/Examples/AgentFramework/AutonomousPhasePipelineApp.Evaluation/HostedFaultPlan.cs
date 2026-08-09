namespace AutonomousPhasePipelineApp.Evaluation;

internal sealed record HostedFaultPlan(
    HostedFaultMode Mode,
    HostedFaultActivationPoint ActivationPoint,
    HostedEvaluationAgentRole TargetRole,
    bool MustActivate)
{
    internal static HostedFaultPlan None { get; } = new(
        HostedFaultMode.None,
        HostedFaultActivationPoint.None,
        HostedEvaluationAgentRole.SynthesisParent,
        MustActivate: false);
}
