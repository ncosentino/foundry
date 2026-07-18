using Microsoft.Extensions.AI;

using NexusLabs.Needlr;

namespace GenAiTokenMetricsApp;

[DoNotAutoRegister]
internal sealed class MockChatClientWithFullUsage : IChatClient
{
    private readonly ChatClientMetadata _metadata = new(
        providerName: "mock-provider",
        providerUri: new Uri("https://api.example.com:443"),
        defaultModelId: "mock-model");

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options,
        CancellationToken cancellationToken)
    {
        var response = new ChatResponse(
            [new ChatMessage(ChatRole.Assistant, "Mock summary response.")])
        {
            ModelId = "mock-model",
            Usage = new UsageDetails
            {
                InputTokenCount = 5000,
                OutputTokenCount = 250,
                TotalTokenCount = 5250,
                CachedInputTokenCount = 3000,
                ReasoningTokenCount = 150,
            },
        };
        return Task.FromResult(response);
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException("Streaming not used in this example.");

    public void Dispose()
    {
    }

    public object? GetService(Type serviceType, object? serviceKey)
    {
        ArgumentNullException.ThrowIfNull(serviceType);
        return serviceType == typeof(ChatClientMetadata) ? _metadata : null;
    }
}
