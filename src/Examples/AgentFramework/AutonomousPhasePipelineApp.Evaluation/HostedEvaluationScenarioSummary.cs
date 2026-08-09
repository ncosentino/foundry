namespace AutonomousPhasePipelineApp.Evaluation;

internal sealed record HostedEvaluationScenarioSummary(
    HostedEvaluationArm Arm,
    HostedEvaluationScenario Scenario,
    bool Poolable,
    int AdmissibleResults,
    int PassedResults,
    double? PassRate);
