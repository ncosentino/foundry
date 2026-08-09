using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;

namespace AutonomousPhasePipelineApp.Core;

internal sealed class SpecialistPhaseExecutor(
    ReferenceBranchDefinition branch,
    AIAgent agent) :
    Executor<ReferencePhaseArtifact, ReferencePhaseArtifact>(
        $"specialist-{branch.Phase}.v1")
{
    public override async ValueTask<ReferencePhaseArtifact> HandleAsync(
        ReferencePhaseArtifact message,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        if (message.Artifact is not { } researchArtifact)
        {
            return ReferencePhaseArtifact.Skipped(
                branch.Phase,
                branch.Ordinal,
                branch.Required,
                "research-artifact-missing");
        }

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            timeout.Token);

        try
        {
            AgentSession session = await agent.CreateSessionAsync(linked.Token);
            AgentResponse response = await agent.RunAsync(
                $"""
                Run the {branch.Phase} specialist phase.
                artifact_id={researchArtifact.Id}
                Use the artifact tool; do not rely on a prior agent transcript.
                """,
                session,
                options: null,
                linked.Token);
            return ReferencePhaseArtifact.Candidate(
                branch.Phase,
                branch.Ordinal,
                branch.Required,
                response.Text,
                researchArtifact);
        }
        catch (OperationCanceledException) when (
            cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            return ReferencePhaseArtifact.Failed(
                branch.Phase,
                branch.Ordinal,
                branch.Required,
                "timeout",
                [researchArtifact]);
        }
        catch (InvalidOperationException exception)
        {
            string category = exception.GetType().Name;
            return ReferencePhaseArtifact.Failed(
                branch.Phase,
                branch.Ordinal,
                branch.Required,
                category,
                [researchArtifact]);
        }
    }
}
