using Microsoft.Extensions.AI;

namespace HarnessCompatibilityProbe;

internal sealed class ProbeChatClient(
    string functionName,
    LifecycleTrace? lifecycleTrace = null) : IChatClient
{
    private int _callCount;

    Task<ChatResponse> IChatClient.GetResponseAsync(
        IEnumerable<ChatMessage> chatMessages,
        ChatOptions? options,
        CancellationToken cancellationToken)
    {
        var call = Interlocked.Increment(ref _callCount);
        lifecycleTrace?.Add($"chat.call:{call}");

        var messageList = chatMessages.ToList();
        if (messageList.Any(message => message.Text?.Contains("history-probe", StringComparison.Ordinal) is true))
        {
            lifecycleTrace?.Add("history.seen");
        }
        if (messageList.Any(message => message.Text?.Contains("injected-probe", StringComparison.Ordinal) is true))
        {
            lifecycleTrace?.Add("injected.seen");
        }
        if (options?.Instructions?.Contains("context-probe", StringComparison.Ordinal) is true)
        {
            lifecycleTrace?.Add("context.seen");
        }

        if (call == 1)
        {
            return Task.FromResult(
                new ChatResponse(
                    new ChatMessage(
                        ChatRole.Assistant,
                        [
                            new FunctionCallContent(
                                "probe-call",
                                functionName,
                                new Dictionary<string, object?> { ["value"] = "aot" }),
                        ])));
        }

        var functionResults = messageList
            .SelectMany(message => message.Contents)
            .OfType<FunctionResultContent>()
            .ToList();
        var result = functionResults.Count == 1 &&
            string.Equals(functionResults[0].CallId, "probe-call", StringComparison.Ordinal) &&
            functionResults[0].Result is string value
                ? value
                : "invalid";

        return Task.FromResult(
            new ChatResponse(
                new ChatMessage(ChatRole.Assistant, $"tool-result:{result}")));
    }

    IAsyncEnumerable<ChatResponseUpdate> IChatClient.GetStreamingResponseAsync(
        IEnumerable<ChatMessage> chatMessages,
        ChatOptions? options,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException("Streaming is not required by the compatibility probe.");

    object? IChatClient.GetService(Type serviceType, object? key) => null;

    void IDisposable.Dispose()
    {
    }
}
