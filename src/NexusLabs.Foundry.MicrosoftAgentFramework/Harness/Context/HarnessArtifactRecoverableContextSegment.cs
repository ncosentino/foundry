namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Context;

/// <summary>
/// Stable, distinguishable data carrier for a resolved rehydration payload (T052) — a "marked
/// recoverable context segment" per <c>data-model.md</c>'s "Rehydration Decision" field "Delivery
/// mode: marked recoverable context segment". Positioned conceptually after the eager-offload
/// boundary: unlike an ordinary (never-offloaded) tool result, this carrier is always
/// distinguishable from ordinary conversation content because only
/// <see cref="HarnessArtifactRehydration"/> ever constructs one, and only from a
/// <see cref="HarnessArtifactResolutionStatus.Resolved"/> outcome.
/// </summary>
/// <remarks>
/// <see cref="SkipEagerOffload"/> is always <see langword="true"/> and cannot be constructed
/// otherwise — this is the explicit, immutable invariant required so that a future eager-offload
/// transform (T051) never re-offloads a rehydrated body within the same active request, even if
/// that body is again over the ordinary inline-result byte threshold. G4 builds only this
/// primitive; it does not build the automatic trigger that decides when to rehydrate, nor any
/// compaction/eviction policy layered on top (G5's "hybrid context" responsibility).
/// </remarks>
internal sealed record HarnessArtifactRecoverableContextSegment
{
    private HarnessArtifactRecoverableContextSegment(
        HarnessArtifactReference reference,
        string body,
        DateTimeOffset rehydratedAtUtc)
    {
        Reference = reference;
        Body = body;
        RehydratedAtUtc = rehydratedAtUtc;
    }

    /// <summary>The identity of the artifact reference this segment recovered.</summary>
    internal HarnessArtifactReference Reference { get; }

    /// <summary>The exact resolved body content.</summary>
    internal string Body { get; }

    /// <summary>When this segment was produced.</summary>
    internal DateTimeOffset RehydratedAtUtc { get; }

    /// <summary>
    /// Always <see langword="true"/>. Marks this body as bypassing eager re-offload for the active
    /// request, per <c>data-model.md</c>'s Rehydration Decision validation: "Rehydrated content
    /// bypasses eager re-offload for the active request and is never returned as an ordinary
    /// oversized tool-result payload."
    /// </summary>
    internal bool SkipEagerOffload => true;

    /// <exception cref="ArgumentNullException">
    /// <paramref name="reference"/> or <paramref name="body"/> is <see langword="null"/>.
    /// </exception>
    internal static HarnessArtifactRecoverableContextSegment Create(
        HarnessArtifactReference reference,
        string body,
        DateTimeOffset rehydratedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(body);

        return new HarnessArtifactRecoverableContextSegment(reference, body, rehydratedAtUtc);
    }
}
