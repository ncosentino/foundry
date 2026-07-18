using Microsoft.Extensions.AI;

using NexusLabs.Needlr;

namespace DiagnosticAttributionApp;

[DoNotAutoRegister]
internal sealed class MockChatClient : IChatClient
{
    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options,
        CancellationToken cancellationToken)
    {
        var response = new ChatResponse([new ChatMessage(ChatRole.Assistant, "Mock response")])
        {
            ModelId = "mock-model",
        };
        return Task.FromResult(response);
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public void Dispose()
    {
    }

    public object? GetService(Type serviceType, object? serviceKey) => null;
}
