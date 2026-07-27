namespace NexusLabs.Foundry.Evaluation.Harness;

/// <summary>
/// A normalized, privacy-safe progress record used for event-ordering and lifecycle-pairing
/// evaluation. It captures only the lifecycle kind, phase, global sequence number, correlation
/// identity, and optional emitting agent — never message text or payloads.
/// </summary>
public sealed record HarnessLifecycleEventEvidence
{
    /// <summary>Gets the lifecycle kind this record belongs to.</summary>
    public required HarnessLifecycleEventKind Kind { get; init; }

    /// <summary>Gets the lifecycle phase this record represents.</summary>
    public required HarnessLifecyclePhase Phase { get; init; }

    /// <summary>Gets the globally ordered sequence number for event ordering.</summary>
    public required long SequenceNumber { get; init; }

    /// <summary>
    /// Gets the correlation identity that pairs a started record with its completed/terminated record
    /// (for example an assembly ID or a per-call correlation key).
    /// </summary>
    public required string CorrelationId { get; init; }

    /// <summary>Gets the emitting agent identity, or <see langword="null"/> for workflow-level records.</summary>
    public string? AgentId { get; init; }
}
