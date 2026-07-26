using System.Text.Json;

using Microsoft.Extensions.AI;

namespace AotHarnessApp;

internal sealed class AotHarnessScriptedChatClient(
    string functionName) : IChatClient
{
    private int _callCount;

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
                                "aot-harness-call",
                                functionName,
                                new Dictionary<string, object?>
                                {
                                    ["value"] = AotHarnessScenario.ExpectedWorkspaceContent,
                                }),
                        ])));
        }

        var result = chatMessages
            .SelectMany(message => message.Contents)
            .OfType<FunctionResultContent>()
            .SingleOrDefault(content =>
                string.Equals(
                    content.CallId,
                    "aot-harness-call",
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
