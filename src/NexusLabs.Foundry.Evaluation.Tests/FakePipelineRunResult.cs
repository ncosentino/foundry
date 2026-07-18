using Microsoft.Extensions.AI;

using NexusLabs.Foundry.MicrosoftAgentFramework.Diagnostics;

namespace NexusLabs.Foundry.Evaluation.Tests;

internal sealed class FakePipelineRunResult : IPipelineRunResult
{
    public required IReadOnlyList<IAgentStageResult> Stages { get; init; }

    public int PlannedStageCount { get; init; }

    public required IReadOnlyDictionary<string, ChatResponse?> FinalResponses { get; init; }

    public required TimeSpan TotalDuration { get; init; }

    public required TokenUsage? AggregateTokenUsage { get; init; }

    public required bool Succeeded { get; init; }

    public required string? ErrorMessage { get; init; }
}
