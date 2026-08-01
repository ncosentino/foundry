using System.Runtime.CompilerServices;

using Microsoft.Extensions.AI;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Tests.Harness;

/// <summary>
/// A leaf chat client that answers the first request with a function call and records every
/// <see cref="FunctionResultContent"/> it is subsequently handed, so a test can observe what a
/// function-invocation loop feeds back toward the provider.
/// </summary>
internal sealed class HarnessFunctionResultRecordingChatClient(string functionName) : IChatClient
{
    private readonly List<FunctionResultContent> _observedResults = [];
    private int _callCount;

    internal int CallCount => Volatile.Read(ref _callCount);

    internal IReadOnlyList<FunctionResultContent> ObservedResults => _observedResults;

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        Record(messages);
        return Task.FromResult(
            Interlocked.Increment(ref _callCount) == 1
                ? new ChatResponse(
                    new ChatMessage(
                        ChatRole.Assistant,
                        [
                            new FunctionCallContent(
                                "g2-call",
                                functionName,
                                new Dictionary<string, object?>()),
                        ]))
                : new ChatResponse(new ChatMessage(ChatRole.Assistant, "model-result")));
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Record(messages);
        await Task.Yield();
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
            yield break;
        }

        yield return new ChatResponseUpdate(ChatRole.Assistant, "model-result");
    }

    public object? GetService(Type serviceType, object? key) => null;

    public void Dispose()
    {
    }

    private void Record(IEnumerable<ChatMessage> messages)
    {
        lock (_observedResults)
        {
            _observedResults.AddRange(messages
                .SelectMany(message => message.Contents)
                .OfType<FunctionResultContent>());
        }
    }
}
