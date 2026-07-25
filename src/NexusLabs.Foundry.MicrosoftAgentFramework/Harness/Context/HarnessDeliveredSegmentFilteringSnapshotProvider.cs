namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Context;

/// <summary>
/// Wraps the <see cref="IHarnessContextSnapshotProvider"/> a host's
/// <see cref="HarnessContextSnapshotIntegration"/> delegate returns for one provider call, filtering out
/// of every captured <see cref="HarnessContextSnapshot"/> any
/// <see cref="HarnessContextEntryKind.RecoverableContextSegment"/> entry whose canonical artifact digest
/// has already been marked delivered (see <see cref="HarnessCompactionRunCoordinator.MarkDelivered"/>)
/// during the active outer agent run. This is the non-retransmission enforcement point: a stable
/// recovered body a host re-selects on every nested provider call must still reach the real provider at
/// most once per run, even though the host's own selection logic has no reason to know that.
/// </summary>
/// <remarks>
/// Every other entry — every baseline <see cref="HarnessContextEntryKind.ArtifactReference"/> entry, and
/// every ordinary conversational/tool-exchange/system/authoritative/approval/summary entry — passes
/// through completely unfiltered. The captured <see cref="HarnessContextSnapshot.Version"/> is always
/// forwarded unchanged: this wrapper only ever removes entries from what the inner provider reports for
/// a given version, it never invents its own version numbering, and the coordinator's delivered set is
/// never mutated by a capture — only <see cref="HarnessHybridCompactionChatClient"/> ever calls
/// <see cref="HarnessCompactionRunCoordinator.MarkDelivered"/>, and only once per successful assembly,
/// immediately before dispatch, so every capture within one assembly attempt observes a stable, unchanging
/// filtered view.
/// </remarks>
internal sealed class HarnessDeliveredSegmentFilteringSnapshotProvider(
    IHarnessContextSnapshotProvider inner,
    HarnessCompactionRunCoordinator coordinator) : IHarnessContextSnapshotProvider
{
    public HarnessContextSnapshot CaptureSnapshot()
    {
        var snapshot = inner.CaptureSnapshot();

        var filtered = new List<HarnessContextEntry>(snapshot.Entries.Count);
        var anyRemoved = false;
        foreach (var entry in snapshot.Entries)
        {
            if (entry.Kind == HarnessContextEntryKind.RecoverableContextSegment &&
                entry.ArtifactReferenceDigest is { } digest &&
                coordinator.IsDelivered(digest))
            {
                anyRemoved = true;
                continue;
            }

            filtered.Add(entry);
        }

        return anyRemoved
            ? HarnessContextSnapshot.Create(snapshot.Version, filtered)
            : snapshot;
    }
}
