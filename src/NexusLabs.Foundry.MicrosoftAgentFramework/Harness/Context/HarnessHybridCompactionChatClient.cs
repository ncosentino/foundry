using System.Runtime.CompilerServices;

using Microsoft.Extensions.AI;

using NexusLabs.Foundry.MicrosoftAgentFramework.Context;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Context;

/// <summary>
/// The proven per-provider-call hybrid compaction seam. Installed by
/// <see cref="HarnessCompactionComposition"/> as the innermost wrap around the real provider
/// <see cref="IChatClient"/>, this node observes the exact message set dispatched for <em>every</em>
/// intermediate <c>FunctionInvokingChatClient</c> tool round and every
/// <c>MessageInjectingChatClient</c>-driven extra call — never merely the outer agent call, and never
/// only the first or last provider request.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Verified middleware order (MAF 1.15 / MEAI 10.6).</strong> The Foundry-composed pipeline this
/// node is installed beneath is, outermost to innermost:
/// <c>ApprovalResponseBinding → ApprovalNotRequiredFunctionBypassing → FunctionInvokingChatClient →
/// MessageInjectingChatClient → HarnessExecutionBindingChatClient → PerServiceCallChatHistoryPersistingChatClient
/// (if configured) → telemetry (if configured) → this node → the real provider client</c>. Both
/// <c>FunctionInvokingChatClient</c> and <c>MessageInjectingChatClient</c> recurse by calling their own
/// inner client afresh for every tool round or injected batch respectively, so every such call cascades
/// fully down to this node; <c>PerServiceCallChatHistoryPersistingChatClient</c> prepends its loaded
/// history before calling its inner client, so this node — being inner to it — always observes the
/// complete, already-prepended message set for that exact call. This is why a per-call
/// <see cref="IChatClient"/> decorator at this exact position is required, and why MAF 1.15's built-in
/// <c>AIContextProvider</c>/<c>CompactionProvider</c> seam — evaluated once per agent turn rather than
/// once per provider request, and evaluated against a history index that has not yet observed the
/// current tool round's result — is structurally insufficient for observing every intermediate request.
/// </para>
/// <para>
/// <strong>No composition-root duplication.</strong> This node is constructed and installed entirely by
/// <see cref="HarnessCompactionComposition"/>, invoked internally by <see cref="HarnessProviderComposition"/>
/// against the exact same capability profile and <see cref="IChatClient"/> that composer itself received,
/// wrapping the caller's real chat client before the rest of that composer's own pipeline is built from
/// the result. <see cref="HarnessProviderComposition"/> remains the sole selected-provider composition
/// root; this node never constructs or duplicates one.
/// </para>
/// <para>
/// <strong>Never forwards over-budget or invalid context.</strong> Every call re-adapts the exact
/// messages presented for that call, assembles bounded context through
/// <see cref="HarnessContextAssembler"/> (itself backed by <see cref="HarnessCompactionVerifier"/>), and
/// either dispatches the verified final messages onward or throws
/// <see cref="HarnessCompactionIrreducibleException"/> — this node never silently forwards an
/// over-budget or rejected proposal, and never invents a durable provider guarantee: each call is
/// assembled fresh from the caller-supplied session/snapshot integration, with no singleton mutable
/// history retained by this node itself.
/// </para>
/// </remarks>
internal sealed class HarnessHybridCompactionChatClient(
    IChatClient innerClient,
    HarnessHybridProfile profile,
    HarnessExecutionBinding executionBinding,
    IAgentExecutionContextAccessor executionContextAccessor,
    string sessionId,
    HarnessCompactionRunCoordinator? runCoordinator) : DelegatingChatClient(innerClient)
{
    /// <summary>
    /// The non-retransmission coordinator for the active outer agent run. When a caller supplies
    /// one (always the case when this node is installed by <see cref="HarnessCompactionComposition"/>
    /// beneath a <see cref="Harness.HarnessGuardedAgent"/>), every nested provider call within that
    /// agent's outer run shares the same coordinator instance and therefore the same reserved/delivered
    /// digest state. When the caller-supplied coordinator is <see langword="null"/> — direct/standalone
    /// construction outside a guarded agent, as every seam/cancellation unit test in this codebase does
    /// — this node owns a private coordinator instance of its own so <see cref="GetResponseAsync"/> and
    /// <see cref="GetStreamingResponseAsync"/> still enforce non-retransmission per call via
    /// <see cref="HarnessCompactionRunCoordinator.EnsureRunScope"/>, without ever leaking delivered state
    /// from one call on this instance into a later, separate call.
    /// </summary>
    private readonly HarnessCompactionRunCoordinator _runCoordinator =
        runCoordinator ?? new HarnessCompactionRunCoordinator();

    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        // The run scope spans the entire call — assembly AND real dispatch — so that a lease reserved
        // during assembly below remains valid for the commit/release decision made once dispatch either
        // succeeds or fails, and so direct/standalone use (no outer HarnessGuardedAgent run) still
        // behaves correctly: were the scope instead confined to assembly alone, a call-local scope would
        // already have been disposed and reset before the commit/release step ran, silently discarding
        // the reservation bookkeeping for that call.
        using var runScope = _runCoordinator.EnsureRunScope();

        var assembly = await AssembleBoundedMessagesAsync(messages, cancellationToken).ConfigureAwait(false);

        var completedSuccessfully = false;
        try
        {
            var response = await base
                .GetResponseAsync(assembly.Messages, options, cancellationToken)
                .ConfigureAwait(false);
            completedSuccessfully = true;
            return response;
        }
        finally
        {
            if (completedSuccessfully)
            {
                _runCoordinator.Complete(assembly.LeaseId, assembly.ForwardedDigests);
            }
            else
            {
                _runCoordinator.Release(assembly.LeaseId);
            }
        }
    }

    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // See the remarks on GetResponseAsync: the run scope must span the entire call, not merely
        // assembly, for the same reason.
        using var runScope = _runCoordinator.EnsureRunScope();

        var assembly = await AssembleBoundedMessagesAsync(messages, cancellationToken).ConfigureAwait(false);

        var completedSuccessfully = false;
        var enumerator = base
            .GetStreamingResponseAsync(assembly.Messages, options, cancellationToken)
            .WithCancellation(cancellationToken)
            .GetAsyncEnumerator();
        try
        {
            // A try block containing `yield return` may not have a catch clause in C#, only finally.
            // Any exception thrown from MoveNextAsync (including cancellation) propagates past the loop
            // without ever reaching the `completedSuccessfully = true` line below, so the finally block
            // below correctly releases rather than commits. Reaching the end of the loop normally — even
            // after zero updates — still sets the flag, satisfying "commit on successful completion
            // including zero updates".
            while (await enumerator.MoveNextAsync())
            {
                yield return enumerator.Current;
            }

            completedSuccessfully = true;
        }
        finally
        {
            if (completedSuccessfully)
            {
                _runCoordinator.Complete(assembly.LeaseId, assembly.ForwardedDigests);
            }
            else
            {
                _runCoordinator.Release(assembly.LeaseId);
            }

            await enumerator.DisposeAsync();
        }
    }

    /// <summary>
    /// The outcome of one call's context assembly: the final bounded messages to dispatch, the lease
    /// token that reserved every recoverable segment digest surviving into <see cref="Messages"/>, and
    /// the set of digests this call's lease actually reserved and is about to forward to the real
    /// provider — the set passed to <see cref="HarnessCompactionRunCoordinator.Complete"/> on success.
    /// Pressure-evicted digests reserved during assembly but removed from <see cref="Messages"/> before
    /// this record is constructed are not included here; <see cref="HarnessCompactionRunCoordinator.Complete"/>
    /// releases those automatically alongside promoting the forwarded ones to Delivered.
    /// </summary>
    private sealed record HarnessBoundedMessageAssembly(
        IReadOnlyList<ChatMessage> Messages,
        Guid LeaseId,
        IReadOnlyList<string> ForwardedDigests);

    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
    /// <exception cref="InvalidOperationException">
    /// The execution binding is no longer current, whether checked before assembly begins or
    /// revalidated immediately after assembly succeeds and before dispatch to the real provider.
    /// </exception>
    /// <exception cref="HarnessCompactionIrreducibleException">
    /// Assembly for this call terminated as <see cref="HarnessContextAssemblyOutcome.Irreducible"/> or
    /// <see cref="HarnessContextAssemblyOutcome.ConcurrentMutationLimit"/>.
    /// </exception>
    /// <remarks>
    /// <strong>Non-retransmission — reserve here, commit/release around dispatch.</strong> This method
    /// never marks anything delivered. Every call generates its own fresh lease token and wraps the
    /// host's <see cref="HarnessContextSnapshotIntegration"/>-supplied snapshot provider in a
    /// <see cref="HarnessDeliveredSegmentFilteringSnapshotProvider"/> bound to that lease, so a
    /// <see cref="HarnessContextEntryKind.RecoverableContextSegment"/> body already promoted to Delivered
    /// earlier in the active run scope — or currently reserved by a different, concurrently-running
    /// provider call within the same run — is filtered back out before the assembler ever considers it,
    /// while a repeated capture within this same assembly attempt keeps observing its own lease's
    /// reservations as held. On any failure — including cancellation, the irreducible-assembly case, and
    /// a binding revalidation failure — every reservation this lease acquired during the attempt is
    /// released via <see langword="try"/>/<see langword="finally"/> (no broad catch; exceptions propagate
    /// naturally) before the triggering exception propagates, so a subsequent retry within the same run
    /// scope can still reserve, and ultimately deliver, the exact same digests. On success, this method
    /// returns the forwarded digests as <see cref="HarnessBoundedMessageAssembly.ForwardedDigests"/>
    /// without promoting them — the caller (<see cref="GetResponseAsync"/> or
    /// <see cref="GetStreamingResponseAsync"/>) alone decides whether to complete or release, once it
    /// knows whether the real provider call actually completed successfully.
    /// </remarks>
    private async Task<HarnessBoundedMessageAssembly> AssembleBoundedMessagesAsync(
        IEnumerable<ChatMessage> messages, CancellationToken cancellationToken)
    {
        // First checkpoint: a pre-canceled or already-canceled token must never reach the real
        // provider client, and must never be masked by any catch anywhere in this node.
        cancellationToken.ThrowIfCancellationRequested();
        executionBinding.EnsureCurrent(executionContextAccessor, sessionId);

        var leaseId = Guid.NewGuid();
        var leaseOwned = false;
        try
        {
            var materializedMessages = messages as IReadOnlyList<ChatMessage> ?? messages.ToList();
            var baselineEntries = HarnessMafMessageContextAdapter.Adapt(materializedMessages, profile.Classifier);
            var snapshotProvider = profile.SnapshotIntegration(baselineEntries);
            if (snapshotProvider is null)
            {
                throw new InvalidOperationException(
                    $"The configured {nameof(HarnessContextSnapshotIntegration)} delegate returned null. It " +
                    "must always return a non-null snapshot provider for the supplied baseline entries.");
            }

            var filteringSnapshotProvider = new HarnessDeliveredSegmentFilteringSnapshotProvider(
                snapshotProvider, _runCoordinator, leaseId);
            var reducer = new HarnessUpstreamChatReducerAdapter(profile.UpstreamReducer);
            var assembler = new HarnessContextAssembler(profile.Policy, filteringSnapshotProvider, reducer);

            var result = await assembler.AssembleAsync(cancellationToken).ConfigureAwait(false);

            // Second checkpoint, deliberately distinct from the assembler's own internal checks: a
            // cancellation observed at the exact instant assembly finishes — after the reducer ran,
            // before this node ever dispatches a single message onward to the real provider client —
            // must still surface here rather than allowing a completed assembly to be dispatched
            // regardless.
            cancellationToken.ThrowIfCancellationRequested();

            if (!result.IsSuccess)
            {
                throw new HarnessCompactionIrreducibleException(
                    result.Outcome, result.FinalEstimatedSize, result.HardLimit);
            }

            // Trust revalidation, immediately after successful assembly and immediately before dispatch
            // to the real provider: assembly can run for an observable duration (reducer/upstream work),
            // so the binding validated at entry to this method is revalidated again here, right before
            // the bounded messages this node assembled are ever handed onward. This is deliberately in
            // addition to, not instead of, the entry-point check above and any outer defense-in-depth
            // binding validation.
            executionBinding.EnsureCurrent(executionContextAccessor, sessionId);

            // Forwarded digests are only the recoverable segments that actually survived assembly (and
            // are therefore about to be dispatched) — never a segment pressure-evicted before reaching
            // here, since eviction removes it from result.FinalEntries entirely before this point is
            // ever reached. These are reserved by this lease already (via the filtering snapshot
            // provider above); Complete releases all remaining (non-forwarded) reservations atomically
            // when the caller commits on success.
            var forwardedDigests = result.FinalEntries!
                .Where(entry => entry.Kind == HarnessContextEntryKind.RecoverableContextSegment)
                .Select(entry => entry.ArtifactReferenceDigest)
                .Where(digest => digest is not null)
                .Select(digest => digest!)
                .ToList();

            var boundedMessages = result.FinalEntries!.Select(entry => entry.Message).ToList();
            leaseOwned = true;
            return new HarnessBoundedMessageAssembly(boundedMessages, leaseId, forwardedDigests);
        }
        finally
        {
            if (!leaseOwned)
            {
                _runCoordinator.Release(leaseId);
            }
        }
    }
}
