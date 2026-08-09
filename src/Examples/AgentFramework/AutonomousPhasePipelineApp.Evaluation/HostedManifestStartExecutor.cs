using AutonomousPhasePipelineApp.Core;

using Microsoft.Agents.AI.Workflows;

namespace AutonomousPhasePipelineApp.Evaluation;

internal sealed class HostedManifestStartExecutor(
    ReferencePhaseArtifact manifest) :
    Executor<string, ReferencePhaseArtifact>(ExecutorId)
{
    internal const string ExecutorId = "hosted-manifest-start.v1";

    private int _callCount;

    internal int CallCount => Volatile.Read(ref _callCount);

    public override ValueTask<ReferencePhaseArtifact> HandleAsync(
        string message,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _callCount);
        return ValueTask.FromResult(manifest);
    }
}
