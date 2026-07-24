namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Context;

/// <summary>
/// An explicit, deterministic-caller-driven request to rehydrate one artifact reference (T052).
/// There is no implicit/ambient way to trigger rehydration — a
/// <see cref="HarnessArtifactRehydration"/> only ever acts on an instance of this type, and every
/// instance requires an explicit, required maximum rehydrated UTF-8 byte budget (no optional
/// parameter, no token estimator).
/// </summary>
internal sealed record HarnessArtifactRehydrationRequest
{
    private HarnessArtifactRehydrationRequest(
        HarnessArtifactReference reference,
        HarnessArtifactRehydrationRequestSource requestSource,
        int maximumRehydratedUtf8Bytes)
    {
        Reference = reference;
        RequestSource = requestSource;
        MaximumRehydratedUtf8Bytes = maximumRehydratedUtf8Bytes;
    }

    /// <summary>The reference to resolve and, if resolvable, rehydrate.</summary>
    internal HarnessArtifactReference Reference { get; }

    /// <summary>The deterministic origin of this request.</summary>
    internal HarnessArtifactRehydrationRequestSource RequestSource { get; }

    /// <summary>
    /// The required maximum UTF-8 byte size the rehydrated content may occupy. Exceeding this
    /// budget produces <see cref="HarnessArtifactResolutionStatus.OverBudget"/> rather than
    /// truncated or partial content.
    /// </summary>
    internal int MaximumRehydratedUtf8Bytes { get; }

    /// <exception cref="ArgumentNullException"><paramref name="reference"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="maximumRehydratedUtf8Bytes"/> is not greater than zero.
    /// </exception>
    internal static HarnessArtifactRehydrationRequest Create(
        HarnessArtifactReference reference,
        HarnessArtifactRehydrationRequestSource requestSource,
        int maximumRehydratedUtf8Bytes)
    {
        ArgumentNullException.ThrowIfNull(reference);

        if (maximumRehydratedUtf8Bytes <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumRehydratedUtf8Bytes),
                maximumRehydratedUtf8Bytes,
                "The maximum rehydrated byte budget must be greater than zero.");
        }

        return new HarnessArtifactRehydrationRequest(reference, requestSource, maximumRehydratedUtf8Bytes);
    }
}
