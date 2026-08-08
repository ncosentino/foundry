using Microsoft.Extensions.AI;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Tests;

internal sealed class HarnessLoopChatClient : IChatClient
{
    private readonly List<IReadOnlyList<ChatMessage>> _requests = [];
    private readonly IReadOnlyList<ChatResponse> _responses;
    private int _callCount;

    internal HarnessLoopChatClient(params string[] responses)
        : this(
            responses
                .Select(CreateResponse)
                .ToArray())
    {
    }

    internal HarnessLoopChatClient(params ChatResponse[] responses)
    {
        _responses = responses;
    }

    internal int CallCount => Volatile.Read(ref _callCount);

    internal IReadOnlyList<IReadOnlyList<ChatMessage>> Requests
    {
        get
        {
            lock (_requests)
            {
                return _requests
                    .Select(request => (IReadOnlyList<ChatMessage>)request.ToList())
                    .ToList();
            }
        }
    }

    Task<ChatResponse> IChatClient.GetResponseAsync(
        IEnumerable<ChatMessage> chatMessages,
        ChatOptions? options,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var request = chatMessages.ToList();
        lock (_requests)
        {
            _requests.Add(request);
        }

        int call = Interlocked.Increment(ref _callCount);
        var response = _responses.Count == 0
            ? CreateResponse(string.Empty)
            : _responses[Math.Min(call - 1, _responses.Count - 1)];
        return Task.FromResult(response);
    }

    IAsyncEnumerable<ChatResponseUpdate> IChatClient.GetStreamingResponseAsync(
        IEnumerable<ChatMessage> chatMessages,
        ChatOptions? options,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException("Streaming is not required by loop evaluation tests.");

    object? IChatClient.GetService(Type serviceType, object? key) => null;

    void IDisposable.Dispose()
    {
    }

    private static ChatResponse CreateResponse(string text) =>
        new(
            new ChatMessage(ChatRole.Assistant, text))
        {
            ModelId = "harness-loop-test-model",
            Usage = new UsageDetails
            {
                InputTokenCount = 10,
                OutputTokenCount = 2,
                TotalTokenCount = 12,
            },
        };
}
