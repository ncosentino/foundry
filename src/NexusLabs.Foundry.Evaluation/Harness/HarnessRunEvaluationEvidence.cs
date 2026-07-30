namespace NexusLabs.Foundry.Evaluation.Harness;

/// <summary>
/// A compositional bundle of the per-item deterministic evidence slices produced for one Harness
/// agent run. Every slice is optional: a <see langword="null"/> slice means the dimension is not
/// applicable to the run and its evaluator returns an empty result, whereas a present-but-invalid
/// slice is scored with failed/false metrics rather than being silently dropped.
/// </summary>
public sealed record HarnessRunEvaluationEvidence
{
    /// <summary>Gets the diagnostics-schema profile slice, or <see langword="null"/> when not captured.</summary>
    public HarnessDiagnosticsSchemaProfile? DiagnosticsSchema { get; init; }

    /// <summary>Gets the context compaction attempts, or <see langword="null"/> when compaction is not applicable.</summary>
    public IReadOnlyList<HarnessContextCompactionEvidence>? ContextCompactions { get; init; }

    /// <summary>Gets the artifact decisions, or <see langword="null"/> when artifact handling is not applicable.</summary>
    public IReadOnlyList<HarnessArtifactDecisionEvidence>? ArtifactDecisions { get; init; }

    /// <summary>Gets the telemetry-completeness slice, or <see langword="null"/> when not applicable.</summary>
    public HarnessTelemetryEvidence? Telemetry { get; init; }

    /// <summary>Gets the normalized lifecycle events, or <see langword="null"/> when progress was not captured.</summary>
    public IReadOnlyList<HarnessLifecycleEventEvidence>? LifecycleEvents { get; init; }

    /// <summary>Gets the identity-attribution slice, or <see langword="null"/> when not applicable.</summary>
    public HarnessIdentityAttributionEvidence? IdentityAttribution { get; init; }

    /// <summary>Gets the cancellation/timeout slice, or <see langword="null"/> when not applicable.</summary>
    public HarnessCancellationEvidence? Cancellation { get; init; }

    /// <summary>Gets the session-continuity slice, or <see langword="null"/> when not applicable.</summary>
    public HarnessSessionContinuityEvidence? SessionContinuity { get; init; }

    /// <summary>Gets the cost-attribution slice, or <see langword="null"/> when not applicable.</summary>
    public HarnessCostAttributionEvidence? CostAttribution { get; init; }

    /// <summary>Gets the required/forbidden tool-trajectory expectation, or <see langword="null"/> when not applicable.</summary>
    public HarnessToolTrajectoryExpectation? ToolTrajectory { get; init; }
}
