using System.Runtime.CompilerServices;

using Microsoft.Extensions.AI;

namespace AutonomousPhasePipelineApp.Core;

internal sealed class MagenticParticipantChatClient(
    string participantName,
    string responseText,
    string? readManifestToolName,
    ReferenceSynthesisBudget budget) : IChatClient
{
    private readonly string _readCallId = $"{participantName}-read-manifest";
    private int _callCount;

    internal int CallCount => Volatile.Read(ref _callCount);

    Task<ChatResponse> IChatClient.GetResponseAsync(
        IEnumerable<ChatMessage> chatMessages,
        ChatOptions? options,
        CancellationToken cancellationToken) =>
        GetResponseAsync(
            chatMessages,
            options,
            cancellationToken);

    async IAsyncEnumerable<ChatResponseUpdate>
        IChatClient.GetStreamingResponseAsync(
            IEnumerable<ChatMessage> chatMessages,
            ChatOptions? options,
            [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ChatResponse response = await GetResponseAsync(
            chatMessages,
            options,
            cancellationToken);
        ChatMessage message = response.Messages.Single();
        yield return new ChatResponseUpdate
        {
            Role = message.Role,
            AuthorName = participantName,
            MessageId = message.MessageId,
            Contents = [.. message.Contents],
        };
    }

    object? IChatClient.GetService(Type serviceType, object? key) => null;

    void IDisposable.Dispose()
    {
    }

    private Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> chatMessages,
        ChatOptions? options,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        budget.ConsumeProviderCall(participantName);
        Interlocked.Increment(ref _callCount);
        ChatMessage[] messages = [.. chatMessages];
        if (readManifestToolName is not null)
        {
            string? result = ScriptedFunctionCall.GetResultText(
                messages,
                _readCallId);
            if (result is null)
            {
                string manifestId = ExtractManifestId(messages);
                return Task.FromResult(
                    ScriptedFunctionCall.Create(
                        _readCallId,
                        readManifestToolName,
                        new Dictionary<string, object?>
                        {
                            ["manifestId"] = manifestId,
                        },
                        options));
            }

            if (!result.Contains(
                "ARTIFACT_BODIES",
                StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"{participantName} did not receive the artifact manifest.");
            }
        }

        return Task.FromResult(
            new ChatResponse(
                new ChatMessage(
                    ChatRole.Assistant,
                    responseText)));
    }

    private static string ExtractManifestId(
        IEnumerable<ChatMessage> messages)
    {
        const string Prefix = "manifest_id=";
        foreach (string text in messages
            .Select(message => message.Text)
            .OfType<string>()
            .Reverse())
        {
            int start = text.IndexOf(Prefix, StringComparison.Ordinal);
            if (start < 0)
            {
                continue;
            }

            start += Prefix.Length;
            int end = text.IndexOf('\n', start);
            return (end < 0 ? text[start..] : text[start..end]).Trim();
        }

        throw new InvalidOperationException(
            "The Magentic participant instruction omitted the manifest ID.");
    }
}
