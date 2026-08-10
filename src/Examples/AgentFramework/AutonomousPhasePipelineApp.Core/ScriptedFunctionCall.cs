using System.Text.Json;

using Microsoft.Extensions.AI;

namespace AutonomousPhasePipelineApp.Core;

internal static class ScriptedFunctionCall
{
    internal static ChatResponse Create(
        string callId,
        string functionName,
        IDictionary<string, object?> arguments,
        ChatOptions? options)
    {
        bool available = options?.Tools?.Any(tool =>
            string.Equals(
                tool.Name,
                functionName,
                StringComparison.Ordinal)) == true;
        if (!available)
        {
            throw new InvalidOperationException(
                $"Tool '{functionName}' was unavailable.");
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

    internal static string? GetResultText(
        IEnumerable<ChatMessage> messages,
        string callId)
    {
        FunctionResultContent? result = messages
            .SelectMany(message => message.Contents)
            .OfType<FunctionResultContent>()
            .LastOrDefault(content =>
                string.Equals(
                    content.CallId,
                    callId,
                    StringComparison.Ordinal));

        return result?.Result switch
        {
            string text => text,
            JsonElement
            {
                ValueKind: JsonValueKind.String,
            } json => json.GetString(),
            null => null,
            _ => result.Result.ToString(),
        };
    }
}
