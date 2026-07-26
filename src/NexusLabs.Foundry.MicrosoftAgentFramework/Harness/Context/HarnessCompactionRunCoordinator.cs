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
/// <para>
/// <strong>Same-run provider-call admission gate.</strong> Beyond the reservation/lease protocol above
/// — which decides <em>which</em> caller may forward a given digest — <see cref="EnterProviderCallAsync"/>
/// admits at most one nested provider call, from assembly through real provider dispatch, per outer run
/// scope at a time. <see cref="Harness.Context.HarnessHybridCompactionChatClient"/> acquires this gate
/// immediately after establishing its run scope and holds it until the real provider call (or stream) it
/// gates has itself finished and this node's own commit/release decision has been made, so a second
/// nested call within the very same outer run can never even begin assembling — let alone dispatching —
/// while a sibling reservation from an earlier call in that run remains unresolved. This closes the
/// narrow window the reservation/lease protocol alone cannot: two calls racing arbitrarily far into
/// their own assembly before either reserves anything. The per-digest revision logic
/// (<see cref="GetRevision"/>) remains in place as defense-in-depth for legitimate state changes observed
/// across sequential captures within one still-gated call, or by direct/standalone callers of this
/// coordinator that bypass <see cref="Harness.Context.HarnessHybridCompactionChatClient"/> entirely (as
/// several tests in this codebase deliberately do to prove that logic in isolation). The gate lives on
/// <see cref="RunState"/> exactly like every other piece of tracked state, so two different outer runs —
/// distinct <see cref="AsyncLocal{T}"/>-scoped <see cref="RunState"/> instances — always have their own,
/// entirely independent gate and therefore remain fully concurrent with each other.
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
    /// Admits the caller into the single same-run provider-call slot, asynchronously waiting if another
    /// nested call within the currently active run scope is already admitted. Returns a disposable
    /// releaser that must be disposed exactly once — typically via <see langword="using"/> — to vacate
    /// the slot for the next waiter, only once assembly, the real provider call/stream, and this node's
    /// own commit/release decision have all finished. Because the gate lives on the run-scoped
    /// <see cref="RunState"/>, two different outer runs (distinct <see cref="AsyncLocal{T}"/>-scoped
    /// <see cref="RunState"/> instances) always have independent gates and therefore never block one
    /// another; only nested calls sharing the exact same run scope are ever serialized here. Throws when
    /// no run scope is active on this call flow, for the same reason as every other lease-lifecycle
    /// operation on this type.
    /// </summary>
    /// <exception cref="InvalidOperationException">No run scope is active on this call flow.</exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> was canceled while waiting for the slot. No reservation is
    /// created and the slot is left exactly as if this call had never been made — a canceled waiter never
    /// acquires, and therefore never needs to release, the gate.
    /// </exception>
    internal async Task<IDisposable> EnterProviderCallAsync(CancellationToken cancellationToken)
    {
        // Checked before touching any state, exactly like the entry-point checkpoint in
        // HarnessHybridCompactionChatClient.AssembleBoundedMessagesAsync: a pre-canceled token must
        // throw the exact same OperationCanceledException type a mid-wait cancellation does (rather
        // than the TaskCanceledException a pre-canceled SemaphoreSlim.WaitAsync would otherwise
        // surface), so callers observe one consistent cancellation exception type regardless of
        // whether the token was already canceled or was canceled while this call was waiting.
        cancellationToken.ThrowIfCancellationRequested();

        var state = _current.Value;
        if (state is null)
        {
            throw new InvalidOperationException(
                $"{nameof(EnterProviderCallAsync)} requires an active run scope. Establish one via " +
                $"{nameof(EnsureRunScope)} or {nameof(BeginRun)} before invoking any lease-lifecycle " +
                "operation. Every legitimate path through this coordinator always establishes a scope " +
                "first; the absence of a scope is a caller contract violation.");
        }

        await state.ProviderCallGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        return new ProviderCallReleaser(state.ProviderCallGate);
    }

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
                // A repeated capture within the same caller's own assembly attempt observes its
                // own reservation again: no externally-observable state changed, so the digest's
                // revision is deliberately left untouched.
                return owner == leaseId;
            }

            state.Reserved[digest] = leaseId;

            // First reservation: unclaimed -> reserved is an externally-observable state change a
            // concurrent observer's own prior snapshot signature could have captured as "unclaimed",
            // so the digest's revision advances.
            BumpRevision(state, digest);
            return true;
        }
    }

    /// <summary>
    /// Returns <paramref name="digest"/>'s current monotonic revision within the currently active run
    /// scope: <c>0</c> if the digest has never been reserved, delivered, or released in this run, and a
    /// strictly greater value every time <see cref="TryReserve"/> first claims it, <see cref="Complete"/>
    /// promotes or releases it, or <see cref="Release"/> releases it. <see cref="Harness.Context.HarnessDeliveredSegmentFilteringSnapshotProvider"/>
    /// reads this after each capture's reservation decisions to detect exactly the case a bare delivered
    /// flag cannot: a digest this call's own snapshot filtered out because a concurrent lease held it,
    /// where that concurrent lease later completed without forwarding it and released it — a state
    /// change this call must restart to observe, even though neither <see cref="TryReserve"/> nor the
    /// snapshot's own <see cref="HarnessContextSnapshot.Version"/> reports it directly. Throws when no
    /// run scope is active on this call flow, for the same reason as every other lease-lifecycle
    /// operation on this type.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="digest"/> is empty or whitespace-only.</exception>
    /// <exception cref="InvalidOperationException">No run scope is active on this call flow.</exception>
    internal long GetRevision(string digest)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(digest);
        var state = _current.Value;
        if (state is null)
        {
            throw new InvalidOperationException(
                $"{nameof(GetRevision)} requires an active run scope. Establish one via " +
                $"{nameof(EnsureRunScope)} or {nameof(BeginRun)} before invoking any lease-lifecycle " +
                "operation. Every legitimate path through this coordinator always establishes a scope " +
                "first; the absence of a scope is a caller contract violation.");
        }

        lock (state.Sync)
        {
            return state.Revisions.TryGetValue(digest, out var revision) ? revision : 0;
        }
    }

    /// <summary>
    /// Advances <paramref name="digest"/>'s revision by exactly one within <paramref name="state"/>.
    /// Must always be called while already holding <paramref name="state"/>'s <see cref="RunState.Sync"/>
    /// lock — a checked (never wrapping) increment, since a run's lifetime is bounded and this coordinator
    /// never persists across runs.
    /// </summary>
    private static void BumpRevision(RunState state, string digest)
    {
        state.Revisions[digest] = state.Revisions.TryGetValue(digest, out var current)
            ? checked(current + 1)
            : 1;
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

                // Either branch above is an externally-observable state change — reserved -> delivered,
                // or reserved -> unclaimed — so the digest's revision always advances here, regardless
                // of which branch was taken.
                BumpRevision(state, digest);
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

                // Externally-observable state change — reserved -> unclaimed — so the digest's
                // revision always advances here.
                BumpRevision(state, digest);
            }
        }
    }

    private sealed class RunState
    {
        internal object Sync { get; } = new();

        internal HashSet<string> Delivered { get; } = new(StringComparer.Ordinal);

        internal Dictionary<string, Guid> Reserved { get; } = new(StringComparer.Ordinal);

        /// <summary>
        /// Per-digest monotonic revision counter: absent means revision 0 (never reserved, delivered,
        /// or released in this run). <see cref="BumpRevision"/> is the only writer, always called while
        /// holding <see cref="Sync"/>; <see cref="GetRevision"/> is the only external reader.
        /// </summary>
        internal Dictionary<string, long> Revisions { get; } = new(StringComparer.Ordinal);

        /// <summary>
        /// Admits exactly one nested provider call — assembly through real provider dispatch — at a
        /// time within this run. <see cref="EnterProviderCallAsync"/> is the sole acquirer;
        /// <see cref="ProviderCallReleaser"/> is the sole releaser. Scoped to this <see cref="RunState"/>
        /// instance (never static, never shared across runs) so two different outer runs never
        /// contend with one another.
        /// </summary>
        internal SemaphoreSlim ProviderCallGate { get; } = new(1, 1);
    }

    /// <summary>
    /// Disposable releaser returned by <see cref="EnterProviderCallAsync"/>. Releases
    /// <paramref name="gate"/> exactly once, no matter how many times <see cref="Dispose"/> is called,
    /// so a defensive double-dispose (e.g. an explicit call followed by a <see langword="using"/>
    /// block's implicit one) never over-releases the semaphore.
    /// </summary>
    private sealed class ProviderCallReleaser(SemaphoreSlim gate) : IDisposable
    {
        private int _released;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0)
            {
                gate.Release();
            }
        }
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
