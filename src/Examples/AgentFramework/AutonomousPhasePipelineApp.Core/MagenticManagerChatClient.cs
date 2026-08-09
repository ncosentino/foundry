using Microsoft.Extensions.AI;

namespace AutonomousPhasePipelineApp.Core;

internal sealed class MagenticManagerChatClient(
    IReadOnlyList<Func<IReadOnlyList<ChatMessage>, string>> responses,
    MagenticPhaseProbe probe,
    ReferenceSynthesisBudget budget) :
    IChatClient
{
    private readonly List<IReadOnlyList<ChatMessage>> _recordedInputs = [];
    private int _callCount;

    internal int CallCount => Volatile.Read(ref _callCount);

    internal IReadOnlyList<IReadOnlyList<ChatMessage>> RecordedInputs =>
        _recordedInputs;

    Task<ChatResponse> IChatClient.GetResponseAsync(
        IEnumerable<ChatMessage> chatMessages,
        ChatOptions? options,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        budget.ConsumeProviderCall(MagenticPhaseFactory.ManagerName);
        ChatMessage[] messages = [.. chatMessages];
        _recordedInputs.Add(messages);
        int index = Interlocked.Increment(ref _callCount) - 1;
        if (index >= responses.Count)
        {
            throw new InvalidOperationException(
                "The scripted Magentic manager exhausted its responses.");
        }

        string response = responses[index](messages);
        if (MagenticScriptedResponses.TryParseLedger(
            response,
            out MagenticLedgerSnapshot? snapshot) &&
            snapshot is not null)
        {
            probe.RecordLedgerSnapshot(snapshot);
        }

        return Task.FromResult(
            new ChatResponse(
                new ChatMessage(
                    ChatRole.Assistant,
                    response)
                {
                    MessageId = Guid.NewGuid().ToString("N"),
                    CreatedAt = DateTimeOffset.UtcNow,
                }));
    }

    IAsyncEnumerable<ChatResponseUpdate> IChatClient.GetStreamingResponseAsync(
        IEnumerable<ChatMessage> chatMessages,
        ChatOptions? options,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException(
            "Streaming is not required by the offline Magentic manager.");

    object? IChatClient.GetService(Type serviceType, object? key) => null;

    void IDisposable.Dispose()
    {
    }
}
