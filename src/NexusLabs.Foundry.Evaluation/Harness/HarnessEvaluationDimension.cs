namespace NexusLabs.Foundry.Evaluation.Harness;

/// <summary>
/// The deterministic decision dimensions a hosted <c>harness-001</c> case can carry reference
/// evidence for. Each member names a per-item deterministic dimension used by the pre-registered
/// analysis protocol; a manifest case declares a <see cref="HarnessDeterministicReference"/> per
/// dimension it participates in.
/// </summary>
public enum HarnessEvaluationDimension
{
    /// <summary>The deterministic task-completion predicate (the primary decision dimension).</summary>
    Completion,

    /// <summary>Conversation/decision continuity across the run.</summary>
    Continuity,

    /// <summary>Context-window safety and compaction validity.</summary>
    ContextSafety,

    /// <summary>Artifact production, reuse, and rehydration.</summary>
    ArtifactReuse,

    /// <summary>Required/forbidden tool trajectory and tool errors.</summary>
    ToolTrajectory,

    /// <summary>Cancellation and timeout behaviour.</summary>
    Cancellation,

    /// <summary>Termination appropriateness.</summary>
    Termination,

    /// <summary>Diagnostics-schema completeness and parity.</summary>
    DiagnosticsParity,

    /// <summary>Cumulative token usage.</summary>
    CumulativeTokens,

    /// <summary>Peak token usage.</summary>
    PeakTokens,

    /// <summary>Attributed artifact/context cost.</summary>
    CostAttribution,

    /// <summary>End-to-end latency.</summary>
    Latency,
}
