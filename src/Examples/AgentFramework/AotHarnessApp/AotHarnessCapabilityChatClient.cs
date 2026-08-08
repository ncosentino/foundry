using System.Text.Json;

using Microsoft.Extensions.AI;

namespace AotHarnessApp;

internal sealed class AotHarnessCapabilityChatClient(
    string workspaceFunctionName,
    string workspaceProofValue) : IChatClient
{
    private const string BackgroundAgentName = "aot-background-agent";
    private const int BackgroundTaskId = 1;

    private int _callCount;

    Task<ChatResponse> IChatClient.GetResponseAsync(
        IEnumerable<ChatMessage> chatMessages,
        ChatOptions? options,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        int call = Interlocked.Increment(ref _callCount);

        return Task.FromResult(
            call switch
            {
                1 => CreateFunctionCall(
                    "aot-capability-workspace-call",
                    workspaceFunctionName,
                    new Dictionary<string, object?>
                    {
                        ["proof_value"] = workspaceProofValue,
                    },
                    options),
                2 => CreateFunctionCall(
                    "aot-capability-background-start",
                    "background_agents_start_task",
                    new Dictionary<string, object?>
                    {
                        ["agentName"] = BackgroundAgentName,
                        ["input"] = "Return the NativeAOT background proof.",
                        ["description"] = "NativeAOT background proof",
                    },
                    options),
                3 => CreateFunctionCall(
                    "aot-capability-background-wait",
                    "background_agents_wait_for_first_completion",
                    new Dictionary<string, object?>
                    {
                        ["taskIds"] = new object?[] { BackgroundTaskId },
                    },
                    options),
                4 => CreateFunctionCall(
                    "aot-capability-background-result",
                    "background_agents_get_task_results",
                    new Dictionary<string, object?>
                    {
                        ["taskId"] = BackgroundTaskId,
                    },
                    options),
                5 => CreateFunctionCall(
                    "aot-capability-background-continue",
                    "background_agents_continue_task",
                    new Dictionary<string, object?>
                    {
                        ["taskId"] = BackgroundTaskId,
                        ["text"] = "Return the NativeAOT background proof again.",
                    },
                    options),
                6 => CreateFunctionCall(
                    "aot-capability-background-wait-after-continue",
                    "background_agents_wait_for_first_completion",
                    new Dictionary<string, object?>
                    {
                        ["taskIds"] = new object?[] { BackgroundTaskId },
                    },
                    options),
                7 => CreateFunctionCall(
                    "aot-capability-background-result-after-continue",
                    "background_agents_get_task_results",
                    new Dictionary<string, object?>
                    {
                        ["taskId"] = BackgroundTaskId,
                    },
                    options),
                _ => CreateFinalResponse(chatMessages),
            });
    }

    IAsyncEnumerable<ChatResponseUpdate> IChatClient.GetStreamingResponseAsync(
        IEnumerable<ChatMessage> chatMessages,
        ChatOptions? options,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException(
            "Streaming is not required by the NativeAOT capability scenario.");

    object? IChatClient.GetService(Type serviceType, object? key) => null;

    void IDisposable.Dispose()
    {
    }

    private static ChatResponse CreateFunctionCall(
        string callId,
        string functionName,
        IDictionary<string, object?> arguments,
        ChatOptions? options)
    {
        bool toolAvailable = options?.Tools?.Any(tool =>
            string.Equals(tool.Name, functionName, StringComparison.Ordinal)) == true;
        if (!toolAvailable)
        {
            throw new InvalidOperationException(
                $"The capability tool '{functionName}' was unavailable.");
        }

        return new ChatResponse(
            new ChatMessage(
                ChatRole.Assistant,
                [
                    new FunctionCallContent(
                        callId,
                        functionName,
                        arguments),
                ]));
    }

    private static ChatResponse CreateFinalResponse(
        IEnumerable<ChatMessage> chatMessages)
    {
        var result = chatMessages
            .SelectMany(message => message.Contents)
            .OfType<FunctionResultContent>()
            .SingleOrDefault(content =>
                string.Equals(
                    content.CallId,
                    "aot-capability-background-result-after-continue",
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
        if (!string.Equals(resultText, "background-complete", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"The background capability result was '{resultText ?? "missing"}'.");
        }

        return new ChatResponse(
            new ChatMessage(
                ChatRole.Assistant,
                $"harness-result:{resultText}"));
    }
}
