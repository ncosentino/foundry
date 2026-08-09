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

        if (combinedText.Contains(
            ReferenceArtifactValidator.SynthesisCorrectionCode,
            StringComparison.Ordinal))
        {
            Interlocked.Increment(ref _artifactResponseCount);
            return Task.FromResult(
                new ChatResponse(
                    new ChatMessage(
                        ChatRole.Assistant,
                        """
                        {
                          "summary": "Synthesis completed from accepted artifacts.",
                          "evidence": ["research", "required-specialist"],
                          "recommendation": "Proceed with the synthetic release."
                        }
                        """)));
        }

        string? manifestResult = ScriptedFunctionCall.GetResultText(
            messages,
            ReadManifestCallId);
        if (manifestResult is null)
        {
            string prompt = messages
                .First(message => message.Role == ChatRole.User)
                .Text ?? string.Empty;
            _initialPrompts.Add(prompt);
            string manifestId = ExtractValue(prompt, "manifest_id=");
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

        Interlocked.Increment(ref _artifactResponseCount);
        return Task.FromResult(
            new ChatResponse(
                new ChatMessage(
                    ChatRole.Assistant,
                    """
                    {
                      "summary": "Initial synthesis is missing a required field.",
                      "evidence": ["research"]
                    }
                    """)));
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

    private static string ExtractValue(
        string prompt,
        string prefix)
    {
        int start = prompt.IndexOf(prefix, StringComparison.Ordinal);
        if (start < 0)
        {
            throw new InvalidOperationException(
                $"Prompt did not contain '{prefix}'.");
        }

        start += prefix.Length;
        int end = prompt.IndexOf('\n', start);
        return (end < 0 ? prompt[start..] : prompt[start..end]).Trim();
    }
}
