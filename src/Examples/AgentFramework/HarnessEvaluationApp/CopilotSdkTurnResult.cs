using Microsoft.Extensions.AI;

namespace HarnessEvaluationApp;

internal sealed record CopilotSdkTurnResult(
    string ResponseId,
    string ModelId,
    string Text,
    IReadOnlyList<CopilotSdkToolCall> ToolCalls,
    long InputTokens,
    long OutputTokens,
    ChatFinishReason FinishReason);
