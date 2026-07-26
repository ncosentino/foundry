namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Context;

/// <summary>
/// Deterministically assembles a dispatch-eligible entries list bounded by a
/// <see cref="HarnessHybridContextPolicy"/>'s hard limit. Never returns unchanged over-budget history as
/// a success — every non-<see cref="HarnessContextAssemblyOutcome.WithinLimit"/> success is either a
/// verified, strictly size-reducing proposal from the configured <see cref="IHarnessContextReducer"/>, or
/// the verifier's own preservation-only fallback candidate; anything that still exceeds the hard limit
/// after both is a distinct structured termination, never a silently forwarded success.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Trigger-margin gating.</strong> Assembly begins by evaluating the current snapshot's
/// estimated size against <see cref="HarnessHybridContextPolicy.Evaluate"/>. If the result is
/// <em>strictly below</em> the trigger threshold (<c>HardLimit - TriggerMargin</c>), no eviction or
/// reducer invocation occurs: the original entries are verified as-is, and
/// <see cref="HarnessContextAssemblyOutcome.WithinLimit"/> is returned if the verifier accepts, or
/// <see cref="HarnessContextAssemblyOutcome.Irreducible"/> if it rejects (an invalid tool sequence is
/// always irreducible, even under the hard limit). At or above the trigger threshold, this type always
/// makes an actual pressure-handling attempt — reaching the trigger is never silently treated as
/// dispatch-eligible on its own.
/// </para>
/// <para>
/// <strong>Deterministic reduction order.</strong> (1) Every entry a <see cref="HarnessHybridContextPolicy"/>
/// requires — system, authoritative session, approval/security, artifact references, incomplete tool
/// exchanges, reference-less recoverable segments, and the configured trailing recency window — is
/// preserved throughout. (2) Recoverable rehydrated bodies (<see cref="HarnessContextEntryKind.RecoverableContextSegment"/>)
/// backed by a matching <see cref="HarnessContextEntryKind.ArtifactReference"/> digest are evicted
/// first, ahead of any reducer invocation; a body with no durable reference is required instead (see
/// <see cref="HarnessHybridContextPolicy.SelectRequiredPreservation"/>) and may make the context
/// irreducible if it cannot otherwise fit. Eviction alone only ever short-circuits the reducer when it
/// actually occurred <em>and</em> the resulting size drops strictly below the trigger threshold — the
/// one explicit margin-restored rule. (3) Otherwise — no body was evicted, or the evicted size remains
/// at or above the trigger threshold — the configured <see cref="IHarnessContextReducer"/> is invoked
/// even when the current size already fits the hard limit, bounded by
/// <see cref="HarnessHybridContextPolicy.MaximumCompactionAttempts"/>; every proposal is verified by
/// <see cref="HarnessCompactionVerifier"/> against the current authoritative snapshot and must strictly
/// reduce the estimated size to ever be forwarded. An invalid or non-reducing proposal never terminates
/// assembly by itself: once the attempt is recorded, if the current (pre-attempt) entries already fit
/// the hard limit they are preserved as a successful result; only when they still exceed the hard limit
/// does this fall through to the deterministic fallback. (4) If the attempt budget is exhausted without
/// reaching the hard limit, a deterministic fallback tries the verifier's preservation-only candidate —
/// first extended with any retained <see cref="HarnessContextEntryKind.OptionalContext"/> entries, then
/// required-only if that still does not fit — in original order. (5) If neither fallback candidate fits
/// or independently re-verifies, and no version change is pending at that instant, assembly terminates as
/// <see cref="HarnessContextAssemblyOutcome.Irreducible"/> — this is the outcome even when an earlier
/// restart occurred elsewhere in the assembly, as long as a later snapshot version was successfully
/// established and required context still cannot fit or verify against it.
/// <see cref="HarnessContextAssemblyOutcome.ConcurrentMutationLimit"/> is reserved exclusively for the
/// direct churn path: a version change is observed but the bounded attempt budget is already exhausted
/// before that restart can be consumed (see <see cref="CheckFinalizationVersion"/>'s
/// <c>ChurnExhausted</c> outcome). Neither termination ever returns an over-budget list.
/// <see cref="HarnessHybridContextPolicy.HardLimit"/> remains the only final dispatch cap throughout.
/// </para>
/// <para>
/// <strong>Message-injection interaction.</strong> Every entries list this type reasons about is taken
/// from an explicit <see cref="HarnessContextSnapshot"/> obtained from the configured
/// <see cref="IHarnessContextSnapshotProvider"/>. After every reducer invocation the provider is
/// re-queried: if its <see cref="HarnessContextSnapshot.Version"/> changed, the in-flight proposal is
/// discarded — never merged, never patched — and assembly restarts deterministically from the newest
/// snapshot's entries. A final version recheck is also performed immediately before <em>every</em>
/// success return — below-trigger, post-eviction, post-reducer, preserved-after-non-reducing-reducer,
/// and preservation-fallback alike — so a newly injected entry is neither lost (it is present in the
/// very next snapshot capture) nor duplicated (a discarded candidate is never combined with the new
/// snapshot). Each such restart consumes one attempt of the bounded budget, except when the restart is
/// detected mid-reducer-loop (the reducer invocation for that attempt already consumed it). Before any
/// restart is consumed, the budget is checked first: if it is already exhausted, assembly terminates
/// directly as <see cref="HarnessContextAssemblyOutcome.ConcurrentMutationLimit"/> against the latest
/// observed snapshot's evidence — including at the deterministic fallback's own finalization check —
/// rather than ever returning a stale success computed against content that unstable, perpetual churn
/// has already superseded. <see cref="HarnessContextAssemblyResult.AttemptCount"/> therefore never
/// exceeds <see cref="HarnessHybridContextPolicy.MaximumCompactionAttempts"/>.
/// </para>
/// </remarks>
internal sealed class HarnessContextAssembler
{
    private readonly HarnessHybridContextPolicy _policy;
    private readonly IHarnessContextSnapshotProvider _snapshotProvider;
    private readonly IHarnessContextReducer _reducer;

    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    internal HarnessContextAssembler(
        HarnessHybridContextPolicy policy,
        IHarnessContextSnapshotProvider snapshotProvider,
        IHarnessContextReducer reducer)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(snapshotProvider);
        ArgumentNullException.ThrowIfNull(reducer);

        _policy = policy;
        _snapshotProvider = snapshotProvider;
        _reducer = reducer;
    }

    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
    /// <exception cref="ArgumentException">
    /// The captured snapshot's entries contain two entries sharing the same
    /// <see cref="HarnessContextEntry.EntryId"/>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The configured <see cref="IHarnessContextReducer"/> returned <see langword="null"/> from
    /// <see cref="IHarnessContextReducer.ReduceAsync"/>. Reducer implementations must return a non-null
    /// entries list; throw instead of returning a sentinel or <see langword="null"/>.
    /// </exception>
    internal async Task<HarnessContextAssemblyResult> AssembleAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var stages = new List<HarnessContextAssemblyStage>();

        // First snapshot — its size is the immutable OriginalEstimatedSize anchor for the entire
        // assembly, preserved across restarts so callers always know how much was in the context
        // before any pressure handling started.
        var firstSnapshot = _snapshotProvider.CaptureSnapshot();
        stages.Add(HarnessContextAssemblyStage.SnapshotCaptured);

        var initialEstimatedSize = _policy.Evaluate(firstSnapshot.Entries, cancellationToken).EstimatedSize;
        var latestVersion = firstSnapshot.Version;
        var currentOriginalEntries = firstSnapshot.Entries;
        var attempts = 0;

        // Outer loop: each version-change restart consumes one attempt from the bounded budget,
        // whether the restart originated in the post-reducer recheck or in the finalization
        // capture that guards every success return.
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var evaluation = _policy.Evaluate(currentOriginalEntries, cancellationToken);

            // ── STRICTLY BELOW TRIGGER THRESHOLD ─────────────────────────────────────────────
            // No eviction, no reducer. Verify the entries as-is: an invalid tool sequence/state
            // is Irreducible even when the size is comfortably under the hard limit.
            if (!evaluation.Triggered)
            {
                var withinLimitVerification = HarnessCompactionVerifier.Verify(
                    currentOriginalEntries, currentOriginalEntries, _policy, cancellationToken);

                if (!withinLimitVerification.IsAccepted)
                {
                    // Structurally invalid context (e.g. orphaned tool call) cannot be forwarded.
                    var requiredIdsBelowTriggerInvalid = _policy
                        .SelectRequiredPreservation(currentOriginalEntries, cancellationToken)
                        .RequiredEntryIds;
                    return HarnessContextAssemblyResult.Terminated(
                        HarnessContextAssemblyOutcome.Irreducible,
                        initialEstimatedSize,
                        evaluation.EstimatedSize,
                        _policy.HardLimit,
                        attempts,
                        stages,
                        requiredIdsBelowTriggerInvalid,
                        latestVersion);
                }

                // Finalization version check: capture once more before committing to success so
                // that an entry injected while we were verifying is neither lost nor duplicated.
                var finalizationSnapshotBelowTrigger = _snapshotProvider.CaptureSnapshot();
                stages.Add(HarnessContextAssemblyStage.SnapshotCaptured);

                var belowTriggerCheck = CheckFinalizationVersion(
                    finalizationSnapshotBelowTrigger,
                    ref latestVersion,
                    ref currentOriginalEntries,
                    ref attempts,
                    consumesAttempt: true,
                    stages);

                if (belowTriggerCheck == FinalizationCheckOutcome.ChurnExhausted)
                {
                    return BuildChurnTerminationResult(
                        finalizationSnapshotBelowTrigger.Entries,
                        initialEstimatedSize,
                        attempts,
                        stages,
                        finalizationSnapshotBelowTrigger.Version,
                        cancellationToken);
                }

                if (belowTriggerCheck == FinalizationCheckOutcome.Restarted)
                {
                    continue; // restart: re-evaluate trigger on the new snapshot
                }

                // Version stable — return the original entries unchanged.
                var requiredIdsBelowTrigger = _policy
                    .SelectRequiredPreservation(currentOriginalEntries, cancellationToken)
                    .RequiredEntryIds;
                return HarnessContextAssemblyResult.Success(
                    HarnessContextAssemblyOutcome.WithinLimit,
                    currentOriginalEntries,
                    initialEstimatedSize,
                    evaluation.EstimatedSize,
                    _policy.HardLimit,
                    attempts,
                    stages,
                    requiredIdsBelowTrigger,
                    latestVersion,
                    withinLimitVerification);
            }

            // ── AT OR ABOVE TRIGGER THRESHOLD ────────────────────────────────────────────────
            // Start assembly pressure handling: evict recoverable bodies first — but only those
            // that have a durable ArtifactReference entry with the same canonical digest in this
            // same snapshot. HardLimit remains the dispatch eligibility boundary throughout.
            var evictedEntries = EvictRecoverableBodies(currentOriginalEntries, stages);
            var evictedSize = _policy.Evaluate(evictedEntries, cancellationToken).EstimatedSize;
            var evictionOccurred = evictedEntries.Count != currentOriginalEntries.Count;
            var triggerThreshold = _policy.HardLimit - _policy.TriggerMargin;

            // The one explicit margin-restored rule: eviction must have actually happened AND
            // brought the size strictly below the trigger threshold. Anything else — no body was
            // evicted, or the evicted size remains at or above the trigger — always proceeds to
            // the reducer loop below, even when the current size already fits the hard limit;
            // reaching the trigger must always cause an actual pressure-handling attempt.
            var marginRestored = evictionOccurred && evictedSize < triggerThreshold;

            if (marginRestored)
            {
                var evictionVerification = HarnessCompactionVerifier.Verify(
                    currentOriginalEntries, evictedEntries, _policy, cancellationToken);

                if (evictionVerification.IsAccepted)
                {
                    // Finalization version check before committing to success.
                    var finalizationSnapshotEviction = _snapshotProvider.CaptureSnapshot();
                    stages.Add(HarnessContextAssemblyStage.SnapshotCaptured);

                    var evictionCheck = CheckFinalizationVersion(
                        finalizationSnapshotEviction,
                        ref latestVersion,
                        ref currentOriginalEntries,
                        ref attempts,
                        consumesAttempt: true,
                        stages);

                    if (evictionCheck == FinalizationCheckOutcome.ChurnExhausted)
                    {
                        return BuildChurnTerminationResult(
                            finalizationSnapshotEviction.Entries,
                            initialEstimatedSize,
                            attempts,
                            stages,
                            finalizationSnapshotEviction.Version,
                            cancellationToken);
                    }

                    if (evictionCheck == FinalizationCheckOutcome.Restarted)
                    {
                        continue; // restart: re-evaluate trigger on the new snapshot
                    }

                    var requiredIdsEviction = _policy
                        .SelectRequiredPreservation(currentOriginalEntries, cancellationToken)
                        .RequiredEntryIds;
                    return HarnessContextAssemblyResult.Success(
                        HarnessContextAssemblyOutcome.Reduced,
                        evictedEntries,
                        initialEstimatedSize,
                        evictedSize,
                        _policy.HardLimit,
                        attempts,
                        stages,
                        requiredIdsEviction,
                        latestVersion,
                        evictionVerification);
                }

                // Eviction verification was rejected (e.g. a body with no reference was preserved
                // and the entries still contain an invalid sequence): fall through to the reducer
                // loop so it can attempt to repair the invalid sequence. Verification will reject
                // every proposal that fails the same check, eventually terminating as Irreducible.
            }

            // ── BOUNDED REDUCER LOOP ──────────────────────────────────────────────────────────
            var currentEntries = evictedEntries;
            var currentSize = evictedSize;
            var versionChangedInReducerLoop = false;
            HarnessContextAssemblyResult? churnTerminationInReducerLoop = null;

            while (attempts < _policy.MaximumCompactionAttempts)
            {
                attempts++;
                cancellationToken.ThrowIfCancellationRequested();

                // Record one stage entry for each reducer invocation so the reduction path can
                // be audited without inspecting entry content.
                stages.Add(HarnessContextAssemblyStage.ReducerAttempt);

                var requestRequiredIds = _policy
                    .SelectRequiredPreservation(currentEntries, cancellationToken)
                    .RequiredEntryIds;
                var request = HarnessContextReductionRequest.Create(
                    currentEntries, requestRequiredIds, _policy, attempts);
                var proposed = await _reducer.ReduceAsync(request, cancellationToken).ConfigureAwait(false);

                if (proposed is null)
                {
                    throw new InvalidOperationException(
                        $"The configured {nameof(IHarnessContextReducer)} returned null from " +
                        $"{nameof(IHarnessContextReducer.ReduceAsync)}. Reducer implementations " +
                        "must return a non-null entries list; throw instead of returning a sentinel " +
                        "or null value so the assembler can distinguish a deliberate reduction from " +
                        "a swallowed failure.");
                }

                cancellationToken.ThrowIfCancellationRequested();

                // Post-reducer version recheck: discard the in-flight proposal immediately if a
                // new entry was injected while the reducer was running — never merge, never patch.
                // This restart does not itself consume a further attempt: the invocation above
                // already consumed this iteration's attempt.
                var freshSnapshot = _snapshotProvider.CaptureSnapshot();
                stages.Add(HarnessContextAssemblyStage.SnapshotCaptured);

                var postReducerCheck = CheckFinalizationVersion(
                    freshSnapshot,
                    ref latestVersion,
                    ref currentOriginalEntries,
                    ref attempts,
                    consumesAttempt: false,
                    stages);

                if (postReducerCheck == FinalizationCheckOutcome.ChurnExhausted)
                {
                    churnTerminationInReducerLoop = BuildChurnTerminationResult(
                        freshSnapshot.Entries, initialEstimatedSize, attempts, stages,
                        freshSnapshot.Version, cancellationToken);
                    break;
                }

                if (postReducerCheck == FinalizationCheckOutcome.Restarted)
                {
                    versionChangedInReducerLoop = true;
                    break;
                }

                cancellationToken.ThrowIfCancellationRequested();

                var proposalVerification = HarnessCompactionVerifier.Verify(
                    currentOriginalEntries, proposed, _policy, cancellationToken);
                var proposedSize = _policy.Evaluate(proposed, cancellationToken).EstimatedSize;

                if (!proposalVerification.IsAccepted || proposedSize >= currentSize)
                {
                    // Invalid or non-progressing proposal: never forwarded. The reducer's failure
                    // to improve the entries is not itself a termination — if the current
                    // (pre-attempt) entries already fit the hard limit, preserve them as a
                    // successful result now that this attempt has been recorded. Only when they
                    // still exceed the hard limit does this fall through to the deterministic
                    // fallback below.
                    if (currentSize <= _policy.HardLimit)
                    {
                        var preserveVerification = HarnessCompactionVerifier.Verify(
                            currentOriginalEntries, currentEntries, _policy, cancellationToken);

                        if (preserveVerification.IsAccepted)
                        {
                            var finalizationSnapshotPreserved = _snapshotProvider.CaptureSnapshot();
                            stages.Add(HarnessContextAssemblyStage.SnapshotCaptured);

                            var preserveCheck = CheckFinalizationVersion(
                                finalizationSnapshotPreserved,
                                ref latestVersion,
                                ref currentOriginalEntries,
                                ref attempts,
                                consumesAttempt: false,
                                stages);

                            if (preserveCheck == FinalizationCheckOutcome.ChurnExhausted)
                            {
                                churnTerminationInReducerLoop = BuildChurnTerminationResult(
                                    finalizationSnapshotPreserved.Entries, initialEstimatedSize, attempts,
                                    stages, finalizationSnapshotPreserved.Version, cancellationToken);
                                break;
                            }

                            if (preserveCheck == FinalizationCheckOutcome.Restarted)
                            {
                                versionChangedInReducerLoop = true;
                                break;
                            }

                            var requiredIdsPreserved = _policy
                                .SelectRequiredPreservation(currentOriginalEntries, cancellationToken)
                                .RequiredEntryIds;
                            var outcomePreserved = currentEntries.Count == currentOriginalEntries.Count
                                ? HarnessContextAssemblyOutcome.WithinLimit
                                : HarnessContextAssemblyOutcome.Reduced;
                            return HarnessContextAssemblyResult.Success(
                                outcomePreserved,
                                currentEntries,
                                initialEstimatedSize,
                                currentSize,
                                _policy.HardLimit,
                                attempts,
                                stages,
                                requiredIdsPreserved,
                                latestVersion,
                                preserveVerification);
                        }
                    }

                    break;
                }

                if (proposedSize <= _policy.HardLimit)
                {
                    // Finalization version check: a final injection during the window between the
                    // post-reducer recheck and the success return must not be silently dropped.
                    var finalizationSnapshotReduced = _snapshotProvider.CaptureSnapshot();
                    stages.Add(HarnessContextAssemblyStage.SnapshotCaptured);

                    var reducedCheck = CheckFinalizationVersion(
                        finalizationSnapshotReduced,
                        ref latestVersion,
                        ref currentOriginalEntries,
                        ref attempts,
                        consumesAttempt: false,
                        stages);

                    if (reducedCheck == FinalizationCheckOutcome.ChurnExhausted)
                    {
                        churnTerminationInReducerLoop = BuildChurnTerminationResult(
                            finalizationSnapshotReduced.Entries, initialEstimatedSize, attempts, stages,
                            finalizationSnapshotReduced.Version, cancellationToken);
                        break;
                    }

                    if (reducedCheck == FinalizationCheckOutcome.Restarted)
                    {
                        versionChangedInReducerLoop = true;
                        break;
                    }

                    var requiredIdsReduced = _policy
                        .SelectRequiredPreservation(currentOriginalEntries, cancellationToken)
                        .RequiredEntryIds;
                    return HarnessContextAssemblyResult.Success(
                        HarnessContextAssemblyOutcome.Reduced,
                        proposed,
                        initialEstimatedSize,
                        proposedSize,
                        _policy.HardLimit,
                        attempts,
                        stages,
                        requiredIdsReduced,
                        latestVersion,
                        proposalVerification);
                }

                currentEntries = [.. proposed];
                currentSize = proposedSize;
            }

            if (churnTerminationInReducerLoop is not null)
            {
                return churnTerminationInReducerLoop;
            }

            if (versionChangedInReducerLoop)
            {
                continue; // restart outer loop from the new snapshot
            }

            return BuildFallbackResult(
                currentOriginalEntries, initialEstimatedSize, attempts, stages,
                latestVersion, cancellationToken);
        }
    }

    private enum FinalizationCheckOutcome
    {
        /// <summary>No version change was observed; the caller may proceed with its candidate.</summary>
        Stable,

        /// <summary>
        /// A version change was observed and the bounded attempt budget was not yet exhausted: the
        /// restart was consumed (per <c>consumesAttempt</c>) and the caller must restart from the
        /// newly observed snapshot.
        /// </summary>
        Restarted,

        /// <summary>
        /// A version change was observed but the bounded attempt budget was already exhausted before
        /// this restart could be consumed: unstable, perpetual churn. The caller must terminate
        /// directly as <see cref="HarnessContextAssemblyOutcome.ConcurrentMutationLimit"/> against the
        /// observed snapshot's evidence rather than attempting any further work.
        /// </summary>
        ChurnExhausted,
    }

    /// <summary>
    /// Single shared helper for every finalization/restart version recheck this type performs — the
    /// below-trigger, post-eviction, post-reducer, preserved-after-non-reducing-reducer, reduced, and
    /// deterministic-fallback checkpoints all funnel through here so every success path is checked
    /// consistently. Compares <paramref name="observedSnapshot"/>'s version against
    /// <paramref name="latestVersion"/>: unchanged is <see cref="FinalizationCheckOutcome.Stable"/>. On
    /// a change, the bounded attempt budget is checked <em>before</em> the restart is consumed — if
    /// already exhausted, returns <see cref="FinalizationCheckOutcome.ChurnExhausted"/> without
    /// mutating any ref parameter, so the caller can terminate directly against
    /// <paramref name="observedSnapshot"/>'s evidence; otherwise the restart is recorded (and, when
    /// <paramref name="consumesAttempt"/> is <see langword="true"/>, one attempt of the budget is
    /// consumed) and <see cref="FinalizationCheckOutcome.Restarted"/> is returned.
    /// </summary>
    private FinalizationCheckOutcome CheckFinalizationVersion(
        HarnessContextSnapshot observedSnapshot,
        ref long latestVersion,
        ref IReadOnlyList<HarnessContextEntry> currentOriginalEntries,
        ref int attempts,
        bool consumesAttempt,
        List<HarnessContextAssemblyStage> stages)
    {
        if (observedSnapshot.Version == latestVersion)
        {
            return FinalizationCheckOutcome.Stable;
        }

        if (attempts >= _policy.MaximumCompactionAttempts)
        {
            return FinalizationCheckOutcome.ChurnExhausted;
        }

        stages.Add(HarnessContextAssemblyStage.RestartedAfterMutation);
        latestVersion = observedSnapshot.Version;
        currentOriginalEntries = observedSnapshot.Entries;

        if (consumesAttempt)
        {
            attempts++;
        }

        return FinalizationCheckOutcome.Restarted;
    }

    /// <summary>
    /// Builds the direct <see cref="HarnessContextAssemblyOutcome.ConcurrentMutationLimit"/> termination
    /// for unstable, perpetual churn: the bounded attempt budget was exhausted before a detected restart
    /// could be consumed. Evaluated against <paramref name="latestEntries"/> — the newest observed
    /// snapshot — never against a stale candidate, and never routed through the deterministic fallback,
    /// which could otherwise return a stale success computed against content churn has already
    /// superseded.
    /// </summary>
    private HarnessContextAssemblyResult BuildChurnTerminationResult(
        IReadOnlyList<HarnessContextEntry> latestEntries,
        int initialEstimatedSize,
        int attempts,
        List<HarnessContextAssemblyStage> stages,
        long latestVersion,
        CancellationToken cancellationToken)
    {
        var latestSize = _policy.Evaluate(latestEntries, cancellationToken).EstimatedSize;
        var requiredIds = _policy.SelectRequiredPreservation(latestEntries, cancellationToken).RequiredEntryIds;
        return HarnessContextAssemblyResult.Terminated(
            HarnessContextAssemblyOutcome.ConcurrentMutationLimit,
            initialEstimatedSize,
            latestSize,
            _policy.HardLimit,
            attempts,
            stages,
            requiredIds,
            latestVersion);
    }

    /// <summary>
    /// The deterministic fallback: the verifier's preservation-only candidate, first extended with any
    /// retained <see cref="HarnessContextEntryKind.OptionalContext"/> entries and, only if that still does
    /// not fit, required-only — both computed from <paramref name="originalEntries"/> (the true anchor
    /// since the last observed snapshot), in original order. Returns a distinct structured termination if
    /// neither candidate fits or independently re-verifies. Every success candidate here is guarded by
    /// the same shared <see cref="CheckFinalizationVersion"/> checkpoint as every other success path in
    /// this type: an injection observed while building the fallback restarts this method deterministically
    /// against the newest snapshot (consuming one attempt), or — if the bounded budget is already
    /// exhausted — terminates directly as <see cref="HarnessContextAssemblyOutcome.ConcurrentMutationLimit"/>
    /// rather than ever returning a stale success computed against superseded content. If this method
    /// instead reaches its own bottom — neither candidate fits or verifies, with no restart pending at
    /// that instant — the outcome is always <see cref="HarnessContextAssemblyOutcome.Irreducible"/>: a
    /// prior restart earlier in the assembly does not carry forward into this decision.
    /// <see cref="HarnessContextAssemblyOutcome.ConcurrentMutationLimit"/> is reserved exclusively for the
    /// direct churn path in <see cref="BuildChurnTerminationResult"/>, where a version change was observed
    /// but the bounded attempt budget was already exhausted before the restart could be consumed.
    /// </summary>
    private HarnessContextAssemblyResult BuildFallbackResult(
        IReadOnlyList<HarnessContextEntry> originalEntries,
        int initialEstimatedSize,
        int attemptCount,
        List<HarnessContextAssemblyStage> stages,
        long latestVersion,
        CancellationToken cancellationToken)
    {
        stages.Add(HarnessContextAssemblyStage.DeterministicFallback);

        var attempts = attemptCount;
        var currentOriginalEntries = originalEntries;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var requiredSelection = _policy.SelectRequiredPreservation(currentOriginalEntries, cancellationToken);
            var requiredEntryIds = requiredSelection.RequiredEntryIds;
            var requiredIdSet = new HashSet<string>(requiredEntryIds, StringComparer.Ordinal);

            var extendedIdSet = new HashSet<string>(requiredIdSet, StringComparer.Ordinal);
            foreach (var entry in currentOriginalEntries)
            {
                if (entry.Kind == HarnessContextEntryKind.OptionalContext)
                {
                    extendedIdSet.Add(entry.EntryId);
                }
            }

            if (extendedIdSet.Count > requiredIdSet.Count)
            {
                var extendedCandidate = BuildCandidate(currentOriginalEntries, extendedIdSet);
                var extendedSize = _policy.Evaluate(extendedCandidate, cancellationToken).EstimatedSize;

                if (extendedSize <= _policy.HardLimit)
                {
                    var extendedVerification = HarnessCompactionVerifier.Verify(
                        currentOriginalEntries, extendedCandidate, _policy, cancellationToken);
                    if (extendedVerification.IsAccepted)
                    {
                        var finalizationSnapshotExtended = _snapshotProvider.CaptureSnapshot();
                        stages.Add(HarnessContextAssemblyStage.SnapshotCaptured);

                        var extendedCheck = CheckFinalizationVersion(
                            finalizationSnapshotExtended,
                            ref latestVersion,
                            ref currentOriginalEntries,
                            ref attempts,
                            consumesAttempt: true,
                            stages);

                        if (extendedCheck == FinalizationCheckOutcome.ChurnExhausted)
                        {
                            return BuildChurnTerminationResult(
                                finalizationSnapshotExtended.Entries, initialEstimatedSize, attempts,
                                stages, finalizationSnapshotExtended.Version, cancellationToken);
                        }

                        if (extendedCheck == FinalizationCheckOutcome.Restarted)
                        {
                            continue; // restart: recompute candidates from the newest snapshot
                        }

                        return HarnessContextAssemblyResult.Success(
                            HarnessContextAssemblyOutcome.PreservationFallback,
                            extendedCandidate,
                            initialEstimatedSize,
                            extendedSize,
                            _policy.HardLimit,
                            attempts,
                            stages,
                            requiredEntryIds,
                            latestVersion,
                            extendedVerification);
                    }
                }
            }

            var requiredCandidate = BuildCandidate(currentOriginalEntries, requiredIdSet);
            var requiredSize = _policy.Evaluate(requiredCandidate, cancellationToken).EstimatedSize;

            if (requiredSize <= _policy.HardLimit)
            {
                var requiredVerification = HarnessCompactionVerifier.Verify(
                    currentOriginalEntries, requiredCandidate, _policy, cancellationToken);
                if (requiredVerification.IsAccepted)
                {
                    var finalizationSnapshotRequired = _snapshotProvider.CaptureSnapshot();
                    stages.Add(HarnessContextAssemblyStage.SnapshotCaptured);

                    var requiredCheck = CheckFinalizationVersion(
                        finalizationSnapshotRequired,
                        ref latestVersion,
                        ref currentOriginalEntries,
                        ref attempts,
                        consumesAttempt: true,
                        stages);

                    if (requiredCheck == FinalizationCheckOutcome.ChurnExhausted)
                    {
                        return BuildChurnTerminationResult(
                            finalizationSnapshotRequired.Entries, initialEstimatedSize, attempts,
                            stages, finalizationSnapshotRequired.Version, cancellationToken);
                    }

                    if (requiredCheck == FinalizationCheckOutcome.Restarted)
                    {
                        continue; // restart: recompute candidates from the newest snapshot
                    }

                    return HarnessContextAssemblyResult.Success(
                        HarnessContextAssemblyOutcome.PreservationFallback,
                        requiredCandidate,
                        initialEstimatedSize,
                        requiredSize,
                        _policy.HardLimit,
                        attempts,
                        stages,
                        requiredEntryIds,
                        latestVersion,
                        requiredVerification);
                }
            }

            cancellationToken.ThrowIfCancellationRequested();

            // Required (and, where retained, optional) content alone still exceeds the hard limit even
            // after this deterministic fallback. This is a distinct, direct evidence of irreducibility —
            // never ConcurrentMutationLimit here, even if an earlier restart occurred elsewhere in this
            // assembly: ConcurrentMutationLimit is reserved exclusively for the direct churn path (see
            // BuildChurnTerminationResult), where a version change was observed but the bounded attempt
            // budget was already exhausted before the restart could be consumed.
            return HarnessContextAssemblyResult.Terminated(
                HarnessContextAssemblyOutcome.Irreducible,
                initialEstimatedSize,
                requiredSize,
                _policy.HardLimit,
                attempts,
                stages,
                requiredEntryIds,
                latestVersion);
        }
    }

    /// <summary>
    /// Every entry in <paramref name="entries"/> whose id is in <paramref name="entryIds"/>, in
    /// <paramref name="entries"/>'s original relative order.
    /// </summary>
    private static List<HarnessContextEntry> BuildCandidate(
        IReadOnlyList<HarnessContextEntry> entries, HashSet<string> entryIds)
    {
        var candidate = new List<HarnessContextEntry>(entryIds.Count);
        foreach (var entry in entries)
        {
            if (entryIds.Contains(entry.EntryId))
            {
                candidate.Add(entry);
            }
        }

        return candidate;
    }

    /// <summary>
    /// Every entry in <paramref name="entries"/> except <see cref="HarnessContextEntryKind.RecoverableContextSegment"/>
    /// bodies that have a durable reference with the same canonical digest in the same snapshot — either
    /// a standalone <see cref="HarnessContextEntryKind.ArtifactReference"/> entry, or a
    /// <see cref="HarnessContextEntryKind.ToolExchange"/> entry whose result payload structurally carries
    /// the reference. A recoverable body whose digest has no matching durable reference is kept as-is:
    /// silently discarding it would lose content for which no independently preservable reference
    /// pointer exists.
    /// Records a <see cref="HarnessContextAssemblyStage.RecoverableBodyEviction"/> stage only when at
    /// least one body was actually evicted.
    /// </summary>
    private static List<HarnessContextEntry> EvictRecoverableBodies(
        IReadOnlyList<HarnessContextEntry> entries, List<HarnessContextAssemblyStage> stages)
    {
        // Collect every canonical digest backed by a durable reference so the eviction loop below can
        // confirm a reference exists before removing the corresponding body.
        var durableReferenceDigests = HarnessContextEntry.CollectDurableArtifactReferenceDigests(entries);

        var evictedAny = false;
        var result = new List<HarnessContextEntry>(entries.Count);

        foreach (var entry in entries)
        {
            if (entry.Kind == HarnessContextEntryKind.RecoverableContextSegment
                && entry.ArtifactReferenceDigest is not null
                && durableReferenceDigests.Contains(entry.ArtifactReferenceDigest))
            {
                // Durable reference entry exists with matching digest: safe to evict the body
                // because the reference pointer alone is sufficient for later re-retrieval.
                evictedAny = true;
                continue;
            }

            result.Add(entry);
        }

        if (evictedAny)
        {
            stages.Add(HarnessContextAssemblyStage.RecoverableBodyEviction);
        }

        return result;
    }
}
