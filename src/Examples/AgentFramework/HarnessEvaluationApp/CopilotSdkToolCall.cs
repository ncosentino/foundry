namespace HarnessEvaluationApp;

internal sealed record CopilotSdkToolCall(
    string CallId,
    string Name,
    IReadOnlyDictionary<string, object?> Arguments);
