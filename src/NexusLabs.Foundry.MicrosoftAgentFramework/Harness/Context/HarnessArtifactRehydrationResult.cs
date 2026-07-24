namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Context;

/// <summary>
/// Explicit, structured result of one <see cref="HarnessArtifactRehydration.Rehydrate"/> call
/// (T052). Always carries the full <see cref="HarnessArtifactResolution"/> evidence; only carries a
/// non-<see langword="null"/> <see cref="Segment"/> when <see cref="Status"/> is
/// <see cref="HarnessArtifactResolutionStatus.Resolved"/>.
/// </summary>
internal sealed record HarnessArtifactRehydrationResult
{
    private HarnessArtifactRehydrationResult(
        HarnessArtifactResolution resolution,
        HarnessArtifactRecoverableContextSegment? segment)
    {
        Resolution = resolution;
        Segment = segment;
    }

    /// <summary>The full resolution evidence backing this rehydration outcome.</summary>
    internal HarnessArtifactResolution Resolution { get; }

    /// <summary>Convenience accessor mirroring <see cref="Resolution"/>'s status.</summary>
    internal HarnessArtifactResolutionStatus Status => Resolution.Status;

    /// <summary>
    /// The marked recoverable context segment produced for a <see cref="HarnessArtifactResolutionStatus.Resolved"/>
    /// outcome; <see langword="null"/> for every other status. No body is ever injected for a
    /// non-resolved outcome.
    /// </summary>
    internal HarnessArtifactRecoverableContextSegment? Segment { get; }

    /// <exception cref="ArgumentNullException"><paramref name="resolution"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="resolution"/>'s status is <see cref="HarnessArtifactResolutionStatus.Resolved"/>.
    /// </exception>
    internal static HarnessArtifactRehydrationResult NotResolved(HarnessArtifactResolution resolution)
    {
        ArgumentNullException.ThrowIfNull(resolution);

        if (resolution.Status == HarnessArtifactResolutionStatus.Resolved)
        {
            throw new ArgumentException(
                "A Resolved resolution must be paired with a recoverable context segment; use Resolved(...) instead.",
                nameof(resolution));
        }

        return new HarnessArtifactRehydrationResult(resolution, null);
    }

    /// <exception cref="ArgumentNullException">
    /// <paramref name="resolution"/> or <paramref name="segment"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="resolution"/>'s status is not <see cref="HarnessArtifactResolutionStatus.Resolved"/>.
    /// </exception>
    internal static HarnessArtifactRehydrationResult Resolved(
        HarnessArtifactResolution resolution,
        HarnessArtifactRecoverableContextSegment segment)
    {
        ArgumentNullException.ThrowIfNull(resolution);
        ArgumentNullException.ThrowIfNull(segment);

        if (resolution.Status != HarnessArtifactResolutionStatus.Resolved)
        {
            throw new ArgumentException(
                "A recoverable context segment may only be paired with a Resolved resolution.",
                nameof(resolution));
        }

        return new HarnessArtifactRehydrationResult(resolution, segment);
    }
}
