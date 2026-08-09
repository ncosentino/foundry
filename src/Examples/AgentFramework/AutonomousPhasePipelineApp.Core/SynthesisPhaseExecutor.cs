using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;

namespace AutonomousPhasePipelineApp.Core;

internal sealed class SynthesisPhaseExecutor(
    AIAgent agent,
    ReferenceArtifactStore artifacts,
    string runId) :
    Executor<ReferencePhaseArtifact, ReferencePhaseArtifact>(ExecutorId)
{
    internal const string ExecutorId = "synthesis-phase.v1";
    internal const string Phase = "synthesis";

    public override async ValueTask<ReferencePhaseArtifact> HandleAsync(
        ReferencePhaseArtifact message,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        if (message.Artifact is not { } manifestReference)
        {
            return ReferencePhaseArtifact.Skipped(
                Phase,
                ordinal: 200,
                required: true,
                "manifest-missing");
        }

        if (message.Outcome == ReferencePipelineOutcome.Failed)
        {
            return ReferencePhaseArtifact.Skipped(
                Phase,
                ordinal: 200,
                required: true,
                "blocked-by-required-branch",
                manifestReference);
        }

        ReferenceArtifactManifest manifest = artifacts.GetManifest(
            manifestReference,
            runId);
        string branches = string.Join(
            ",",
            manifest.Branches.Select(
                branch => $"{branch.Phase}:{branch.Outcome}"));
        string gaps = manifest.Gaps.Length == 0
            ? "none"
            : string.Join(",", manifest.Gaps);
        string prompt =
            $"""
            Build the synthesis artifact from the accepted manifest.
            manifest_id={manifestReference.Id}
            manifest_outcome={manifest.Outcome}
            branch_outcomes={branches}
            explicit_gaps={gaps}
            Resolve artifact bodies through the manifest tool; no prior transcript is authoritative.
            """;

        try
        {
            AgentSession session = await agent.CreateSessionAsync(
                cancellationToken);
            AgentResponse response = await agent.RunAsync(
                prompt,
                session,
                options: null,
                cancellationToken);
            return ReferencePhaseArtifact.Candidate(
                Phase,
                ordinal: 200,
                required: true,
                response.Text,
                manifestReference);
        }
        catch (OperationCanceledException) when (
            cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (InvalidOperationException exception)
        {
            return ReferencePhaseArtifact.Failed(
                Phase,
                ordinal: 200,
                required: true,
                exception.GetType().Name,
                [manifestReference]);
        }
    }
}
