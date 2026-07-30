namespace NexusLabs.Foundry.Evaluation.Harness;

/// <summary>
/// Per-item evidence describing how completely an arm's run emitted the telemetry counters the hosted
/// protocol relies on. The telemetry-completeness evaluator scores whether the expected per-call and
/// aggregate counters were captured; it does not compare arms.
/// </summary>
public sealed record HarnessTelemetryEvidence
{
    /// <summary>Gets the number of chat-completion calls the run was expected to record.</summary>
    public required int ExpectedChatCompletionCount { get; init; }

    /// <summary>Gets the number of chat-completion diagnostics actually recorded.</summary>
    public required int ObservedChatCompletionCount { get; init; }

    /// <summary>Gets the number of tool calls the run was expected to record.</summary>
    public required int ExpectedToolCallCount { get; init; }

    /// <summary>Gets the number of tool-call diagnostics actually recorded.</summary>
    public required int ObservedToolCallCount { get; init; }

    /// <summary>Gets a value indicating whether aggregate token counters were captured.</summary>
    public required bool HasAggregateTokenUsage { get; init; }

    /// <summary>Gets a value indicating whether per-call durations were captured.</summary>
    public required bool HasCallDurations { get; init; }

    /// <summary>Gets a value indicating whether progress events were captured for the run.</summary>
    public required bool HasProgressEvents { get; init; }

    /// <summary>Gets the names of required telemetry fields that were missing, if any.</summary>
    public IReadOnlyList<string> MissingFields { get; init; } = [];
}
