namespace AutonomousPhasePipelineApp.Evaluation;

internal sealed record HostedEvaluationCase(
    string CaseId,
    HostedEvaluationScenario Scenario,
    int ScenarioOrdinal);
