using System.Text.Json;

using Microsoft.Extensions.AI;

namespace AutonomousPhasePipelineApp.Core;

internal sealed class SpecialistChatClient(
    string phase,
    bool fail,
    string readArtifactToolName,
    ConcurrentInvocationGate gate) : IChatClient
{
    private readonly string _readCallId = $"{phase}-read-artifact";
    private int _artifactResponseCount;
    private int _callCount;

    internal int ArtifactResponseCount =>
        Volatile.Read(ref _artifactResponseCount);

    internal int CallCount => Volatile.Read(ref _callCount);

    internal string? LastInitialPrompt { get; private set; }

    async Task<ChatResponse> IChatClient.GetResponseAsync(
        IEnumerable<ChatMessage> chatMessages,
        ChatOptions? options,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Interlocked.Increment(ref _callCount);
        ChatMessage[] messages = [.. chatMessages];
        string? artifactResult = ScriptedFunctionCall.GetResultText(
            messages,
            _readCallId);
        if (artifactResult is null)
        {
            string prompt = messages
                .First(message => message.Role == ChatRole.User)
                .Text ?? string.Empty;
            LastInitialPrompt = prompt;
            string artifactId = ExtractValue(prompt, "artifact_id=");
            return ScriptedFunctionCall.Create(
                _readCallId,
                readArtifactToolName,
                new Dictionary<string, object?>
                {
                    ["artifactId"] = artifactId,
                },
                options);
        }

        await gate.EnterAsync(cancellationToken);
        Interlocked.Increment(ref _artifactResponseCount);
        if (fail)
        {
            throw new InvalidOperationException(
                $"{phase} specialist failed.");
        }

        if (!artifactResult.Contains(
            "evidence-complete",
            StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"{phase} specialist did not receive the research artifact.");
        }

        string artifact = JsonSerializer.Serialize(
            new
            {
                summary = $"{phase} assessment complete",
                evidence = new[]
                {
                    $"{phase}-evidence",
                },
            });
        return new ChatResponse(
            new ChatMessage(ChatRole.Assistant, artifact));
    }

    IAsyncEnumerable<ChatResponseUpdate> IChatClient.GetStreamingResponseAsync(
        IEnumerable<ChatMessage> chatMessages,
        ChatOptions? options,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException(
            "Streaming is not required by the offline specialist.");

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
