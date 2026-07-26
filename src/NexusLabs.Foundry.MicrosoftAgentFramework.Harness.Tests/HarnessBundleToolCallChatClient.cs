using Microsoft.Extensions.AI;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Tests;

internal sealed class HarnessBundleToolCallChatClient(
    string functionName,
    IReadOnlyDictionary<string, object?> arguments) : IChatClient
{
    private int _callCount;

    internal int CallCount => _callCount;

    internal ChatOptions? FirstCallOptions { get; private set; }

    Task<ChatResponse> IChatClient.GetResponseAsync(
        IEnumerable<ChatMessage> chatMessages,
        ChatOptions? options,
        CancellationToken cancellationToken)
    {
        int callCount = Interlocked.Increment(ref _callCount);
        if (callCount == 1)
        {
            FirstCallOptions = options;
            return Task.FromResult(
                new ChatResponse(
                    new ChatMessage(
                        ChatRole.Assistant,
                        [
                            new FunctionCallContent(
                                "harness-bundle-call",
                                functionName,
                                new Dictionary<string, object?>(arguments)),
                        ]))
                {
                    ModelId = "harness-bundle-test-model",
                    Usage = new UsageDetails
                    {
                        InputTokenCount = 10,
                        OutputTokenCount = 1,
                        TotalTokenCount = 11,
                    },
                });
        }

        return Task.FromResult(
            new ChatResponse(
                new ChatMessage(ChatRole.Assistant, "bundle-complete"))
            {
                ModelId = "harness-bundle-test-model",
                Usage = new UsageDetails
                {
                    InputTokenCount = 20,
                    OutputTokenCount = 2,
                    TotalTokenCount = 22,
                },
            });
    }

    IAsyncEnumerable<ChatResponseUpdate> IChatClient.GetStreamingResponseAsync(
        IEnumerable<ChatMessage> chatMessages,
        ChatOptions? options,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException("Streaming is not required by the bundle tool ingress tests.");

    object? IChatClient.GetService(Type serviceType, object? key) => null;

    void IDisposable.Dispose()
    {
    }
}
