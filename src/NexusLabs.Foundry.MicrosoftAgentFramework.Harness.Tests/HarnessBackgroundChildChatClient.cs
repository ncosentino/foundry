using Microsoft.Extensions.AI;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Tests;

internal sealed class HarnessBackgroundChildChatClient(
    string response,
    Func<CancellationToken, Task>? beforeResponse = null,
    Exception? exception = null) : IChatClient
{
    private int _callCount;

    internal int CallCount => Volatile.Read(ref _callCount);

    internal bool? LastCancellationCanBeCanceled { get; private set; }

    async Task<ChatResponse> IChatClient.GetResponseAsync(
        IEnumerable<ChatMessage> chatMessages,
        ChatOptions? options,
        CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _callCount);
        LastCancellationCanBeCanceled = cancellationToken.CanBeCanceled;

        if (beforeResponse is not null)
        {
            await beforeResponse(cancellationToken);
        }

        if (exception is not null)
        {
            throw exception;
        }

        return new ChatResponse(
            new ChatMessage(ChatRole.Assistant, response));
    }

    IAsyncEnumerable<ChatResponseUpdate> IChatClient.GetStreamingResponseAsync(
        IEnumerable<ChatMessage> chatMessages,
        ChatOptions? options,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException(
            "Streaming is not required by background-agent provider tests.");

    object? IChatClient.GetService(Type serviceType, object? key) => null;

    void IDisposable.Dispose()
    {
    }
}
