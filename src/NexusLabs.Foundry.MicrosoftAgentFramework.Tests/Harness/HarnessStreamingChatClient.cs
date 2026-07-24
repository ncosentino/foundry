using System.Runtime.CompilerServices;

using Microsoft.Extensions.AI;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Tests.Harness;

internal sealed class HarnessStreamingChatClient(
    Action afterFirstUpdate) : IChatClient
{
    Task<ChatResponse> IChatClient.GetResponseAsync(
        IEnumerable<ChatMessage> chatMessages,
        ChatOptions? options,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException("Non-streaming execution is not required by this test client.");

    async IAsyncEnumerable<ChatResponseUpdate> IChatClient.GetStreamingResponseAsync(
        IEnumerable<ChatMessage> chatMessages,
        ChatOptions? options,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        yield return new ChatResponseUpdate(ChatRole.Assistant, "first");
        afterFirstUpdate();
        await Task.Yield();
        yield return new ChatResponseUpdate(ChatRole.Assistant, "second");
    }

    object? IChatClient.GetService(Type serviceType, object? key) => null;

    void IDisposable.Dispose()
    {
    }
}
