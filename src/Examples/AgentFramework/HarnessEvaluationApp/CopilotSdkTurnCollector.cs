using System.Net;
using System.Text.Json;

using GitHub.Copilot;

using Microsoft.Extensions.AI;

namespace HarnessEvaluationApp;

internal sealed class CopilotSdkTurnCollector
{
    private readonly object _gate = new();
    private readonly string _fallbackModelId;
    private readonly TaskCompletionSource<CopilotSdkTurnResult> _completion =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly HashSet<string> _externalToolCallIds = new(StringComparer.Ordinal);
    private AssistantMessageData? _assistant;
    private long _inputTokens;
    private long _outputTokens;
    private ChatFinishReason? _finishReason;
    private bool _usageObserved;

    internal CopilotSdkTurnCollector(string fallbackModelId)
    {
        _fallbackModelId = fallbackModelId;
    }

    internal Task<CopilotSdkTurnResult> Completion => _completion.Task;

    internal void Observe(SessionEvent sessionEvent)
    {
        lock (_gate)
        {
            switch (sessionEvent)
            {
                case AssistantMessageEvent message:
                    _assistant = message.Data;
                    break;
                case AssistantUsageEvent usage:
                    _usageObserved = true;
                    _inputTokens += usage.Data.InputTokens ?? 0;
                    _outputTokens += usage.Data.OutputTokens ?? 0;
                    _finishReason = MapFinishReason(usage.Data.FinishReason);
                    break;
                case ExternalToolRequestedEvent tool:
                    _externalToolCallIds.Add(tool.Data.ToolCallId);
                    break;
                case SessionErrorEvent error:
                    _completion.TrySetException(CreateException(error.Data));
                    return;
            }

            TryComplete();
        }
    }

    private void TryComplete()
    {
        if (_assistant is null)
        {
            return;
        }

        if (!_usageObserved)
        {
            return;
        }

        var toolRequests = _assistant.ToolRequests ?? [];
        if (toolRequests.Length > 0 &&
            toolRequests.Any(tool => !_externalToolCallIds.Contains(tool.ToolCallId)))
        {
            return;
        }

        var calls = toolRequests
            .Select(tool => new CopilotSdkToolCall(
                tool.ToolCallId,
                tool.Name,
                DeserializeArguments(tool.Arguments)))
            .ToArray();
        _completion.TrySetResult(new CopilotSdkTurnResult(
            _assistant.ApiCallId ?? _assistant.MessageId,
            _assistant.Model ?? _fallbackModelId,
            _assistant.Content,
            calls,
            _inputTokens,
            _outputTokens,
            calls.Length > 0
                ? ChatFinishReason.ToolCalls
                : _finishReason ?? ChatFinishReason.Stop));
    }

    private static IReadOnlyDictionary<string, object?> DeserializeArguments(JsonElement? arguments)
    {
        if (arguments is not { ValueKind: JsonValueKind.Object } value)
        {
            return new Dictionary<string, object?>();
        }

        return JsonSerializer.Deserialize<Dictionary<string, object?>>(value.GetRawText())
            ?? new Dictionary<string, object?>();
    }

    private static Exception CreateException(SessionErrorData error)
    {
        var message = $"Copilot SDK session error ({error.ErrorType}): {error.Message}";
        return error.StatusCode is { } statusCode &&
            Enum.IsDefined(typeof(HttpStatusCode), statusCode)
            ? new HttpRequestException(
                message,
                inner: null,
                (HttpStatusCode)statusCode)
            : new InvalidOperationException(message);
    }

    private static ChatFinishReason? MapFinishReason(string? finishReason) =>
        finishReason switch
        {
            "stop" => ChatFinishReason.Stop,
            "length" => ChatFinishReason.Length,
            "tool_calls" => ChatFinishReason.ToolCalls,
            "content_filter" => ChatFinishReason.ContentFilter,
            _ => null,
        };
}
