using NexusLabs.Foundry.MicrosoftAgentFramework.Diagnostics;

namespace NexusLabs.Foundry.Evaluation.Harness;

/// <summary>
/// Per-item, privacy-safe evidence for one hybrid context compaction/assembly attempt, mirroring the
/// public categorical shape of <see cref="HarnessContextDiagnostics"/> using only categories, sizes,
/// and counts. It is constructable without the Harness runtime so evaluators and tests can score
/// context-safety and compaction-validity dimensions from a plain evidence record.
/// </summary>
public sealed record HarnessContextCompactionEvidence
{
    /// <summary>Gets the categorical compaction/assembly outcome.</summary>
    public required HarnessContextCompactionOutcome Outcome { get; init; }

    /// <summary>Gets the unit every size, threshold, and limit is expressed in.</summary>
    public required HarnessContextMeasurementUnit MeasurementUnit { get; init; }

    /// <summary>Gets the original assembled size.</summary>
    public required int OriginalSize { get; init; }

    /// <summary>Gets the final assembled size (or terminating fallback candidate size on termination).</summary>
    public required int FinalSize { get; init; }

    /// <summary>Gets the trigger threshold (hard limit minus trigger margin) in force.</summary>
    public required int TriggerThreshold { get; init; }

    /// <summary>Gets the hard limit in force.</summary>
    public required int HardLimit { get; init; }

    /// <summary>Gets the number of bounded recompaction attempts consumed.</summary>
    public required int AttemptCount { get; init; }

    /// <summary>Gets the reduction path, in execution order.</summary>
    public required IReadOnlyList<HarnessContextAssemblyStageCategory> Stages { get; init; }

    /// <summary>
    /// Gets the sum of the per-category size contributions to <see cref="FinalSize"/>. On a success
    /// outcome this must equal <see cref="FinalSize"/>; on a termination there are no contributions.
    /// </summary>
    public required int CategoryContributionSizeSum { get; init; }

    /// <summary>Gets the number of distinct categories contributing to the final entries.</summary>
    public required int CategoryContributionCount { get; init; }

    /// <summary>
    /// Gets the final sequence-validity flag: <see langword="true"/> on a verified success,
    /// <see langword="null"/> on a termination.
    /// </summary>
    public bool? FinalSequenceValid { get; init; }
}
