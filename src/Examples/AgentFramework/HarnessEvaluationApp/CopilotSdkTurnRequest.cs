using Microsoft.Extensions.AI;

namespace HarnessEvaluationApp;

internal sealed record CopilotSdkTurnRequest(
    string ModelId,
    string TranscriptJson,
    IReadOnlyList<AIFunction> Tools,
    int MaximumOutputTokens);
