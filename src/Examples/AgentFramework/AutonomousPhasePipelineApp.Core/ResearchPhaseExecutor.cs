using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;

namespace AutonomousPhasePipelineApp.Core;

internal sealed class ResearchPhaseExecutor(
    AIAgent agent) :
    Executor<ReferencePipelineRequest, ReferencePhaseArtifact>(ExecutorId)
{
    internal const string ExecutorId = "research-phase.v1";
    internal const string Phase = "research";

    public override async ValueTask<ReferencePhaseArtifact> HandleAsync(
        ReferencePipelineRequest message,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        AgentSession session = await agent.CreateSessionAsync(cancellationToken);
        AgentResponse response = await agent.RunAsync(
            $"Build the bounded research artifact for topic={message.Topic}",
            session,
            options: null,
            cancellationToken);

        if (agent.GetService<BackgroundAgentsProvider>() is { } provider &&
            provider.GetIncompleteTasks(session).Count != 0)
        {
            return ReferencePhaseArtifact.Failed(
                Phase,
                ordinal: -1,
                required: true,
                "background-tasks-still-running",
                inputs: []);
        }

        return ReferencePhaseArtifact.Candidate(
            Phase,
            ordinal: -1,
            required: true,
            response.Text);
    }
}
