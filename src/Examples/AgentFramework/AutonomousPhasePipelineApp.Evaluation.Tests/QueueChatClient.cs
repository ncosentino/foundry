using Microsoft.Extensions.AI;

namespace AutonomousPhasePipelineApp.Evaluation.Tests;

internal sealed class QueueChatClient(
    params string[] responses) : IChatClient
{
    private int _callCount;

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> chatMessages,
        ChatOptions? options,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        int index = Interlocked.Increment(ref _callCount) - 1;
        return Task.FromResult(
            new ChatResponse(
                new ChatMessage(
                    ChatRole.Assistant,
                    responses[index])));
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> chatMessages,
        ChatOptions? options,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public object? GetService(Type serviceType, object? key) => null;

    public void Dispose()
    {
    }
}
