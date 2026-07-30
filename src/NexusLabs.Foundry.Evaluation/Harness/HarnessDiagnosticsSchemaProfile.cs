namespace NexusLabs.Foundry.Evaluation.Harness;

/// <summary>
/// A per-item snapshot of which diagnostics-schema fields and relationships an arm's run captured,
/// plus its execution mode. It is the per-item evidence the diagnostics-schema profile evaluator
/// scores; cross-arm parity comparison is performed downstream from the emitted profile, never inside
/// the per-item evaluator.
/// </summary>
public sealed record HarnessDiagnosticsSchemaProfile
{
    /// <summary>Gets the execution mode label that produced the diagnostics (for example <c>IterativeLoop</c>).</summary>
    public required string ExecutionMode { get; init; }

    /// <summary>Gets a value indicating whether aggregate token usage counters were captured.</summary>
    public required bool HasAggregateTokenUsage { get; init; }

    /// <summary>Gets a value indicating whether per-call chat-completion diagnostics were captured.</summary>
    public required bool HasChatCompletionDiagnostics { get; init; }

    /// <summary>Gets a value indicating whether per-call tool-invocation diagnostics were captured.</summary>
    public required bool HasToolCallDiagnostics { get; init; }

    /// <summary>Gets a value indicating whether run timing boundaries (start/complete) were captured.</summary>
    public required bool HasTimingBoundaries { get; init; }

    /// <summary>Gets a value indicating whether the captured input messages were retained.</summary>
    public required bool HasInputMessages { get; init; }

    /// <summary>Gets a value indicating whether the aggregated output response was retained.</summary>
    public required bool HasOutputResponse { get; init; }

    /// <summary>Gets a value indicating whether the arm captures hybrid context compaction diagnostics.</summary>
    public required bool SupportsContextDiagnostics { get; init; }

    /// <summary>Gets a value indicating whether the arm captures artifact offload/rehydration diagnostics.</summary>
    public required bool SupportsArtifactDiagnostics { get; init; }
}
