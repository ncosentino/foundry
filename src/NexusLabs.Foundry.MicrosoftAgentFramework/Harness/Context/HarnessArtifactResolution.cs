namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Context;

/// <summary>
/// Explicit, structured result of resolving one <see cref="HarnessArtifactReference"/> (T053).
/// Carries exactly the evidence needed to distinguish every outcome without ever exposing raw
/// artifact content except through the dedicated <see cref="Resolved"/> factory. No public
/// constructor exists — every instance is produced by one of the status-specific factory methods
/// below, so a non-<see cref="HarnessArtifactResolutionStatus.Resolved"/> instance can never carry
/// <see cref="Content"/>.
/// </summary>
internal sealed record HarnessArtifactResolution
{
    private HarnessArtifactResolution(
        HarnessArtifactResolutionStatus status,
        HarnessArtifactReference reference,
        string? content,
        int? observedContentByteSize,
        string? observedContentDigest,
        string evidence)
    {
        Status = status;
        Reference = reference;
        Content = content;
        ObservedContentByteSize = observedContentByteSize;
        ObservedContentDigest = observedContentDigest;
        Evidence = evidence;
    }

    /// <summary>The explicit outcome of resolution.</summary>
    internal HarnessArtifactResolutionStatus Status { get; }

    /// <summary>The reference that was resolved.</summary>
    internal HarnessArtifactReference Reference { get; }

    /// <summary>
    /// The exact resolved content. Only ever non-<see langword="null"/> when <see cref="Status"/> is
    /// <see cref="HarnessArtifactResolutionStatus.Resolved"/>.
    /// </summary>
    internal string? Content { get; }

    /// <summary>
    /// The UTF-8 byte size actually observed in the workspace at resolution time, when content was
    /// found (<see cref="HarnessArtifactResolutionStatus.Resolved"/>,
    /// <see cref="HarnessArtifactResolutionStatus.Stale"/>, or
    /// <see cref="HarnessArtifactResolutionStatus.OverBudget"/>); otherwise <see langword="null"/>.
    /// </summary>
    internal int? ObservedContentByteSize { get; }

    /// <summary>
    /// The digest actually recomputed from observed workspace content, when content was found;
    /// otherwise <see langword="null"/>.
    /// </summary>
    internal string? ObservedContentDigest { get; }

    /// <summary>
    /// Bounded, human-readable explanation of the outcome. Never contains raw artifact content.
    /// </summary>
    internal string Evidence { get; }

    /// <summary>Content was found, matched its recorded digest, and is within budget.</summary>
    internal static HarnessArtifactResolution Resolved(
        HarnessArtifactReference reference,
        string content,
        int observedContentByteSize,
        string observedContentDigest)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(content);

        return new HarnessArtifactResolution(
            HarnessArtifactResolutionStatus.Resolved,
            reference,
            content,
            observedContentByteSize,
            observedContentDigest,
            $"Resolved {observedContentByteSize} byte(s) for digest '{reference.ContentDigest}' at '{reference.WorkspacePath}'.");
    }

    /// <summary>Content was found but its recomputed digest no longer matches the recorded digest.</summary>
    internal static HarnessArtifactResolution Stale(
        HarnessArtifactReference reference,
        string observedContentDigest,
        int observedContentByteSize)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(observedContentDigest);

        return new HarnessArtifactResolution(
            HarnessArtifactResolutionStatus.Stale,
            reference,
            null,
            observedContentByteSize,
            observedContentDigest,
            $"Recorded digest '{reference.ContentDigest}' at '{reference.WorkspacePath}' no longer " +
            $"matches observed digest '{observedContentDigest}'. The content was mutated out-of-band " +
            "after the reference was created.");
    }

    /// <summary>No content exists at the reference's recorded workspace path.</summary>
    internal static HarnessArtifactResolution Missing(HarnessArtifactReference reference)
    {
        ArgumentNullException.ThrowIfNull(reference);

        return new HarnessArtifactResolution(
            HarnessArtifactResolutionStatus.Missing,
            reference,
            null,
            null,
            null,
            $"No content exists at workspace path '{reference.WorkspacePath}'.");
    }

    /// <summary>The reference's recorded owner does not match the current trusted execution binding.</summary>
    internal static HarnessArtifactResolution Unauthorized(HarnessArtifactReference reference)
    {
        ArgumentNullException.ThrowIfNull(reference);

        return new HarnessArtifactResolution(
            HarnessArtifactResolutionStatus.Unauthorized,
            reference,
            null,
            null,
            null,
            "The reference's recorded owner identity does not match the current trusted execution binding.");
    }

    /// <summary>
    /// Content was found and matched its recorded digest, but its observed size exceeds the
    /// caller-supplied maximum resolvable/rehydratable UTF-8 byte budget.
    /// </summary>
    internal static HarnessArtifactResolution OverBudget(
        HarnessArtifactReference reference,
        int observedContentByteSize,
        int maximumResolvedUtf8Bytes)
    {
        ArgumentNullException.ThrowIfNull(reference);

        return new HarnessArtifactResolution(
            HarnessArtifactResolutionStatus.OverBudget,
            reference,
            null,
            observedContentByteSize,
            reference.ContentDigest,
            $"Observed content size {observedContentByteSize} byte(s) exceeds the maximum " +
            $"resolvable budget of {maximumResolvedUtf8Bytes} byte(s).");
    }
}
