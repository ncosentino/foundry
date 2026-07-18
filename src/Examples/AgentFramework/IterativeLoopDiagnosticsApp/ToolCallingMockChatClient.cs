using Microsoft.Extensions.AI;

using NexusLabs.Needlr;

namespace IterativeLoopDiagnosticsApp;

[DoNotAutoRegister]
internal sealed class ToolCallingMockChatClient : IChatClient
{
    private int _callCount;

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options,
        CancellationToken cancellationToken)
    {
        var callCount = Interlocked.Increment(ref _callCount);

        if (callCount == 1)
        {
            var response = new ChatResponse(
            [
                new ChatMessage(ChatRole.Assistant,
                [
                    new FunctionCallContent(
                        "call-1",
                        "SearchArticles",
                        new Dictionary<string, object?> { ["query"] = "diagnostics" }),
                ]),
            ])
            {
                ModelId = "mock-model",
                Usage = new UsageDetails
                {
                    InputTokenCount = 150,
                    OutputTokenCount = 30,
                    TotalTokenCount = 180,
                },
            };
            return Task.FromResult(response);
        }

        var textResponse = new ChatResponse(
            [new ChatMessage(ChatRole.Assistant, "Here are the results from the search.")])
        {
            ModelId = "mock-model",
            Usage = new UsageDetails
            {
                InputTokenCount = 200,
                OutputTokenCount = 80,
                TotalTokenCount = 280,
            },
        };
        return Task.FromResult(textResponse);
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException("Streaming not used by IterativeAgentLoop");

    public void Dispose()
    {
    }

    public object? GetService(Type serviceType, object? serviceKey) => null;
}
