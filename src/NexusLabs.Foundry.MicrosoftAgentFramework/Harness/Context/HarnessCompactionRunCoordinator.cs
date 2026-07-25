namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Context;

/// <summary>
/// Coordinates per-outer-agent-run delivery state for stable recovered artifact bodies: a
/// <see cref="HarnessArtifactRecoverableContextSegment"/>'s raw body must be dispatched to the real
/// provider at most once per outer agent run, even though the exact same
/// <see cref="HarnessContextSnapshotIntegration"/> selection may be re-observed on every nested
/// <c>FunctionInvokingChatClient</c> tool round or <c>MessageInjectingChatClient</c>-driven extra call
/// within that run.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Instance-scoped <see cref="AsyncLocal{T}"/>, never static.</strong> Each composed pipeline
/// that enables hybrid compaction owns exactly one <see cref="HarnessCompactionRunCoordinator"/>
/// instance, shared by construction between <see cref="Harness.HarnessGuardedAgent"/> (which begins,
/// via <see cref="BeginRun"/>, and disposes the one run scope around its entire outer
/// <c>RunCoreAsync</c>/<c>RunCoreStreamingAsync</c> call) and <see cref="HarnessHybridCompactionChatClient"/>
/// (which reads and marks delivered digests, via <see cref="EnsureRunScope"/>, during every nested
/// provider call within that scope). The tracked state lives behind an <see cref="AsyncLocal{T}"/> field
/// that is itself an instance member of this class — never a <see langword="static"/> field of any
/// type — so no coordinator instance ever leaks delivered state to a different composed agent instance.
/// Two concurrent outer runs on the very same composed agent still observe independent delivered sets:
/// that is the ordinary, well-known isolation guarantee of <see cref="AsyncLocal{T}"/> across distinct
/// logical call flows, not any locking or partitioning this type performs itself.
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
    /// Begins a brand-new run scope for the current async call flow, tracking no digest as delivered
    /// yet, regardless of whatever run state (if any) was previously active on this flow. The prior
    /// state is restored when the returned scope is disposed. Intended for exactly one caller per outer
    /// run: <see cref="Harness.HarnessGuardedAgent.RunCoreAsync"/> and
    /// <see cref="Harness.HarnessGuardedAgent.RunCoreStreamingAsync"/>, each of which begins one scope
    /// around its entire outer run so every nested provider call observes the same delivered set, and a
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
    /// <see cref="BeginRun"/> would. <see cref="HarnessHybridCompactionChatClient"/> calls this once per
    /// provider call so that direct/standalone use outside a <see cref="Harness.HarnessGuardedAgent"/>
    /// run never leaks delivered state into a later, unrelated call, while nested calls inside an
    /// already-active outer run correctly continue observing (and contributing to) that same run's
    /// delivered set.
    /// </summary>
    internal IDisposable EnsureRunScope() =>
        _current.Value is not null
            ? NoOpScope.Instance
            : BeginRun();

    /// <summary>
    /// <see langword="true"/> if <paramref name="digest"/> was already marked delivered (via
    /// <see cref="MarkDelivered"/>) during the currently active run scope; <see langword="false"/> if no
    /// run scope is active on this call flow or the digest was never marked.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="digest"/> is empty or whitespace-only.</exception>
    internal bool IsDelivered(string digest)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(digest);
        var state = _current.Value;
        if (state is null)
        {
            return false;
        }

        lock (state.Delivered)
        {
            return state.Delivered.Contains(digest);
        }
    }

    /// <summary>
    /// Marks every supplied digest delivered for the remainder of the currently active run scope; a
    /// no-op, per digest, for any digest already marked. Does nothing at all if no run scope is active
    /// (in practice <see cref="HarnessHybridCompactionChatClient"/> only ever calls this after itself
    /// establishing a scope via <see cref="EnsureRunScope"/>, so this should never observe an absent
    /// scope).
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="digests"/> is <see langword="null"/>.</exception>
    internal void MarkDelivered(IEnumerable<string> digests)
    {
        ArgumentNullException.ThrowIfNull(digests);
        var state = _current.Value;
        if (state is null)
        {
            return;
        }

        lock (state.Delivered)
        {
            foreach (var digest in digests)
            {
                state.Delivered.Add(digest);
            }
        }
    }

    private sealed class RunState
    {
        internal HashSet<string> Delivered { get; } = new(StringComparer.Ordinal);
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
