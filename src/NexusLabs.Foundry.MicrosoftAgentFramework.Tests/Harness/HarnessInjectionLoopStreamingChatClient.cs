using System.Runtime.CompilerServices;

using Microsoft.Extensions.AI;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Tests.Harness;

internal sealed class HarnessInjectionLoopStreamingChatClient(
    Action afterFirstResponse) : IChatClient
{
    private int _callCount;

    internal int CallCount => _callCount;

    Task<ChatResponse> IChatClient.GetResponseAsync(
        IEnumerable<ChatMessage> chatMessages,
        ChatOptions? options,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException(
            "Non-streaming execution is not required by this test client.");

    async IAsyncEnumerable<ChatResponseUpdate>
        IChatClient.GetStreamingResponseAsync(
            IEnumerable<ChatMessage> chatMessages,
            ChatOptions? options,
            [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var call = Interlocked.Increment(ref _callCount);
        yield return new ChatResponseUpdate(
            ChatRole.Assistant,
            $"response-{call}");

        if (call == 1)
        {
            afterFirstResponse();
        }

        await Task.Yield();
    }

    object? IChatClient.GetService(Type serviceType, object? key) => null;

    void IDisposable.Dispose()
    {
    }
}
