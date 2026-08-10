using System.Runtime.CompilerServices;

using Microsoft.Extensions.AI;

namespace CheckpointWorkflowApp;

internal sealed class CheckpointScriptedChatClient(
    IReadOnlyList<object> outcomes) : IChatClient
{
    private int _callCount;

    internal int CallCount => Volatile.Read(ref _callCount);

    async Task<ChatResponse> IChatClient.GetResponseAsync(
        IEnumerable<ChatMessage> chatMessages,
        ChatOptions? options,
        CancellationToken cancellationToken)
    {
        object outcome = GetNextOutcome(cancellationToken);
        if (outcome is Exception exception)
        {
            throw exception;
        }

        await Task.CompletedTask;
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
        object outcome = GetNextOutcome(cancellationToken);
        if (outcome is Exception exception)
        {
            throw exception;
        }

        await Task.CompletedTask;
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

    private object GetNextOutcome(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        int call = Interlocked.Increment(ref _callCount);
        return outcomes[Math.Min(call - 1, outcomes.Count - 1)];
    }
}
