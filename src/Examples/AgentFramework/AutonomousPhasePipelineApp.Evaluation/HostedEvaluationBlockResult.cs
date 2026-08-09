namespace AutonomousPhasePipelineApp.Evaluation;

internal sealed record HostedEvaluationBlockResult(
    string BlockId,
    string CaseId,
    HostedEvaluationScenario Scenario,
    int Replicate,
    HostedEvaluationArm[] ArmOrder,
    HostedEvaluationArmResult[] Arms);
