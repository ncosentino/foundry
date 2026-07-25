namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Context;

/// <summary>
/// Coordinates per-outer-agent-run delivery state for stable recovered artifact bodies: a
/// <see cref="HarnessArtifactRecoverableContextSegment"/>'s raw body must be dispatched to the real
/// provider at most once per outer agent run, even though the exact same
/// <see cref="HarnessContextSnapshotIntegration"/> selection may be re-observed on every nested
/// <c>FunctionInvokingChatClient</c> tool round or <c>MessageInjectingChatClient</c>-driven extra call
/// within that run — including two such calls racing concurrently within the very same run.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Atomic reservation/lease protocol, not a bare delivered flag.</strong> A digest moves through
/// at most three states within one run scope: unclaimed, reserved by exactly one lease
/// (<see cref="TryReserve"/>), and delivered (<see cref="Complete"/>). <see cref="TryReserve"/> is the
/// one atomic decision point: the first caller — identified by its own <see cref="Guid"/> lease token —
/// to reserve a given digest during a run wins it; every other concurrent caller in the same run scope
/// observes the reservation and must filter that digest back out of what it forwards, rather than
/// forwarding it a second time. A caller's own repeated captures within one assembly attempt (the same
/// lease token reserving the same digest again) keep observing it as reserved-by-itself, so an
/// assembler's own retry loop within a single call never spuriously filters out its own selection.
/// </para>
/// <para>
/// <strong>Atomic lease completion; release on failure so a retry can redeliver.</strong> A reservation
/// is provisional until <see cref="Complete"/> atomically closes the lease — which
/// <see cref="Harness.Context.HarnessHybridCompactionChatClient"/> only ever calls after the real
/// provider call this reservation gates has itself completed successfully. <see cref="Complete"/>
/// promotes only the actually-forwarded digests to Delivered and, in the same lock acquisition,
/// releases every remaining reservation the lease holds — including pressure-evicted bodies that
/// survived reservation but were removed from the final entries before dispatch — so they never
/// remain stranded and a later call within the same run scope can still reserve and deliver them
/// if a later snapshot or policy would forward them. If assembly or the real provider call fails
/// or is canceled first, <see cref="Release"/> discards every reservation still held by that lease,
/// so the digest becomes reservable again — a subsequent retry within the same run scope can still
/// deliver the body, rather than the digest being permanently (and incorrectly) treated as already
/// delivered after a call that never actually reached the provider.
/// </para>
/// <para>
/// <strong>Instance-scoped <see cref="AsyncLocal{T}"/>, never static.</strong> Each composed pipeline
/// that enables hybrid compaction owns exactly one <see cref="HarnessCompactionRunCoordinator"/>
/// instance, shared by construction between <see cref="Harness.HarnessGuardedAgent"/> (which begins,
/// via <see cref="BeginRun"/>, and disposes the one run scope around its entire outer
/// <c>RunCoreAsync</c>/<c>RunCoreStreamingAsync</c> call) and <see cref="Harness.Context.HarnessHybridCompactionChatClient"/>
/// (which reserves, commits, and releases digests, via <see cref="EnsureRunScope"/>, during every nested
/// provider call within that scope). The tracked state lives behind an <see cref="AsyncLocal{T}"/> field
/// that is itself an instance member of this class — never a <see langword="static"/> field of any
/// type — so no coordinator instance ever leaks delivered state to a different composed agent instance.
/// Two concurrent outer runs on the very same composed agent still observe independent delivered sets:
/// that is the ordinary, well-known isolation guarantee of <see cref="AsyncLocal{T}"/> across distinct
/// logical call flows, not any locking or partitioning this type performs itself. Two concurrent nested
/// calls <em>within</em> the same outer run, by contrast, share the exact same run state instance — the
/// locking inside that shared state is what makes <see cref="TryReserve"/> atomic across them.
/// </para>
/// <para>
/// <strong>No serialization, no workspace/session persistence.</strong> This coordinator's state is
/// transient, in-process call-graph memory only. It is never written into
/// <see cref="Harness.HarnessSessionEnvelope"/>, never persisted through any workspace or session store,
/// and always resets to empty for every new outer run started via <see cref="BeginRun"/>.
/// </para>
/// </remarks>
internal sealed class HarnessCompactionRunCoordinator
{
    private readonly AsyncLocal<RunState?> _current = new();

    /// <summary>
    /// Begins a brand-new run scope for the current async call flow, tracking no digest as reserved or
    /// delivered yet, regardless of whatever run state (if any) was previously active on this flow. The
    /// prior state is restored when the returned scope is disposed. Intended for exactly one caller per
    /// outer run: <see cref="Harness.HarnessGuardedAgent.RunCoreAsync"/> and
    /// <see cref="Harness.HarnessGuardedAgent.RunCoreStreamingAsync"/>, each of which begins one scope
    /// around its entire outer run so every nested provider call observes the same shared state, and a
    /// later, separate outer run always starts from an empty set again.
    /// </summary>
    internal IDisposable BeginRun()
    {
        var previous = _current.Value;
        _current.Value = new RunState();
        return new RunScope(this, previous);
    }

    /// <summary>
    /// Returns the currently active run scope for this async call flow without disturbing it, or — if
    /// no run is currently active — begins and returns a fresh, call-local run scope exactly as
    /// <see cref="BeginRun"/> would. <see cref="Harness.Context.HarnessHybridCompactionChatClient"/> calls this
    /// once per provider call so that direct/standalone use outside a <see cref="Harness.HarnessGuardedAgent"/>
    /// run never leaks delivered state into a later, unrelated call, while nested calls inside an
    /// already-active outer run correctly continue observing (and contributing to) that same run's
    /// shared state.
    /// </summary>
    internal IDisposable EnsureRunScope() =>
        _current.Value is not null
            ? NoOpScope.Instance
            : BeginRun();

    /// <summary>
    /// Atomically reserves <paramref name="digest"/> for <paramref name="leaseId"/> within the currently
    /// active run scope, returning whether the caller owning <paramref name="leaseId"/> should treat the
    /// digest as available to forward. Returns <see langword="false"/> when <paramref name="digest"/> was
    /// already promoted to delivered via <see cref="Complete"/> earlier in this run, or is currently
    /// reserved by a different lease (a concurrent provider call in the same run already claimed it
    /// first) — the caller must filter that digest's body back out. Returns <see langword="true"/> when
    /// <paramref name="digest"/> was not yet claimed by anyone (this call becomes the first, and only,
    /// reservation owner for it) or was already reserved by this exact <paramref name="leaseId"/>
    /// (a repeated capture within the same caller's own assembly attempt). Throws when no run scope is
    /// active on this call flow: every legitimate path establishes one via <see cref="EnsureRunScope"/>
    /// before invoking this method, so the absence of a scope is a caller contract violation, not a
    /// recoverable condition.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="digest"/> is empty or whitespace-only.</exception>
    /// <exception cref="InvalidOperationException">No run scope is active on this call flow.</exception>
    internal bool TryReserve(string digest, Guid leaseId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(digest);
        var state = _current.Value;
        if (state is null)
        {
            throw new InvalidOperationException(
                $"{nameof(TryReserve)} requires an active run scope. Establish one via " +
                $"{nameof(EnsureRunScope)} or {nameof(BeginRun)} before invoking any lease-lifecycle " +
                "operation. Every legitimate path through this coordinator always establishes a scope " +
                "first; the absence of a scope is a caller contract violation.");
        }

        lock (state.Sync)
        {
            if (state.Delivered.Contains(digest))
            {
                return false;
            }

            if (state.Reserved.TryGetValue(digest, out var owner))
            {
                return owner == leaseId;
            }

            state.Reserved[digest] = leaseId;
            return true;
        }
    }

    /// <summary>
    /// Atomically closes the lease: promotes every supplied actually-forwarded digest reserved by
    /// <paramref name="leaseId"/> to Delivered, then releases every remaining reservation this lease
    /// still holds — including pressure-evicted bodies that were reserved during assembly but removed
    /// from the final forwarded entries before dispatch. Both steps execute under a single lock
    /// acquisition so no concurrent observer can interleave between promotion and release. A digest in
    /// <paramref name="deliveredDigests"/> that <paramref name="leaseId"/> does not actually hold a
    /// reservation for is silently ignored — this method only ever promotes what this exact lease itself
    /// reserved. Throws when no run scope is active on this call flow: every legitimate path establishes
    /// one via <see cref="EnsureRunScope"/> before invoking this method.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="deliveredDigests"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">No run scope is active on this call flow.</exception>
    internal void Complete(Guid leaseId, IEnumerable<string> deliveredDigests)
    {
        ArgumentNullException.ThrowIfNull(deliveredDigests);
        var state = _current.Value;
        if (state is null)
        {
            throw new InvalidOperationException(
                $"{nameof(Complete)} requires an active run scope. Establish one via " +
                $"{nameof(EnsureRunScope)} or {nameof(BeginRun)} before invoking any lease-lifecycle " +
                "operation. Every legitimate path through this coordinator always establishes a scope " +
                "first; the absence of a scope is a caller contract violation.");
        }

        lock (state.Sync)
        {
            // Materialize the delivered set inside the lock so we enumerate deliveredDigests exactly once
            // and never re-enter the lock while holding it.
            var deliveredSet = new HashSet<string>(deliveredDigests, StringComparer.Ordinal);

            // Collect every digest currently reserved by this lease.
            List<string>? ownedDigests = null;
            foreach (var (digest, owner) in state.Reserved)
            {
                if (owner == leaseId)
                {
                    ownedDigests ??= [];
                    ownedDigests.Add(digest);
                }
            }

            if (ownedDigests is null)
            {
                return;
            }

            // Promote delivered digests; release the rest — all under this same lock acquisition.
            foreach (var digest in ownedDigests)
            {
                state.Reserved.Remove(digest);
                if (deliveredSet.Contains(digest))
                {
                    state.Delivered.Add(digest);
                }
                // else: not forwarded (pressure-evicted or filtered out); released without promotion so a
                // later call in the same run scope can still reserve and deliver it.
            }
        }
    }

    /// <summary>
    /// Releases every digest currently reserved by <paramref name="leaseId"/> in the currently active run
    /// scope, without ever marking any of them delivered. Called when assembly or the real provider call
    /// a reservation was gating fails or is canceled before completing successfully, so a later retry
    /// within the same run scope can still reserve — and ultimately deliver — the exact same digest,
    /// rather than the digest being stranded in a permanently-reserved, never-delivered state. Does
    /// nothing at all if no run scope is active.
    /// </summary>
    internal void Release(Guid leaseId)
    {
        var state = _current.Value;
        if (state is null)
        {
            return;
        }

        lock (state.Sync)
        {
            List<string>? ownedDigests = null;
            foreach (var (digest, owner) in state.Reserved)
            {
                if (owner == leaseId)
                {
                    ownedDigests ??= [];
                    ownedDigests.Add(digest);
                }
            }

            if (ownedDigests is null)
            {
                return;
            }

            foreach (var digest in ownedDigests)
            {
                state.Reserved.Remove(digest);
            }
        }
    }

    private sealed class RunState
    {
        internal object Sync { get; } = new();

        internal HashSet<string> Delivered { get; } = new(StringComparer.Ordinal);

        internal Dictionary<string, Guid> Reserved { get; } = new(StringComparer.Ordinal);
    }

    private sealed class RunScope(HarnessCompactionRunCoordinator owner, RunState? previous) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            owner._current.Value = previous;
        }
    }

    private sealed class NoOpScope : IDisposable
    {
        internal static readonly NoOpScope Instance = new();

        public void Dispose()
        {
        }
    }
}
