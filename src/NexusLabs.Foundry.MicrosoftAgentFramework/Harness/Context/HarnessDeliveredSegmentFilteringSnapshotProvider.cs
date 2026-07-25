namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Context;

/// <summary>
/// Wraps the <see cref="IHarnessContextSnapshotProvider"/> a host's
/// <see cref="HarnessContextSnapshotIntegration"/> delegate returns for one provider call, filtering out
/// of every captured <see cref="HarnessContextSnapshot"/> any
/// <see cref="HarnessContextEntryKind.RecoverableContextSegment"/> entry whose canonical artifact digest
/// this call's lease cannot reserve — either because it was already promoted to Delivered (see
/// <see cref="HarnessCompactionRunCoordinator.Complete"/>) during the active outer agent run, or because a
/// different, concurrently-running provider call within the very same run already reserved it first (see
/// <see cref="HarnessCompactionRunCoordinator.TryReserve"/>). This is the non-retransmission enforcement
/// point: a stable recovered body a host re-selects on every nested provider call must still reach the
/// real provider at most once per run, even though the host's own selection logic has no reason to know
/// that, and two provider calls racing concurrently within the same run must never both forward it.
/// </summary>
/// <remarks>
/// Every other entry — every baseline <see cref="HarnessContextEntryKind.ArtifactReference"/> entry, and
/// every ordinary conversational/tool-exchange/system/authoritative/approval/summary entry — passes
/// through completely unfiltered. The captured <see cref="HarnessContextSnapshot.Version"/> is always
/// forwarded unchanged: this wrapper only ever removes entries from what the inner provider reports for
/// a given version, it never invents its own version numbering. Reserving a digest here does not, by
/// itself, deliver it: <see cref="HarnessHybridCompactionChatClient"/> only closes this call's own
/// <paramref name="leaseId"/>-owned lease via
/// <see cref="HarnessCompactionRunCoordinator.Complete"/>, after the real provider call this capture
/// feeds has itself completed successfully — promoting forwarded digests to Delivered and atomically
/// releasing any remaining (pressure-evicted or non-forwarded) reservations owned by this lease — and
/// releases them all, via <see cref="HarnessCompactionRunCoordinator.Release"/>, if assembly or dispatch
/// fails or is canceled first, so a retry can still reserve and deliver the exact same digest. Every
/// repeated capture within one assembly attempt shares the same <paramref name="leaseId"/>, so it keeps
/// observing its own reservations as held rather than filtering them back out a second time.
/// </remarks>
internal sealed class HarnessDeliveredSegmentFilteringSnapshotProvider(
    IHarnessContextSnapshotProvider inner,
    HarnessCompactionRunCoordinator coordinator,
    Guid leaseId) : IHarnessContextSnapshotProvider
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
                !coordinator.TryReserve(digest, leaseId))
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
