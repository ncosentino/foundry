using System.Text.Json;

using Microsoft.Extensions.AI;

namespace HarnessProviderApp;

/// <summary>
/// Deterministic offline provider used when no real model is configured. It calls the write tool
/// once, then summarizes the tool result, which is enough to exercise the full Harness tool loop
/// without a network call or a subscription.
/// </summary>
internal sealed class ScriptedProviderChatClient : IChatClient
{
    private const string CallId = "scripted-write";
    private int _callCount;

    Task<ChatResponse> IChatClient.GetResponseAsync(
        IEnumerable<ChatMessage> chatMessages,
        ChatOptions? options,
        CancellationToken cancellationToken)
    {
        var callCount = Interlocked.Increment(ref _callCount);
        if (callCount == 1)
        {
            return Task.FromResult(new ChatResponse(
                new ChatMessage(
                    ChatRole.Assistant,
                    [
                        new FunctionCallContent(
                            CallId,
                            "WriteNote",
                            new Dictionary<string, object?>
                            {
                                ["path"] = HarnessProviderRun.NotePath,
                                ["content"] = HarnessProviderRun.ScriptedNote,
                            }),
                    ])));
        }

        var toolResult = chatMessages
            .SelectMany(message => message.Contents)
            .OfType<FunctionResultContent>()
            .LastOrDefault(content =>
                string.Equals(content.CallId, CallId, StringComparison.Ordinal));
        var resultText = toolResult?.Result switch
        {
            string text => text,
            JsonElement { ValueKind: JsonValueKind.String } json => json.GetString(),
            null => "no tool result",
            var other => other.ToString(),
        };
        return Task.FromResult(new ChatResponse(
            new ChatMessage(ChatRole.Assistant, $"Done. Tool reported: {resultText}")));
    }

    IAsyncEnumerable<ChatResponseUpdate> IChatClient.GetStreamingResponseAsync(
        IEnumerable<ChatMessage> chatMessages,
        ChatOptions? options,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException("The scripted provider does not stream.");

    object? IChatClient.GetService(Type serviceType, object? key) =>
        serviceType == typeof(IChatClient) ? this : null;

    void IDisposable.Dispose()
    {
    }
}
