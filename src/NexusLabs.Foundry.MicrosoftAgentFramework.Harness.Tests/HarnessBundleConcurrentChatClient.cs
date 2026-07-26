using Microsoft.Extensions.AI;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Tests;

internal sealed class HarnessBundleConcurrentChatClient : IChatClient
{
    private readonly TaskCompletionSource _bothCallsEntered =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _callCount;

    async Task<ChatResponse> IChatClient.GetResponseAsync(
        IEnumerable<ChatMessage> chatMessages,
        ChatOptions? options,
        CancellationToken cancellationToken)
    {
        if (Interlocked.Increment(ref _callCount) == 2)
        {
            _bothCallsEntered.TrySetResult();
        }

        await _bothCallsEntered.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        return new ChatResponse(new ChatMessage(ChatRole.Assistant, "concurrent"))
        {
            ModelId = "harness-bundle-concurrent-model",
            Usage = new UsageDetails
            {
                InputTokenCount = 4,
                OutputTokenCount = 1,
                TotalTokenCount = 5,
            },
        };
    }

    IAsyncEnumerable<ChatResponseUpdate> IChatClient.GetStreamingResponseAsync(
        IEnumerable<ChatMessage> chatMessages,
        ChatOptions? options,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException("Streaming is not required by this concurrency test client.");

    object? IChatClient.GetService(Type serviceType, object? key) => null;

    void IDisposable.Dispose()
    {
    }
}
