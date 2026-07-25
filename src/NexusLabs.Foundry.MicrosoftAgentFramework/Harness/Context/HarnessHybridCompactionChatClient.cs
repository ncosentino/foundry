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
    /// agent's outer run shares the same coordinator instance and therefore the same delivered-digest
    /// set. When the caller-supplied coordinator is <see langword="null"/> — direct/standalone
    /// construction outside a guarded agent, as every seam/cancellation unit test in this codebase does
    /// — this node owns a private coordinator instance of its own so <see cref="AssembleBoundedMessagesAsync"/>
    /// still enforces non-retransmission per call via <see cref="HarnessCompactionRunCoordinator.EnsureRunScope"/>,
    /// without ever leaking delivered state from one call on this instance into a later, separate call.
    /// </summary>
    private readonly HarnessCompactionRunCoordinator _runCoordinator =
        runCoordinator ?? new HarnessCompactionRunCoordinator();

    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var boundedMessages = await AssembleBoundedMessagesAsync(messages, cancellationToken)
            .ConfigureAwait(false);
        return await base
            .GetResponseAsync(boundedMessages, options, cancellationToken)
            .ConfigureAwait(false);
    }

    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var boundedMessages = await AssembleBoundedMessagesAsync(messages, cancellationToken)
            .ConfigureAwait(false);

        await foreach (var update in base
            .GetStreamingResponseAsync(boundedMessages, options, cancellationToken)
            .WithCancellation(cancellationToken)
            .ConfigureAwait(false))
        {
            yield return update;
        }
    }

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
    /// <strong>Non-retransmission.</strong> <see cref="_runCoordinator"/>'s
    /// <see cref="HarnessCompactionRunCoordinator.EnsureRunScope"/> either joins the one run scope a
    /// <see cref="Harness.HarnessGuardedAgent"/> already began around the entire outer run this call
    /// nests inside, or — for direct/standalone use of this node — begins and disposes a fresh,
    /// call-local scope around this one call only. Either way, the snapshot provider the host's
    /// <see cref="HarnessContextSnapshotIntegration"/> delegate returns is wrapped in a
    /// <see cref="HarnessDeliveredSegmentFilteringSnapshotProvider"/> so a
    /// <see cref="HarnessContextEntryKind.RecoverableContextSegment"/> body already delivered earlier in
    /// the active run scope is filtered back out before the assembler ever considers it, and — on
    /// successful assembly, immediately before dispatch — every recoverable segment that actually
    /// survived to the final forwarded entries (never one evicted under pressure, since an evicted body
    /// is never present in <c>result.FinalEntries</c>) is marked delivered for the remainder of that
    /// scope. Marking happens before this method returns rather than after the real provider call
    /// completes: a caller retrying after the real provider call itself fails must not have the raw body
    /// forwarded again, which this conservative ordering guarantees at the cost of (harmlessly) treating
    /// an assembled-but-never-actually-transmitted call as delivered in that narrow retry window.
    /// </remarks>
    private async Task<IReadOnlyList<ChatMessage>> AssembleBoundedMessagesAsync(
        IEnumerable<ChatMessage> messages, CancellationToken cancellationToken)
    {
        // First checkpoint: a pre-canceled or already-canceled token must never reach the real
        // provider client, and must never be masked by a broad catch anywhere in this node.
        cancellationToken.ThrowIfCancellationRequested();
        executionBinding.EnsureCurrent(executionContextAccessor, sessionId);

        using var runScope = _runCoordinator.EnsureRunScope();

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
            snapshotProvider, _runCoordinator);
        var reducer = new HarnessUpstreamChatReducerAdapter(profile.UpstreamReducer);
        var assembler = new HarnessContextAssembler(profile.Policy, filteringSnapshotProvider, reducer);

        var result = await assembler.AssembleAsync(cancellationToken).ConfigureAwait(false);

        // Second checkpoint, deliberately distinct from the assembler's own internal checks: a
        // cancellation observed at the exact instant assembly finishes — after the reducer ran, before
        // this node ever dispatches a single message onward to the real provider client — must still
        // surface here rather than allowing a completed assembly to be dispatched regardless.
        cancellationToken.ThrowIfCancellationRequested();

        if (!result.IsSuccess)
        {
            throw new HarnessCompactionIrreducibleException(
                result.Outcome, result.FinalEstimatedSize, result.HardLimit);
        }

        // Trust revalidation, immediately after successful assembly and immediately before dispatch to
        // the real provider: assembly can run for an observable duration (reducer/upstream work), so the
        // binding validated at entry to this method is revalidated again here, right before the bounded
        // messages this node assembled are ever handed onward. This is deliberately in addition to, not
        // instead of, the entry-point check above and any outer defense-in-depth binding validation.
        executionBinding.EnsureCurrent(executionContextAccessor, sessionId);

        // Mark only the recoverable segments that actually survived assembly (and therefore are
        // about to be dispatched) delivered — never a segment pressure-evicted before reaching here,
        // since eviction removes it from result.FinalEntries entirely before this point is ever reached.
        var deliveredDigests = result.FinalEntries!
            .Where(entry => entry.Kind == HarnessContextEntryKind.RecoverableContextSegment)
            .Select(entry => entry.ArtifactReferenceDigest)
            .Where(digest => digest is not null)
            .Select(digest => digest!);
        _runCoordinator.MarkDelivered(deliveredDigests);

        return result.FinalEntries!.Select(entry => entry.Message).ToList();
    }
}
