using Microsoft.Extensions.AI;

using NexusLabs.Needlr;

namespace LangfuseEvaluationApp;

[DoNotAutoRegister]
internal sealed class MockChatClient : IChatClient
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
            [new ChatMessage(ChatRole.Assistant, "Mock summary of the cached prompt content.")])
        {
            ModelId = "mock-model",
            Usage = new UsageDetails
            {
                InputTokenCount = 4000,
                OutputTokenCount = 180,
                TotalTokenCount = 4180,
                CachedInputTokenCount = 2500,
                ReasoningTokenCount = 90,
            },
        };
        return Task.FromResult(response);
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException("Streaming is not used in this example.");

    public void Dispose()
    {
    }

    public object? GetService(Type serviceType, object? serviceKey)
    {
        ArgumentNullException.ThrowIfNull(serviceType);
        return serviceType == typeof(ChatClientMetadata) ? _metadata : null;
    }
}
