using NexusLabs.Foundry.MicrosoftAgentFramework.Diagnostics;
using NexusLabs.Foundry.MicrosoftAgentFramework.Progress;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Context;

/// <summary>
/// Explicit, structured result of one <see cref="HarnessArtifactRehydration.Rehydrate"/> call.
/// Always carries the full <see cref="HarnessArtifactResolution"/> evidence and a matching
/// <see cref="Diagnostics"/> snapshot; only carries a non-<see langword="null"/>
/// <see cref="Segment"/> when <see cref="Status"/> is
/// <see cref="HarnessArtifactResolutionStatus.Resolved"/>.
/// </summary>
internal sealed record HarnessArtifactRehydrationResult
{
    private HarnessArtifactRehydrationResult(
        HarnessArtifactResolution resolution,
        HarnessArtifactRecoverableContextSegment? segment,
        HarnessArtifactDiagnostics diagnostics)
    {
        Resolution = resolution;
        Segment = segment;
        Diagnostics = diagnostics;
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

    /// <summary>
    /// The privacy-safe, structured evidence for this decision. The identical instance is also
    /// attached to the <see cref="HarnessArtifactRehydrationDecisionEvent"/> emitted for this
    /// decision.
    /// </summary>
    internal HarnessArtifactDiagnostics Diagnostics { get; }

    /// <exception cref="ArgumentNullException">
    /// <paramref name="resolution"/> or <paramref name="diagnostics"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="resolution"/>'s status is <see cref="HarnessArtifactResolutionStatus.Resolved"/>.
    /// </exception>
    internal static HarnessArtifactRehydrationResult NotResolved(
        HarnessArtifactResolution resolution,
        HarnessArtifactDiagnostics diagnostics)
    {
        ArgumentNullException.ThrowIfNull(resolution);
        ArgumentNullException.ThrowIfNull(diagnostics);

        if (resolution.Status == HarnessArtifactResolutionStatus.Resolved)
        {
            throw new ArgumentException(
                "A Resolved resolution must be paired with a recoverable context segment; use Resolved(...) instead.",
                nameof(resolution));
        }

        return new HarnessArtifactRehydrationResult(resolution, null, diagnostics);
    }

    /// <exception cref="ArgumentNullException">
    /// <paramref name="resolution"/>, <paramref name="segment"/>, or <paramref name="diagnostics"/>
    /// is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="resolution"/>'s status is not <see cref="HarnessArtifactResolutionStatus.Resolved"/>.
    /// </exception>
    internal static HarnessArtifactRehydrationResult Resolved(
        HarnessArtifactResolution resolution,
        HarnessArtifactRecoverableContextSegment segment,
        HarnessArtifactDiagnostics diagnostics)
    {
        ArgumentNullException.ThrowIfNull(resolution);
        ArgumentNullException.ThrowIfNull(segment);
        ArgumentNullException.ThrowIfNull(diagnostics);

        if (resolution.Status != HarnessArtifactResolutionStatus.Resolved)
        {
            throw new ArgumentException(
                "A recoverable context segment may only be paired with a Resolved resolution.",
                nameof(resolution));
        }

        return new HarnessArtifactRehydrationResult(resolution, segment, diagnostics);
    }
}
