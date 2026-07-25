namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Context;

/// <summary>
/// One immutable, versioned view of the current context entries, captured by an
/// <see cref="IHarnessContextSnapshotProvider"/>. <see cref="HarnessContextAssembler"/> compares two
/// snapshots' <see cref="Version"/> values — never their <see cref="Entries"/> content — to decide
/// whether new injected entries appeared while a reduction attempt was in flight, so it can discard a
/// stale proposal and restart deterministically from the newest snapshot instead.
/// </summary>
/// <remarks>
/// <see cref="Create"/> never stores the caller's supplied entries list, and never shares a
/// caller-supplied <see cref="HarnessContextEntry"/> instance: every entry is defensively copied via
/// <see cref="HarnessContextEntry.Copy"/> into a read-only collection this type alone constructs and
/// holds. Mutating the original list, or any entry within it, after <see cref="Create"/> returns can
/// never change what this snapshot's <see cref="Entries"/> reports.
/// </remarks>
internal sealed record HarnessContextSnapshot
{
    private HarnessContextSnapshot(long version, IReadOnlyList<HarnessContextEntry> entries)
    {
        Version = version;
        Entries = entries;
    }

    /// <summary>
    /// A monotonically non-decreasing version stamp. A provider must return a strictly greater value
    /// than any previous capture whenever its entries changed (for example a newly injected message),
    /// and the identical value whenever nothing changed since the previous capture.
    /// </summary>
    internal long Version { get; }

    /// <summary>
    /// The exact ordered entries as of this snapshot: a read-only collection over defensively-copied
    /// <see cref="HarnessContextEntry"/> instances (see <see cref="Create"/>'s remarks), never the
    /// caller's own list or entry instances.
    /// </summary>
    internal IReadOnlyList<HarnessContextEntry> Entries { get; }

    /// <exception cref="ArgumentNullException"><paramref name="entries"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="version"/> is negative.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="entries"/> contains two entries sharing the same
    /// <see cref="HarnessContextEntry.EntryId"/>.
    /// </exception>
    internal static HarnessContextSnapshot Create(long version, IReadOnlyList<HarnessContextEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        if (version < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(version), version, "The snapshot version must not be negative.");
        }

        // Defensive copy: never store the caller's list, and never share a HarnessContextEntry
        // instance (and therefore never its underlying mutable ChatMessage content list) with the
        // caller. The result is wrapped in a read-only collection over a list this type alone
        // constructed and holds, so a later out-of-band mutation of either the caller's original
        // list or its entries can never change what this snapshot reports.
        var seenEntryIds = new HashSet<string>(StringComparer.Ordinal);
        var copiedEntries = new List<HarnessContextEntry>(entries.Count);
        foreach (var entry in entries)
        {
            if (!seenEntryIds.Add(entry.EntryId))
            {
                throw new ArgumentException(
                    $"Duplicate entry id '{entry.EntryId}' in the supplied entries.", nameof(entries));
            }

            copiedEntries.Add(entry.Copy());
        }

        return new HarnessContextSnapshot(version, copiedEntries.AsReadOnly());
    }
}
