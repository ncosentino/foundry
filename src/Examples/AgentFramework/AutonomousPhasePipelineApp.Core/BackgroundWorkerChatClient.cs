using Microsoft.Extensions.AI;

namespace AutonomousPhasePipelineApp.Core;

internal sealed class BackgroundWorkerChatClient(
    string result,
    ConcurrentInvocationGate gate) : IChatClient
{
    private int _callCount;

    internal int CallCount => Volatile.Read(ref _callCount);

    async Task<ChatResponse> IChatClient.GetResponseAsync(
        IEnumerable<ChatMessage> chatMessages,
        ChatOptions? options,
        CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _callCount);
        await gate.EnterAsync(cancellationToken);
        return new ChatResponse(
            new ChatMessage(ChatRole.Assistant, result));
    }

    IAsyncEnumerable<ChatResponseUpdate> IChatClient.GetStreamingResponseAsync(
        IEnumerable<ChatMessage> chatMessages,
        ChatOptions? options,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException(
            "Streaming is not required by the offline background worker.");

    object? IChatClient.GetService(Type serviceType, object? key) => null;

    void IDisposable.Dispose()
    {
    }
}
