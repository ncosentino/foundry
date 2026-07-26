using Microsoft.Extensions.AI;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Tests;

/// <summary>
/// A minimal <see cref="IChatClient"/> fake used to construct
/// <see cref="Bundle.FoundryHarnessAgentConfiguration"/> instances in tests without any live
/// network dependency. It never issues a real chat completion; tests that need scripted
/// responses use dedicated fakes instead.
/// </summary>
internal sealed class FakeHarnessChatClient : IChatClient
{
    Task<ChatResponse> IChatClient.GetResponseAsync(
        IEnumerable<ChatMessage> chatMessages,
        ChatOptions? options,
        CancellationToken cancellationToken) =>
        Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "fake-response")));

    IAsyncEnumerable<ChatResponseUpdate> IChatClient.GetStreamingResponseAsync(
        IEnumerable<ChatMessage> chatMessages,
        ChatOptions? options,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException("Streaming is not required by the Harness bundle tests.");

    object? IChatClient.GetService(Type serviceType, object? key) => null;

    void IDisposable.Dispose()
    {
    }
}
