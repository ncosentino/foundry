using System.Text.Json;

using Microsoft.Extensions.AI;

namespace HarnessHybridApp;

internal sealed class HybridScriptedChatClient(
    string functionName) : IChatClient
{
    private int _callCount;

    Task<ChatResponse> IChatClient.GetResponseAsync(
        IEnumerable<ChatMessage> chatMessages,
        ChatOptions? options,
        CancellationToken cancellationToken)
    {
        int callCount = Interlocked.Increment(ref _callCount);
        if (callCount == 1)
        {
            if (options?.Tools?.Any(tool =>
                string.Equals(tool.Name, functionName, StringComparison.Ordinal)) != true)
            {
                throw new InvalidOperationException(
                    "The generated selected-provider tool was not dispatched.");
            }

            return Task.FromResult(
                new ChatResponse(
                    new ChatMessage(
                        ChatRole.Assistant,
                        [
                            new FunctionCallContent(
                                "selected-provider-call",
                                functionName,
                                new Dictionary<string, object?>()),
                        ]))
                {
                    ModelId = "selected-provider-scripted",
                    Usage = new UsageDetails
                    {
                        InputTokenCount = 8,
                        OutputTokenCount = 1,
                        TotalTokenCount = 9,
                    },
                });
        }

        var functionResult = chatMessages
            .SelectMany(message => message.Contents)
            .OfType<FunctionResultContent>()
            .SingleOrDefault(content =>
                string.Equals(
                    content.CallId,
                    "selected-provider-call",
                    StringComparison.Ordinal));
        string? resultText = functionResult?.Result switch
        {
            string text => text,
            JsonElement
            {
                ValueKind: JsonValueKind.String,
            } json => json.GetString(),
            null => null,
            _ => functionResult.Result.ToString(),
        };
        return Task.FromResult(
            new ChatResponse(
                new ChatMessage(
                    ChatRole.Assistant,
                    $"selected-provider:{resultText ?? "missing"}"))
            {
                ModelId = "selected-provider-scripted",
                Usage = new UsageDetails
                {
                    InputTokenCount = 12,
                    OutputTokenCount = 2,
                    TotalTokenCount = 14,
                },
            });
    }

    IAsyncEnumerable<ChatResponseUpdate> IChatClient.GetStreamingResponseAsync(
        IEnumerable<ChatMessage> chatMessages,
        ChatOptions? options,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException("Streaming is not required by this deterministic example.");

    object? IChatClient.GetService(Type serviceType, object? key) => null;

    void IDisposable.Dispose()
    {
    }
}
