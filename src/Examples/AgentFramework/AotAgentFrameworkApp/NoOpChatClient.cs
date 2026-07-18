using Microsoft.Extensions.AI;

using NexusLabs.Needlr;

namespace AotAgentFrameworkApp;

/// <summary>
/// Provides a NativeAOT-compatible chat client for exercising Foundry's Needlr integration.
/// </summary>
[DoNotAutoRegister]
internal sealed class NoOpChatClient : IChatClient
{
    public ChatClientMetadata Metadata { get; } = new("no-op");

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> chatMessages,
        ChatOptions? options,
        CancellationToken cancellationToken) =>
        Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "No-op response")));

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> chatMessages,
        ChatOptions? options,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException("Streaming not supported in no-op client.");

    public void Dispose()
    {
    }

    public object? GetService(Type serviceType, object? key) => null;
}
