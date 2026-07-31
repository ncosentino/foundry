using System.Text.Json;

using Microsoft.Extensions.AI;

namespace AotHarnessApp;

internal sealed class AotHarnessScriptedChatClient(
    string functionName,
    string toolArgumentValue) : IChatClient
{
    private readonly string _callId = $"aot-harness-call-{Guid.NewGuid():N}";
    private int _callCount;

    internal AotHarnessScriptedChatClient(string functionName)
        : this(functionName, AotHarnessScenario.ExpectedWorkspaceContent)
    {
    }

    Task<ChatResponse> IChatClient.GetResponseAsync(
        IEnumerable<ChatMessage> chatMessages,
        ChatOptions? options,
        CancellationToken cancellationToken)
    {
        int callCount = Interlocked.Increment(ref _callCount);
        if (callCount == 1)
        {
            bool toolAvailable = options?.Tools?.Any(tool =>
                string.Equals(tool.Name, functionName, StringComparison.Ordinal)) == true;
            if (!toolAvailable)
            {
                throw new InvalidOperationException(
                    "The generated tool was absent from the first provider call.");
            }

            return Task.FromResult(
                new ChatResponse(
                    new ChatMessage(
                        ChatRole.Assistant,
                        [
                            new FunctionCallContent(
                                _callId,
                                functionName,
                                new Dictionary<string, object?>
                                {
                                    ["value"] = toolArgumentValue,
                                }),
                        ])));
        }

        var result = chatMessages
            .SelectMany(message => message.Contents)
            .OfType<FunctionResultContent>()
            .SingleOrDefault(content =>
                string.Equals(
                    content.CallId,
                    _callId,
                    StringComparison.Ordinal));
        string? resultText = result?.Result switch
        {
            string text => text,
            JsonElement
            {
                ValueKind: JsonValueKind.String,
            } json => json.GetString(),
            null => null,
            _ => result.Result.ToString(),
        };
        return Task.FromResult(
            new ChatResponse(
                new ChatMessage(
                    ChatRole.Assistant,
                    $"harness-result:{resultText ?? "missing"}")));
    }

    IAsyncEnumerable<ChatResponseUpdate> IChatClient.GetStreamingResponseAsync(
        IEnumerable<ChatMessage> chatMessages,
        ChatOptions? options,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException("Streaming is not part of the minimum AOT Harness profile.");

    object? IChatClient.GetService(Type serviceType, object? key) => null;

    void IDisposable.Dispose()
    {
    }
}
