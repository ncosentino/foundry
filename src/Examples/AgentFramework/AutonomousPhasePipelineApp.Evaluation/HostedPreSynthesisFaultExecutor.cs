using AutonomousPhasePipelineApp.Core;

using Microsoft.Agents.AI.Workflows;

namespace AutonomousPhasePipelineApp.Evaluation;

internal sealed class HostedPreSynthesisFaultExecutor(
    bool fail) :
    Executor<ReferencePhaseArtifact, ReferencePhaseArtifact>(ExecutorId)
{
    internal const string ExecutorId = "hosted-pre-synthesis-fault.v1";

    public override ValueTask<ReferencePhaseArtifact> HandleAsync(
        ReferencePhaseArtifact message,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        if (fail)
        {
            throw new HttpRequestException(
                "Injected failure after the accepted manifest checkpoint.");
        }

        return ValueTask.FromResult(message);
    }
}
