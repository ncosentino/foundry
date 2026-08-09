using AutonomousPhasePipelineApp.Core;

using Microsoft.Agents.AI.Workflows;

namespace AutonomousPhasePipelineApp.Evaluation;

internal sealed class HostedSynthesisArm(
    ExecutorBinding executor,
    HostedEvaluationTelemetry telemetry,
    MagenticPhaseProbe? magenticProbe,
    IReadOnlyList<IDisposable> resources) : IDisposable
{
    internal ExecutorBinding Executor { get; } = executor;

    internal HostedEvaluationTelemetry Telemetry { get; } = telemetry;

    internal MagenticPhaseProbe? MagenticProbe { get; } = magenticProbe;

    public void Dispose()
    {
        for (int index = resources.Count - 1; index >= 0; index--)
        {
            resources[index].Dispose();
        }
    }
}
