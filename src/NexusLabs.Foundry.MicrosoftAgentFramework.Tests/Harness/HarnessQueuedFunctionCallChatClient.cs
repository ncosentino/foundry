using Microsoft.Extensions.AI;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Tests.Harness;

/// <summary>
/// Scripted <see cref="IChatClient"/> test double that issues a queued sequence of
/// <see cref="FunctionCallContent"/> calls, one per model turn, then returns a plain text
/// response once the queue is exhausted. Used to exercise multi-step tool conformance (for
/// example Todo add-then-complete) through the real function-invocation loop, since the
/// upstream provider tool factories are internal and cannot be invoked directly.
/// </summary>
internal sealed class HarnessQueuedFunctionCallChatClient : IChatClient
{
    private readonly IReadOnlyList<(string Name, IReadOnlyDictionary<string, object?> Arguments)> _calls;
    private int _callCount;

    internal HarnessQueuedFunctionCallChatClient(
        params (string Name, IReadOnlyDictionary<string, object?> Arguments)[] calls)
    {
        _calls = calls;
    }

    internal int CallCount => _callCount;

    Task<ChatResponse> IChatClient.GetResponseAsync(
        IEnumerable<ChatMessage> chatMessages,
        ChatOptions? options,
        CancellationToken cancellationToken)
    {
        var index = Interlocked.Increment(ref _callCount) - 1;
        if (index < _calls.Count)
        {
            var (name, arguments) = _calls[index];
            return Task.FromResult(
                new ChatResponse(
                    new ChatMessage(
                        ChatRole.Assistant,
                        [
                            new FunctionCallContent(
                                $"queued-call-{index}",
                                name,
                                new Dictionary<string, object?>(arguments)),
                        ])));
        }

        return Task.FromResult(
            new ChatResponse(new ChatMessage(ChatRole.Assistant, "queued-calls-complete")));
    }

    IAsyncEnumerable<ChatResponseUpdate> IChatClient.GetStreamingResponseAsync(
        IEnumerable<ChatMessage> chatMessages,
        ChatOptions? options,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException("Streaming is not required by the composition tests.");

    object? IChatClient.GetService(Type serviceType, object? key) => null;

    void IDisposable.Dispose()
    {
    }
}
