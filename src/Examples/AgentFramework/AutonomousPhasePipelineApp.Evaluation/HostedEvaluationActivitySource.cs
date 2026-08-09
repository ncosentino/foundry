using System.Diagnostics;

namespace AutonomousPhasePipelineApp.Evaluation;

internal static class HostedEvaluationActivitySource
{
    internal const string Name =
        "NexusLabs.Foundry.AutonomousPhaseEvaluation";

    internal static ActivitySource Source { get; } = new(Name);
}
