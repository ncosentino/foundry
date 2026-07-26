using Microsoft.Extensions.AI;

using NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Context;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Tests.Harness;

/// <summary>
/// Test-only leaf <see cref="IChatClient"/> that records the exact materialized message list observed
/// on every call — unlike <see cref="HarnessScriptedChatClient"/>, which only records
/// <see cref="ChatOptions"/> — so a seam test can assert directly on what a
/// <see cref="HarnessHybridCompactionChatClient"/> installed above this leaf actually
/// forwards to the real provider client on each of a two-round FICC tool flow's calls.
/// </summary>
internal sealed class HarnessCompactionObservingChatClient : IChatClient
{
    private readonly string _functionName;
    private readonly Action? _afterCall;
    private readonly List<IReadOnlyList<ChatMessage>> _observedCalls = [];

    internal HarnessCompactionObservingChatClient(string functionName, Action? afterCall = null)
    {
        _functionName = functionName;
        _afterCall = afterCall;
    }

    /// <summary>The exact materialized message list observed on every call, in call order.</summary>
    internal IReadOnlyList<IReadOnlyList<ChatMessage>> ObservedCalls => _observedCalls;

    internal int CallCount => _observedCalls.Count;

    Task<ChatResponse> IChatClient.GetResponseAsync(
        IEnumerable<ChatMessage> chatMessages, ChatOptions? options, CancellationToken cancellationToken)
    {
        var materialized = chatMessages.ToList();
        _observedCalls.Add(materialized);
        _afterCall?.Invoke();

        if (_observedCalls.Count == 1)
        {
            var response = new ChatResponse(
                new ChatMessage(
                    ChatRole.Assistant,
                    [new FunctionCallContent("compaction-seam-call", _functionName, new Dictionary<string, object?>())]));
            return Task.FromResult(response);
        }

        var result = materialized
            .SelectMany(message => message.Contents)
            .OfType<FunctionResultContent>()
            .LastOrDefault();
        var resultText = result?.Result?.ToString();
        return Task.FromResult(
            new ChatResponse(
                new ChatMessage(
                    ChatRole.Assistant,
                    string.IsNullOrEmpty(resultText) ? "missing-result" : resultText)));
    }

    IAsyncEnumerable<ChatResponseUpdate> IChatClient.GetStreamingResponseAsync(
        IEnumerable<ChatMessage> chatMessages, ChatOptions? options, CancellationToken cancellationToken) =>
        throw new NotSupportedException("Streaming is not required by the compaction seam tests.");

    object? IChatClient.GetService(Type serviceType, object? key) => null;

    void IDisposable.Dispose()
    {
    }
}
