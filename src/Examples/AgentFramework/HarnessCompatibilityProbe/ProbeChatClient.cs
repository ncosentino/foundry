using Microsoft.Extensions.AI;

namespace HarnessCompatibilityProbe;

internal sealed class ProbeChatClient : IChatClient
{
    Task<ChatResponse> IChatClient.GetResponseAsync(
        IEnumerable<ChatMessage> chatMessages,
        ChatOptions? options,
        CancellationToken cancellationToken) =>
        Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "probe")));

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
