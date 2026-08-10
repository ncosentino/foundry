using Microsoft.Agents.AI.Workflows;

namespace AutonomousPhasePipelineApp.Core;

internal sealed class IntakeExecutor() :
    Executor<ReferencePipelineRequest, ReferencePipelineRequest>(ExecutorId)
{
    internal const string ExecutorId = "intake.v1";

    public override ValueTask<ReferencePipelineRequest> HandleAsync(
        ReferencePipelineRequest message,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message.RunId);
        ArgumentException.ThrowIfNullOrWhiteSpace(message.Topic);
        return ValueTask.FromResult(message);
    }
}
