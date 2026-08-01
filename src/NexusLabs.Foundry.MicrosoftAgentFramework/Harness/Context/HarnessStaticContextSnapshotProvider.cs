namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Context;

/// <summary>
/// The default <see cref="IHarnessContextSnapshotProvider"/>: it reports exactly the baseline entries
/// adapted for one provider call, at a constant version, for callers that do not integrate live session
/// or message-injection state.
/// </summary>
/// <remarks>
/// A constant version is correct rather than a shortcut here. The version exists so
/// <see cref="HarnessContextAssembler"/> can detect entries appearing while a reduction attempt is in
/// flight; this provider is constructed fresh from the exact message set presented for one call and has
/// no source that could introduce new entries mid-assembly, so no capture during that assembly can ever
/// observe different entries. A host that does inject mid-assembly supplies its own provider.
/// </remarks>
internal sealed class HarnessStaticContextSnapshotProvider : IHarnessContextSnapshotProvider
{
    private readonly HarnessContextSnapshot _snapshot;

    /// <exception cref="ArgumentNullException"><paramref name="entries"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="entries"/> contains two entries sharing the same
    /// <see cref="HarnessContextEntry.EntryId"/>.
    /// </exception>
    internal HarnessStaticContextSnapshotProvider(IReadOnlyList<HarnessContextEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        _snapshot = HarnessContextSnapshot.Create(0, entries);
    }

    public HarnessContextSnapshot CaptureSnapshot() => _snapshot;
}
