using NexusLabs.Foundry.Evaluation.Experiments;

namespace AutonomousPhasePipelineApp.Evaluation;

internal static class HostedEvaluationCaseCatalog
{
    internal static IReadOnlyList<ExperimentCase<HostedEvaluationCase>> Create(
        int trialCount) =>
        Enum.GetValues<HostedEvaluationScenario>()
            .Select(
                (scenario, index) =>
                    new ExperimentCase<HostedEvaluationCase>
                    {
                        Id = $"release-readiness-{scenario}",
                        Value = new HostedEvaluationCase(
                            "release-readiness",
                            scenario,
                            index),
                        TrialCount = trialCount,
                        Tags =
                        [
                            "hosted",
                            "autonomous-phase",
                            scenario.ToString(),
                        ],
                    })
            .ToArray();
}
