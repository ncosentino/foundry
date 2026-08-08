using System.Runtime.CompilerServices;

using Microsoft.Extensions.AI;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Tests;

internal sealed class CheckpointScriptedChatClient(
    IReadOnlyList<object> outcomes,
    Func<CancellationToken, Task>? beforeResponse = null) : IChatClient
{
    private int _callCount;

    internal CheckpointScriptedChatClient(
        params object[] outcomes)
        : this(outcomes, beforeResponse: null)
    {
    }

    internal int CallCount => Volatile.Read(ref _callCount);

    async Task<ChatResponse> IChatClient.GetResponseAsync(
        IEnumerable<ChatMessage> chatMessages,
        ChatOptions? options,
        CancellationToken cancellationToken)
    {
        object outcome = await NextOutcomeAsync(cancellationToken);
        if (outcome is Exception exception)
        {
            throw exception;
        }

        return new ChatResponse(
            new ChatMessage(
                ChatRole.Assistant,
                (string)outcome));
    }

    async IAsyncEnumerable<ChatResponseUpdate> IChatClient.GetStreamingResponseAsync(
        IEnumerable<ChatMessage> chatMessages,
        ChatOptions? options,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        object outcome = await NextOutcomeAsync(cancellationToken);
        if (outcome is Exception exception)
        {
            throw exception;
        }

        yield return new ChatResponseUpdate
        {
            Role = ChatRole.Assistant,
            Contents = [new TextContent((string)outcome)],
        };
    }

    object? IChatClient.GetService(Type serviceType, object? key) => null;

    void IDisposable.Dispose()
    {
    }

    private async Task<object> NextOutcomeAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (beforeResponse is not null)
        {
            await beforeResponse(cancellationToken);
        }

        int call = Interlocked.Increment(ref _callCount);
        return outcomes[Math.Min(call - 1, outcomes.Count - 1)];
    }
}
