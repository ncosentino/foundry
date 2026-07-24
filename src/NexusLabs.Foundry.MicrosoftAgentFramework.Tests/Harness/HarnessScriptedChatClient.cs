using System.Text.Json;

using Microsoft.Extensions.AI;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Tests.Harness;

internal sealed class HarnessScriptedChatClient : IChatClient
{
    private readonly string _functionName;
    private readonly Action _afterFirstResponse;
    private readonly bool _requestFunctionCall;
    private int _callCount;

    internal HarnessScriptedChatClient(string functionName)
        : this(functionName, static () => { }, requestFunctionCall: true)
    {
    }

    internal HarnessScriptedChatClient(
        string functionName,
        Action afterFirstResponse)
        : this(functionName, afterFirstResponse, requestFunctionCall: true)
    {
    }

    internal HarnessScriptedChatClient(
        string functionName,
        Action afterFirstResponse,
        bool requestFunctionCall)
    {
        _functionName = functionName;
        _afterFirstResponse = afterFirstResponse;
        _requestFunctionCall = requestFunctionCall;
    }

    internal int CallCount => _callCount;

    /// <summary>
    /// The <see cref="ChatOptions"/> instance supplied to the most recent
    /// <see cref="IChatClient.GetResponseAsync"/> call, captured verbatim for tests that
    /// assert on the exact composed <c>ChatOptions.Tools</c> shape (for example, that
    /// exactly one hosted web search marker was appended).
    /// </summary>
    internal ChatOptions? LastOptions { get; private set; }

    Task<ChatResponse> IChatClient.GetResponseAsync(
        IEnumerable<ChatMessage> chatMessages,
        ChatOptions? options,
        CancellationToken cancellationToken)
    {
        LastOptions = options;
        if (Interlocked.Increment(ref _callCount) == 1)
        {
            var response = _requestFunctionCall
                ? new ChatResponse(
                    new ChatMessage(
                        ChatRole.Assistant,
                        [
                            new FunctionCallContent(
                                "g2-call",
                                _functionName,
                                new Dictionary<string, object?>()),
                        ]))
                : new ChatResponse(
                    new ChatMessage(ChatRole.Assistant, "model-result"));
            _afterFirstResponse();
            return Task.FromResult(response);
        }

        var result = chatMessages
            .SelectMany(message => message.Contents)
            .OfType<FunctionResultContent>()
            .LastOrDefault();
        var resultText = result?.Result is JsonElement
            {
                ValueKind: JsonValueKind.String,
            } json
                ? json.GetString()
                : ToolResultSerializer.Serialize(result?.Result);
        return Task.FromResult(
            new ChatResponse(
                new ChatMessage(
                    ChatRole.Assistant,
                    string.IsNullOrEmpty(resultText) ? "missing-result" : resultText)));
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
