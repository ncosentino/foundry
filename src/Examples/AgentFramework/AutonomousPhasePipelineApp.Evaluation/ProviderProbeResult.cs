namespace AutonomousPhasePipelineApp.Evaluation;

internal sealed record ProviderProbeResult(
    int SchemaVersion,
    bool Succeeded,
    string Stage,
    string RequestedModel,
    string? ObservedModel,
    long? InputTokens,
    long? OutputTokens,
    long? CachedInputTokens,
    int ToolCalls,
    string CommitSha,
    DateTimeOffset CompletedAtUtc,
    string? ErrorType,
    string? ErrorMessage);
