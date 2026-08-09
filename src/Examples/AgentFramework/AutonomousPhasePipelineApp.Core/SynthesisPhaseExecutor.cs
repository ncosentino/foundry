using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

namespace AutonomousPhasePipelineApp.Core;

internal sealed class SynthesisPhaseExecutor(
    AIAgent agent,
    ReferenceArtifactStore artifacts,
    string runId) :
    Executor<ReferencePhaseArtifact, ReferencePhaseArtifact>(ExecutorId)
{
    internal const string ExecutorId = "synthesis-phase.v1";
    internal const string Phase = "synthesis";
    internal const string ManifestIdPrefix = "manifest_id=";

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
        string prompt = ReferenceSynthesisPrompt.Build(
            manifestReference,
            manifest);

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

    internal static string GetManifestId(
        IEnumerable<ChatMessage> messages)
    {
        string prompt = messages
            .FirstOrDefault(message => message.Role == ChatRole.User)
            ?.Text ?? throw new InvalidOperationException(
                "Synthesis messages did not contain a user prompt.");
        int start = prompt.IndexOf(
            ManifestIdPrefix,
            StringComparison.Ordinal);
        if (start < 0)
        {
            throw new InvalidOperationException(
                $"Synthesis prompt did not contain '{ManifestIdPrefix}'.");
        }

        start += ManifestIdPrefix.Length;
        int end = prompt.IndexOf('\n', start);
        string manifestId =
            (end < 0 ? prompt[start..] : prompt[start..end]).Trim();
        if (string.IsNullOrWhiteSpace(manifestId))
        {
            throw new InvalidOperationException(
                "Synthesis prompt contained an empty manifest ID.");
        }

        return manifestId;
    }
}
