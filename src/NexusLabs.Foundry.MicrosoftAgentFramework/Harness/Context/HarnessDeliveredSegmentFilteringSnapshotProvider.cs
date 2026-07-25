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
/// through completely unfiltered. Reserving a digest here does not, by itself, deliver it:
/// <see cref="HarnessHybridCompactionChatClient"/> only closes this call's own <paramref name="leaseId"/>-owned
/// lease via <see cref="HarnessCompactionRunCoordinator.Complete"/>, after the real provider call this
/// capture feeds has itself completed successfully — promoting forwarded digests to Delivered and
/// atomically releasing any remaining (pressure-evicted or non-forwarded) reservations owned by this
/// lease — and releases them all, via <see cref="HarnessCompactionRunCoordinator.Release"/>, if assembly
/// or dispatch fails or is canceled first, so a retry can still reserve and deliver the exact same
/// digest. Every repeated capture within one assembly attempt shares the same <paramref name="leaseId"/>,
/// so it keeps observing its own reservations as held rather than filtering them back out a second time.
/// <para>
/// <strong>Own effective version, independent of the inner snapshot's version.</strong> The inner
/// <see cref="HarnessContextSnapshot.Version"/> alone is not sufficient to signal every restart-worthy
/// change this wrapper can observe: a concurrent lease can hold a digest's reservation at the moment this
/// call captures (so this call filters that digest's body back out), and later release it — without
/// forwarding it — after this call's own snapshot was already captured, all while the inner provider's
/// version never changes (no new message was injected; only reservation/delivery state, tracked
/// separately by <see cref="HarnessCompactionRunCoordinator"/>, changed). Left undetected, that leaves
/// the released body permanently undelivered: the caller that lost the race never restarts to see it
/// newly available, and the caller that released it already dispatched without it. To close this gap,
/// this wrapper maintains its own monotonic effective version, returned in place of the inner snapshot's
/// version on every <see cref="HarnessContextSnapshot.Version"/> observed by
/// <see cref="HarnessContextAssembler"/>. On every <see cref="CaptureSnapshot"/> call, after making this
/// call's own reservation/filter decisions for every recoverable segment present in the inner snapshot,
/// this wrapper reads each such digest's current <see cref="HarnessCompactionRunCoordinator.GetRevision"/>
/// value and combines the sorted set of (digest, revision) pairs with the inner snapshot's own version
/// into one signature. The very first capture always reports effective version <c>0</c>; every later
/// capture reports the previous effective version unchanged when neither the inner version nor any
/// relevant digest's revision changed since the previous capture, and one greater otherwise. A revision
/// change to some other digest never present in any snapshot this exact instance has captured can never
/// affect this signature, so it can never spuriously force a restart of an assembly attempt that never
/// observed that digest in the first place.
/// </para>
/// </remarks>
internal sealed class HarnessDeliveredSegmentFilteringSnapshotProvider(
    IHarnessContextSnapshotProvider inner,
    HarnessCompactionRunCoordinator coordinator,
    Guid leaseId) : IHarnessContextSnapshotProvider
{
    private bool _hasCaptured;
    private long _effectiveVersion;
    private long _previousInnerVersion;
    private IReadOnlyList<(string Digest, long Revision)> _previousRevisionSignature = [];

    public HarnessContextSnapshot CaptureSnapshot()
    {
        var snapshot = inner.CaptureSnapshot();

        var filtered = new List<HarnessContextEntry>(snapshot.Entries.Count);
        List<(string Digest, long Revision)>? revisionSignature = null;
        foreach (var entry in snapshot.Entries)
        {
            if (entry.Kind == HarnessContextEntryKind.RecoverableContextSegment &&
                entry.ArtifactReferenceDigest is { } digest)
            {
                var reserved = coordinator.TryReserve(digest, leaseId);

                // Read this digest's revision only after this call's own reservation decision has
                // been made for it, so the signature always reflects the up-to-date state — including
                // any bump this very reservation attempt itself just caused.
                revisionSignature ??= [];
                revisionSignature.Add((digest, coordinator.GetRevision(digest)));

                if (!reserved)
                {
                    continue;
                }
            }

            filtered.Add(entry);
        }

        revisionSignature?.Sort(static (left, right) => string.CompareOrdinal(left.Digest, right.Digest));
        var signature = (IReadOnlyList<(string Digest, long Revision)>?)revisionSignature ?? [];

        var effectiveVersion = ComputeEffectiveVersion(snapshot.Version, signature);

        return HarnessContextSnapshot.Create(effectiveVersion, filtered);
    }

    /// <summary>
    /// Advances (or, on the very first capture, initializes) this instance's own monotonic effective
    /// version by comparing <paramref name="innerVersion"/> and <paramref name="signature"/> — this
    /// capture's sorted (recoverable digest, coordinator revision) pairs for every recoverable segment
    /// present in the inner snapshot before filtering — against what the immediately preceding capture
    /// on this exact instance observed.
    /// </summary>
    private long ComputeEffectiveVersion(
        long innerVersion, IReadOnlyList<(string Digest, long Revision)> signature)
    {
        if (!_hasCaptured)
        {
            _hasCaptured = true;
            _effectiveVersion = 0;
        }
        else if (innerVersion != _previousInnerVersion || !SignatureEquals(signature, _previousRevisionSignature))
        {
            _effectiveVersion = checked(_effectiveVersion + 1);
        }

        _previousInnerVersion = innerVersion;
        _previousRevisionSignature = signature;
        return _effectiveVersion;
    }

    private static bool SignatureEquals(
        IReadOnlyList<(string Digest, long Revision)> left,
        IReadOnlyList<(string Digest, long Revision)> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        for (var i = 0; i < left.Count; i++)
        {
            if (left[i].Revision != right[i].Revision ||
                !string.Equals(left[i].Digest, right[i].Digest, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }
}
