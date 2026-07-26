using Microsoft.Extensions.AI;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Tests;

internal sealed class HarnessBundleStreamingLifecycleFailureChatClient(
    bool throwOnAcquire,
    bool throwOnDispose) :
    IChatClient,
    IAsyncEnumerable<ChatResponseUpdate>,
    IAsyncEnumerator<ChatResponseUpdate>
{
    private bool _yielded;

    public ChatResponseUpdate Current { get; private set; } = new()
    {
        Role = ChatRole.Assistant,
        Contents = [new TextContent("partial")],
    };

    Task<ChatResponse> IChatClient.GetResponseAsync(
        IEnumerable<ChatMessage> chatMessages,
        ChatOptions? options,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException("Non-streaming calls are not required by this lifecycle test client.");

    IAsyncEnumerable<ChatResponseUpdate> IChatClient.GetStreamingResponseAsync(
        IEnumerable<ChatMessage> chatMessages,
        ChatOptions? options,
        CancellationToken cancellationToken) =>
        this;

    public IAsyncEnumerator<ChatResponseUpdate> GetAsyncEnumerator(
        CancellationToken cancellationToken = default)
    {
        if (throwOnAcquire)
        {
            throw new InvalidOperationException("stream acquisition failed");
        }

        return this;
    }

    public ValueTask<bool> MoveNextAsync()
    {
        if (_yielded)
        {
            return ValueTask.FromResult(false);
        }

        _yielded = true;
        return ValueTask.FromResult(true);
    }

    public ValueTask DisposeAsync() =>
        throwOnDispose
            ? ValueTask.FromException(new InvalidOperationException("stream dispose failed"))
            : ValueTask.CompletedTask;

    object? IChatClient.GetService(Type serviceType, object? key) => null;

    void IDisposable.Dispose()
    {
    }
}
