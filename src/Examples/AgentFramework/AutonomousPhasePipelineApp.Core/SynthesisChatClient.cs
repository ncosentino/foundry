using System.Text.Json;

using Microsoft.Extensions.AI;

namespace AutonomousPhasePipelineApp.Core;

internal sealed class SynthesisChatClient(
    string readManifestToolName) : IChatClient
{
    private const string ReadManifestCallId = "synthesis-read-manifest";

    private readonly List<string> _initialPrompts = [];
    private int _artifactResponseCount;
    private int _callCount;

    internal int ArtifactResponseCount =>
        Volatile.Read(ref _artifactResponseCount);

    internal int CallCount => Volatile.Read(ref _callCount);

    internal IReadOnlyList<string> InitialPrompts => _initialPrompts;

    Task<ChatResponse> IChatClient.GetResponseAsync(
        IEnumerable<ChatMessage> chatMessages,
        ChatOptions? options,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Interlocked.Increment(ref _callCount);
        ChatMessage[] messages = [.. chatMessages];
        string combinedText = string.Join(
            "\n",
            messages.Select(message => message.Text));

        string? manifestResult = ScriptedFunctionCall.GetResultText(
            messages,
            ReadManifestCallId);
        if (manifestResult is null)
        {
            string prompt = messages
                .First(message => message.Role == ChatRole.User)
                .Text ?? string.Empty;
            _initialPrompts.Add(prompt);
            string manifestId =
                SynthesisPhaseExecutor.GetManifestId(messages);
            return Task.FromResult(
                ScriptedFunctionCall.Create(
                    ReadManifestCallId,
                    readManifestToolName,
                    new Dictionary<string, object?>
                    {
                        ["manifestId"] = manifestId,
                    },
                    options));
        }

        if (!manifestResult.Contains(
            "ARTIFACT_BODIES",
            StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Synthesis did not receive the artifact manifest bundle.");
        }

        ReferenceArtifactManifest manifest = ParseManifest(manifestResult);
        Interlocked.Increment(ref _artifactResponseCount);
        bool corrected = combinedText.Contains(
            ReferenceArtifactValidator.SynthesisCorrectionCode,
            StringComparison.Ordinal);
        return Task.FromResult(
            new ChatResponse(
                new ChatMessage(
                    ChatRole.Assistant,
                    CreateArtifact(
                        manifest,
                        includeRecommendation: corrected))));
    }

    IAsyncEnumerable<ChatResponseUpdate> IChatClient.GetStreamingResponseAsync(
        IEnumerable<ChatMessage> chatMessages,
        ChatOptions? options,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException(
            "Streaming is not required by the offline synthesis phase.");

    object? IChatClient.GetService(Type serviceType, object? key) => null;

    void IDisposable.Dispose()
    {
    }

    private static ReferenceArtifactManifest ParseManifest(
        string manifestBundle)
    {
        int end = manifestBundle.IndexOfAny(['\r', '\n']);
        string manifestJson = end < 0
            ? manifestBundle
            : manifestBundle[..end];
        return JsonSerializer.Deserialize<ReferenceArtifactManifest>(
            manifestJson) ?? throw new InvalidOperationException(
                "Synthesis received an empty artifact manifest.");
    }

    private static string CreateArtifact(
        ReferenceArtifactManifest manifest,
        bool includeRecommendation)
    {
        string[] evidence =
        [
            manifest.Research.Id,
            .. manifest.Branches
                .Where(branch => branch.Outcome is
                    ReferencePipelineOutcome.Completed or
                    ReferencePipelineOutcome.Partial)
                .Select(branch =>
                    branch.Artifact?.Id ??
                    throw new InvalidOperationException(
                        $"Accepted branch '{branch.Phase}' has no artifact.")),
        ];
        var artifact = new Dictionary<string, object?>
        {
            ["summary"] = includeRecommendation
                ? "Synthesis completed from accepted artifacts."
                : "Initial synthesis is missing a required field.",
            ["evidence"] = evidence,
            ["gaps"] = manifest.Gaps,
        };
        if (includeRecommendation)
        {
            artifact["recommendation"] =
                "Proceed with the synthetic release.";
        }

        return JsonSerializer.Serialize(artifact);
    }
}
