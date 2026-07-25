namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Context;

/// <summary>
/// Validated factory for the one legitimate way a <see cref="HarnessContextEntryKind.RecoverableContextSegment"/>
/// entry ever enters a snapshot: a host-authored <see cref="HarnessContextSnapshotIntegration"/> delegate
/// augmenting the already-adapted baseline entries with a selected, already-rehydrated body. This never
/// derives a recoverable segment from an incoming <see cref="Microsoft.Extensions.AI.ChatMessage"/> — the
/// baseline entries this type augments are themselves the product of
/// <see cref="HarnessMafMessageContextAdapter.Adapt"/> observing only the already-loaded, reference-bearing
/// request, so the transient body added here is never visible to anything upstream of the augmentation
/// call, including any outer per-service history-persistence decorator.
/// </summary>
/// <remarks>
/// Enforces, before ever handing the result to <see cref="HarnessContextSnapshot.Create"/>: (1) the
/// augmented entry's id is not already present among the supplied baseline entries — a
/// <see cref="HarnessContextSnapshotIntegration"/> delegate must never silently replace an existing
/// baseline entry; and (2) a durable <see cref="HarnessContextEntryKind.ArtifactReference"/> entry with the
/// exact same canonical digest as the augmented segment's <see cref="HarnessArtifactReference.ContentDigest"/>
/// already exists among the supplied baseline entries — the assembler's own eviction-before-reducer
/// rule (<see cref="HarnessContextAssembler"/>) only ever evicts a recoverable body backed by such a
/// matching durable reference, so augmenting one without a matching reference would silently defeat that
/// rule and leave the transient body permanently un-evictable.
/// </remarks>
internal static class HarnessContextSnapshotAugmentation
{
    /// <exception cref="ArgumentNullException">
    /// <paramref name="baselineEntries"/>, <paramref name="entryId"/>, or <paramref name="segment"/> is
    /// <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="entryId"/> is empty or whitespace-only; <paramref name="entryId"/> already
    /// identifies an entry in <paramref name="baselineEntries"/>; or no
    /// <see cref="HarnessContextEntryKind.ArtifactReference"/> entry in <paramref name="baselineEntries"/>
    /// carries the same canonical digest as <paramref name="segment"/>'s
    /// <see cref="HarnessArtifactReference.ContentDigest"/>.
    /// </exception>
    internal static IReadOnlyList<HarnessContextEntry> WithRecoverableSegment(
        IReadOnlyList<HarnessContextEntry> baselineEntries,
        string entryId,
        HarnessArtifactRecoverableContextSegment segment)
    {
        ArgumentNullException.ThrowIfNull(baselineEntries);
        ArgumentNullException.ThrowIfNull(entryId);
        ArgumentNullException.ThrowIfNull(segment);
        ArgumentException.ThrowIfNullOrWhiteSpace(entryId);

        var digest = segment.Reference.ContentDigest;
        var hasMatchingDurableReference = false;
        foreach (var baselineEntry in baselineEntries)
        {
            if (string.Equals(baselineEntry.EntryId, entryId, StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    $"Entry id '{entryId}' already identifies a baseline entry. A " +
                    $"{nameof(HarnessContextSnapshotIntegration)} delegate must never silently replace " +
                    "an existing baseline entry with an augmented recoverable segment.",
                    nameof(entryId));
            }

            if (baselineEntry.Kind == HarnessContextEntryKind.ArtifactReference
                && string.Equals(baselineEntry.ArtifactReferenceDigest, digest, StringComparison.Ordinal))
            {
                hasMatchingDurableReference = true;
            }
        }

        if (!hasMatchingDurableReference)
        {
            throw new ArgumentException(
                $"No {nameof(HarnessContextEntryKind.ArtifactReference)} entry among the baseline entries " +
                $"carries digest '{digest}'. Augmenting a recoverable segment requires a matching durable " +
                "reference already present in the baseline so the assembler's eviction-before-reducer rule " +
                "can ever evict the transient body back down to that reference.",
                nameof(segment));
        }

        var augmented = new List<HarnessContextEntry>(baselineEntries.Count + 1);
        augmented.AddRange(baselineEntries);
        augmented.Add(HarnessContextEntry.CreateRecoverableSegment(entryId, segment));

        return augmented;
    }
}
