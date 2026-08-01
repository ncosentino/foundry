using System.Runtime.CompilerServices;

using Microsoft.Extensions.AI;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Tests.Harness;

/// <summary>
/// A streaming chat client whose first response streams a function call rather than plain text,
/// so streaming tool-invocation guards can be exercised end to end.
/// </summary>
internal sealed class HarnessStreamingToolCallChatClient(
    string functionName,
    Action afterFirstResponse) : IChatClient
{
    private int _callCount;

    internal int CallCount => Volatile.Read(ref _callCount);

    Task<ChatResponse> IChatClient.GetResponseAsync(
        IEnumerable<ChatMessage> chatMessages,
        ChatOptions? options,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException(
            "Non-streaming execution is not required by this test client.");

    async IAsyncEnumerable<ChatResponseUpdate> IChatClient.GetStreamingResponseAsync(
        IEnumerable<ChatMessage> chatMessages,
        ChatOptions? options,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (Interlocked.Increment(ref _callCount) == 1)
        {
            yield return new ChatResponseUpdate(
                ChatRole.Assistant,
                new AIContent[]
                {
                    new FunctionCallContent(
                        "g2-call",
                        functionName,
                        new Dictionary<string, object?>()),
                });
            await Task.Yield();
            afterFirstResponse();
            yield break;
        }

        await Task.Yield();
        yield return new ChatResponseUpdate(ChatRole.Assistant, "model-result");
    }

    object? IChatClient.GetService(Type serviceType, object? key) => null;

    void IDisposable.Dispose()
    {
    }
}
