namespace NexusLabs.Foundry.Evaluation.Harness;

/// <summary>
/// The lifecycle phase a normalized progress record represents. A <see cref="Started"/> record is
/// expected to be paired with exactly one <see cref="Completed"/> or <see cref="Terminated"/> record
/// sharing the same correlation identity; an <see cref="Instant"/> record stands alone.
/// </summary>
public enum HarnessLifecyclePhase
{
    /// <summary>A lifecycle span began.</summary>
    Started,

    /// <summary>A lifecycle span completed successfully.</summary>
    Completed,

    /// <summary>A lifecycle span terminated without success.</summary>
    Terminated,

    /// <summary>A standalone, instantaneous record with no paired boundary.</summary>
    Instant,
}
