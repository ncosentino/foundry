namespace NexusLabs.Foundry.MicrosoftAgentFramework.Tools;

/// <summary>
/// Required, explicit policy driving one <see cref="HarnessToolResultOffloadTransform"/> decision.
/// Every field is validated at construction — there is no permissive default threshold, session ID,
/// or description strategy. The offload decision is driven only by an explicit byte threshold,
/// never by token estimation.
/// </summary>
internal sealed record HarnessToolResultOffloadPolicy
{
    private HarnessToolResultOffloadPolicy(
        int maximumInlineToolResultBytes,
        string offloadSessionId,
        HarnessToolResultOffloadDescriptionStrategy descriptionStrategy,
        HarnessToolResultOffloadCheckpoint? checkpoint)
    {
        MaximumInlineToolResultBytes = maximumInlineToolResultBytes;
        OffloadSessionId = offloadSessionId;
        DescriptionStrategy = descriptionStrategy;
        Checkpoint = checkpoint;
    }

    /// <summary>
    /// The maximum allowed UTF-8 byte length of <see cref="ToolResultSerializer.Serialize"/>'s
    /// output before a result is offloaded instead of inlined. Exactly-at-threshold inlines; only
    /// strictly-over-threshold offloads. Always greater than zero.
    /// </summary>
    internal int MaximumInlineToolResultBytes { get; }

    /// <summary>
    /// The trusted session identity the transform revalidates the supplied
    /// <see cref="Harness.HarnessExecutionBinding"/> against before touching the workspace. Never
    /// null/empty/whitespace.
    /// </summary>
    internal string OffloadSessionId { get; }

    /// <summary>The bounded strategy used to build a fresh artifact's description.</summary>
    internal HarnessToolResultOffloadDescriptionStrategy DescriptionStrategy { get; }

    /// <summary>
    /// Optional internal failure-injection/checkpoint seam. <see langword="null"/> in every
    /// production call site; supplied only by tests exercising the
    /// artifact-written/reference-not-committed recovery window.
    /// </summary>
    internal HarnessToolResultOffloadCheckpoint? Checkpoint { get; }

    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="maximumInlineToolResultBytes"/> is not greater than zero.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="offloadSessionId"/> is null, empty, or whitespace-only.
    /// </exception>
    /// <exception cref="ArgumentNullException"><paramref name="descriptionStrategy"/> is <see langword="null"/>.</exception>
    internal static HarnessToolResultOffloadPolicy Create(
        int maximumInlineToolResultBytes,
        string offloadSessionId,
        HarnessToolResultOffloadDescriptionStrategy descriptionStrategy,
        HarnessToolResultOffloadCheckpoint? checkpoint)
    {
        if (maximumInlineToolResultBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumInlineToolResultBytes),
                maximumInlineToolResultBytes,
                "The maximum inline tool-result byte threshold must be greater than zero.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(offloadSessionId);
        ArgumentNullException.ThrowIfNull(descriptionStrategy);

        return new HarnessToolResultOffloadPolicy(
            maximumInlineToolResultBytes,
            offloadSessionId,
            descriptionStrategy,
            checkpoint);
    }
}
