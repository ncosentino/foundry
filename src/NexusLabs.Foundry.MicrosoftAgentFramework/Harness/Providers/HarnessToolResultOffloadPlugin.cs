using NexusLabs.Foundry.MicrosoftAgentFramework.Tools;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Providers;

/// <summary>
/// Narrow, internal selected-provider slice carrying the eager tool-result offload policy inputs.
/// This is deliberately not a <see cref="Capabilities.HarnessCapability"/>/
/// <see cref="Capabilities.HarnessCapabilityProfile"/> activation — it carries only the
/// byte-threshold offload policy inputs; <see cref="HarnessProviderComposition"/> builds the
/// actual <see cref="HarnessToolResultOffloadPolicy"/> by combining this plugin with the
/// composition request's own existing <c>ExecutionBinding</c>/<c>SessionId</c> rather than
/// minting a separate binding or session identity here.
/// </summary>
internal sealed class HarnessToolResultOffloadPlugin
{
    private HarnessToolResultOffloadPlugin(
        int maximumInlineToolResultBytes,
        HarnessToolResultOffloadDescriptionStrategy descriptionStrategy,
        HarnessToolResultOffloadCheckpoint? checkpoint)
    {
        MaximumInlineToolResultBytes = maximumInlineToolResultBytes;
        DescriptionStrategy = descriptionStrategy;
        Checkpoint = checkpoint;
    }

    /// <summary>
    /// The maximum allowed UTF-8 byte length of a serialized tool result before it is offloaded
    /// instead of inlined. Always greater than zero.
    /// </summary>
    internal int MaximumInlineToolResultBytes { get; }

    /// <summary>The bounded strategy used to build a fresh artifact's description.</summary>
    internal HarnessToolResultOffloadDescriptionStrategy DescriptionStrategy { get; }

    /// <summary>
    /// Optional internal failure-injection/checkpoint seam. <see langword="null"/> in every
    /// production composition; supplied only by tests exercising the
    /// artifact-written/reference-not-committed recovery window.
    /// </summary>
    internal HarnessToolResultOffloadCheckpoint? Checkpoint { get; }

    /// <summary>
    /// Creates a plugin using the single bounded default description strategy
    /// (<see cref="HarnessToolResultOffloadDescriptions.Default"/>) and no checkpoint seam — the
    /// shape every production composition uses.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="maximumInlineToolResultBytes"/> is not greater than zero.
    /// </exception>
    internal static HarnessToolResultOffloadPlugin Create(int maximumInlineToolResultBytes) =>
        Create(
            maximumInlineToolResultBytes,
            HarnessToolResultOffloadDescriptions.Default,
            checkpoint: null);

    /// <summary>
    /// Creates a plugin with an explicit description strategy and/or checkpoint seam — used by
    /// tests that inject a deterministic checkpoint to force the
    /// artifact-written/reference-not-committed recovery window.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="maximumInlineToolResultBytes"/> is not greater than zero.
    /// </exception>
    /// <exception cref="ArgumentNullException"><paramref name="descriptionStrategy"/> is <see langword="null"/>.</exception>
    internal static HarnessToolResultOffloadPlugin Create(
        int maximumInlineToolResultBytes,
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

        ArgumentNullException.ThrowIfNull(descriptionStrategy);

        return new HarnessToolResultOffloadPlugin(
            maximumInlineToolResultBytes,
            descriptionStrategy,
            checkpoint);
    }
}
