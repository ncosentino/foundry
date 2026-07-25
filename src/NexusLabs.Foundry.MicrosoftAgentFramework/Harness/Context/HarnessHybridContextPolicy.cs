using Microsoft.Extensions.AI;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Context;

/// <summary>
/// Explicit, experimental hybrid compaction policy: an integer hard limit, a trigger margin measured
/// in the same units, a recent-message retention count, a maximum compaction attempt bound, and a
/// preservation scheme label/version. Every value is required and independently validated — this
/// policy has no hidden default and cannot be constructed with an implicit or optional value for any
/// of them.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Trigger margin.</strong> Compaction is requested once the estimated context size reaches
/// <c>HardLimit - TriggerMargin</c> (<see cref="HarnessCompactionTriggerEvaluation.TriggerThreshold"/>):
/// strictly below that threshold never triggers, and exactly at or above it always does.
/// </para>
/// <para>
/// <strong>Required preservation set.</strong> <see cref="SelectRequiredPreservation"/> always
/// requires every <see cref="HarnessContextEntryKind.SystemInstruction"/>,
/// <see cref="HarnessContextEntryKind.AuthoritativeSessionState"/>,
/// <see cref="HarnessContextEntryKind.ApprovalSecurityState"/>, and
/// <see cref="HarnessContextEntryKind.ArtifactReference"/> entry, plus the trailing
/// <see cref="RecentMessageRetentionCount"/> "recency units" of the remaining entries. A recency unit
/// is either one <see cref="HarnessContextEntryKind.ConversationalMessage"/> entry or one complete
/// <see cref="HarnessToolExchangeGroup"/> — counting a whole tool exchange as a single unit is the one
/// documented rule that guarantees the retention boundary can never split a required tool exchange.
/// <see cref="HarnessContextEntryKind.Summary"/> entries are never required.
/// </para>
/// <para>
/// <strong>Reference-less recoverable segments are required, not merely retained.</strong> A
/// <see cref="HarnessContextEntryKind.RecoverableContextSegment"/> is also required whenever this
/// same <c>originalEntries</c> set does not contain a <see cref="HarnessContextEntryKind.ArtifactReference"/>
/// entry sharing its canonical digest: with no independently preservable reference pointer, the
/// rehydrated body itself is the only durable copy of that content, so it must never be treated as
/// opportunistically droppable. This is independent of — and evaluated in addition to —
/// <see cref="HarnessContextAssembler"/>'s own eviction-eligibility check, so both
/// <see cref="HarnessCompactionVerifier"/> and the deterministic fallback also honor it. A recoverable
/// segment backed by a matching digest reference remains unrequired and evictable as before.
/// </para>
/// <para>
/// <strong>A reference-bearing complete tool exchange is required atomically.</strong> When a complete
/// <see cref="HarnessToolExchangeGroup"/>'s result entry structurally carries a canonical artifact
/// reference in its <see cref="FunctionResultContent.Result"/> payload (see
/// <see cref="HarnessContextEntry.ArtifactReferenceDigests"/>), <see cref="SelectRequiredPreservation"/>
/// requires every entry id in that group — its call entry and every result entry, never the result alone
/// — regardless of the trailing recency window. The correlated call can never be silently dropped out
/// from under a preserved, reference-bearing result.
/// </para>
/// <para>
/// <strong>Incomplete tool exchanges are never silently reducible.</strong> Independent of the
/// recency-unit retention boundary above, <see cref="SelectRequiredPreservation"/> always requires
/// every entry <see cref="HarnessToolExchangeAnalysis"/> flags as part of an incomplete exchange: an
/// orphaned call, an orphaned result, a duplicated call or result id, or a call whose result was
/// reordered ahead of it. This holds even when every entry in the offending group falls entirely
/// outside the trailing retention window — an old, still-unmatched exchange can never simply age out
/// of the required set. Because <see cref="HarnessCompactionVerifier"/> re-checks the same structural
/// consistency on the proposed entries, an exchange that is irreparably broken in the original entries
/// (for example an orphaned call whose result never existed at all) stays required yet can never pass
/// verification either way: dropping the required entry rejects with
/// <see cref="HarnessCompactionRejectionReason.MissingRequiredEntry"/>, and preserving it still rejects
/// with <see cref="HarnessCompactionRejectionReason.OrphanedToolCall"/> (or the matching orphan/duplicate/
/// reorder reason) — an irreducible termination by design, rather than a silent drop of the broken
/// exchange.
/// </para>
/// </remarks>
internal sealed class HarnessHybridContextPolicy
{
    private readonly IHarnessContextSizeEstimator _sizeEstimator;

    private HarnessHybridContextPolicy(
        int hardLimit,
        int triggerMargin,
        int recentMessageRetentionCount,
        int maximumCompactionAttempts,
        string preservationLabel,
        int preservationVersion,
        IHarnessContextSizeEstimator sizeEstimator)
    {
        HardLimit = hardLimit;
        TriggerMargin = triggerMargin;
        RecentMessageRetentionCount = recentMessageRetentionCount;
        MaximumCompactionAttempts = maximumCompactionAttempts;
        PreservationLabel = preservationLabel;
        PreservationVersion = preservationVersion;
        _sizeEstimator = sizeEstimator;
    }

    /// <summary>The required, positive hard limit, in the configured estimator's units.</summary>
    internal int HardLimit { get; }

    /// <summary>The required, positive compaction execution safety margin.</summary>
    internal int TriggerMargin { get; }

    /// <summary>
    /// The trigger threshold (<see cref="HardLimit"/> minus <see cref="TriggerMargin"/>), in the
    /// configured estimator's units. An estimated size at or above this threshold triggers
    /// compaction evaluation.
    /// </summary>
    internal int TriggerThreshold => HardLimit - TriggerMargin;

    /// <summary>The required, positive number of trailing recency units to always retain.</summary>
    internal int RecentMessageRetentionCount { get; }

    /// <summary>
    /// The required, positive bound on how many compaction attempts a future bounded recompaction
    /// loop (owned elsewhere) may perform. Validated and stored here; this policy does not itself run
    /// a retry loop.
    /// </summary>
    internal int MaximumCompactionAttempts { get; }

    /// <summary>The required, non-empty label identifying this preservation scheme.</summary>
    internal string PreservationLabel { get; }

    /// <summary>The required, positive version of this preservation scheme.</summary>
    internal int PreservationVersion { get; }

    /// <summary>
    /// The configured size estimator this policy evaluates every entry with. Exposed so a caller
    /// building diagnostics from an assembly result can compute per-category contributions using the
    /// exact same estimator instance that governed this policy's own <see cref="Evaluate"/> decision,
    /// so the two are guaranteed to agree.
    /// </summary>
    internal IHarnessContextSizeEstimator SizeEstimator => _sizeEstimator;

    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="hardLimit"/>, <paramref name="recentMessageRetentionCount"/>,
    /// <paramref name="maximumCompactionAttempts"/>, or <paramref name="preservationVersion"/> is not
    /// greater than zero; or <paramref name="triggerMargin"/> is not a positive value strictly less
    /// than <paramref name="hardLimit"/>.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="preservationLabel"/> or <paramref name="sizeEstimator"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="preservationLabel"/> is empty or whitespace-only.
    /// </exception>
    internal static HarnessHybridContextPolicy Create(
        int hardLimit,
        int triggerMargin,
        int recentMessageRetentionCount,
        int maximumCompactionAttempts,
        string preservationLabel,
        int preservationVersion,
        IHarnessContextSizeEstimator sizeEstimator)
    {
        if (hardLimit <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(hardLimit), hardLimit, "The hard limit must be a positive integer size.");
        }

        if (triggerMargin <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(triggerMargin), triggerMargin, "The trigger margin must be a positive integer size.");
        }

        if (triggerMargin >= hardLimit)
        {
            throw new ArgumentOutOfRangeException(
                nameof(triggerMargin),
                triggerMargin,
                "The trigger margin must be strictly less than the hard limit so a positive trigger threshold exists.");
        }

        if (recentMessageRetentionCount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(recentMessageRetentionCount),
                recentMessageRetentionCount,
                "The recent-message retention count must be a positive integer.");
        }

        if (maximumCompactionAttempts <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumCompactionAttempts),
                maximumCompactionAttempts,
                "The maximum compaction attempt bound must be a positive integer.");
        }

        ArgumentNullException.ThrowIfNull(preservationLabel);
        if (string.IsNullOrWhiteSpace(preservationLabel))
        {
            throw new ArgumentException(
                "A non-empty, non-whitespace preservation label is required.", nameof(preservationLabel));
        }

        if (preservationVersion <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(preservationVersion), preservationVersion, "The preservation version must be a positive integer.");
        }

        ArgumentNullException.ThrowIfNull(sizeEstimator);

        return new HarnessHybridContextPolicy(
            hardLimit,
            triggerMargin,
            recentMessageRetentionCount,
            maximumCompactionAttempts,
            preservationLabel,
            preservationVersion,
            sizeEstimator);
    }

    /// <summary>
    /// Sums this policy's configured <see cref="IHarnessContextSizeEstimator"/> over
    /// <paramref name="entries"/> and evaluates the result against <see cref="HardLimit"/> and
    /// <see cref="TriggerMargin"/>.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="entries"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">
    /// The configured <see cref="IHarnessContextSizeEstimator"/> returned a negative value for one of
    /// the entries. Size estimators must return a non-negative integer for every entry.
    /// </exception>
    /// <exception cref="OverflowException">
    /// The accumulated estimated size exceeds <see cref="int.MaxValue"/>. Use a smaller entry set or
    /// an estimator with a bounded unit range.
    /// </exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
    internal HarnessCompactionTriggerEvaluation Evaluate(
        IReadOnlyList<HarnessContextEntry> entries, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entries);
        cancellationToken.ThrowIfCancellationRequested();

        var estimatedSize = 0;
        foreach (var entry in entries)
        {
            var size = _sizeEstimator.EstimateSize(entry);
            if (size < 0)
            {
                throw new InvalidOperationException(
                    $"The configured size estimator returned a negative value ({size}) for entry " +
                    $"'{entry.EntryId}'. Size estimators must return a non-negative integer for every entry.");
            }

            checked
            {
                estimatedSize += size;
            }
        }

        cancellationToken.ThrowIfCancellationRequested();

        var threshold = TriggerThreshold;
        return HarnessCompactionTriggerEvaluation.Create(estimatedSize, HardLimit, TriggerMargin, estimatedSize >= threshold);
    }

    /// <summary>
    /// Deterministically selects every entry id an upstream reducer must preserve from
    /// <paramref name="originalEntries"/>, per this type's documented preservation and recency-unit
    /// rules.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="originalEntries"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="originalEntries"/> contains two entries sharing the same
    /// <see cref="HarnessContextEntry.EntryId"/>.
    /// </exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
    internal HarnessPreservationSelection SelectRequiredPreservation(
        IReadOnlyList<HarnessContextEntry> originalEntries, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(originalEntries);
        EnsureUniqueEntryIds(originalEntries);
        cancellationToken.ThrowIfCancellationRequested();

        var analysis = HarnessToolExchangeAnalysis.Build(originalEntries);
        cancellationToken.ThrowIfCancellationRequested();

        var entriesById = new Dictionary<string, HarnessContextEntry>(StringComparer.Ordinal);
        foreach (var entry in originalEntries)
        {
            entriesById[entry.EntryId] = entry;
        }

        var requiredIds = new List<string>();
        var requiredIdSet = new HashSet<string>(StringComparer.Ordinal);

        void Require(string entryId)
        {
            if (requiredIdSet.Add(entryId))
            {
                requiredIds.Add(entryId);
            }
        }

        // Every canonical digest backed by a durable ArtifactReference entry, or by a ToolExchange
        // result entry whose payload structurally carries a canonical reference, in this same snapshot.
        // A RecoverableContextSegment whose digest is not in this set has no independently preservable
        // reference pointer, so it must be required rather than merely retained opportunistically —
        // dropping it would lose content no other entry can reconstruct.
        var durableReferenceDigests = HarnessContextEntry.CollectDurableArtifactReferenceDigests(originalEntries);

        foreach (var entry in originalEntries)
        {
            if (entry.Kind is HarnessContextEntryKind.SystemInstruction
                or HarnessContextEntryKind.AuthoritativeSessionState
                or HarnessContextEntryKind.ApprovalSecurityState
                or HarnessContextEntryKind.ArtifactReference)
            {
                Require(entry.EntryId);
                continue;
            }

            if (entry.Kind == HarnessContextEntryKind.RecoverableContextSegment
                && (entry.ArtifactReferenceDigest is null
                    || !durableReferenceDigests.Contains(entry.ArtifactReferenceDigest)))
            {
                Require(entry.EntryId);
            }
        }

        // A complete tool exchange whose result carries an artifact reference is reference-bearing
        // durable context in its own right: the whole call/result group is required atomically — never
        // just the result entry alone, and regardless of how far outside the trailing recency window it
        // falls — so the correlated call can never be silently dropped out from under a preserved result.
        foreach (var group in analysis.Groups)
        {
            if (!group.IsComplete)
            {
                continue;
            }

            var isReferenceBearing = group.ResultEntryIds
                .Select(resultEntryId => entriesById[resultEntryId])
                .Any(resultEntry => resultEntry.ArtifactReferenceDigests.Count > 0);
            if (!isReferenceBearing)
            {
                continue;
            }

            foreach (var entryId in group.AllEntryIds)
            {
                Require(entryId);
            }
        }

        foreach (var entryId in ComputeIncompleteToolExchangeEntryIds(originalEntries, analysis))
        {
            Require(entryId);
        }

        var recencyUnits = BuildRecencyUnits(originalEntries, analysis);
        var retainedUnits = recencyUnits.Count <= RecentMessageRetentionCount
            ? recencyUnits
            : recencyUnits.GetRange(recencyUnits.Count - RecentMessageRetentionCount, RecentMessageRetentionCount);

        foreach (var unit in retainedUnits)
        {
            foreach (var entryId in unit)
            {
                Require(entryId);
            }
        }

        cancellationToken.ThrowIfCancellationRequested();

        var originalOrder = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < originalEntries.Count; i++)
        {
            originalOrder[originalEntries[i].EntryId] = i;
        }

        requiredIds.Sort((left, right) => originalOrder[left].CompareTo(originalOrder[right]));

        return HarnessPreservationSelection.Create(requiredIds, PreservationLabel, PreservationVersion);
    }

    /// <summary>
    /// Builds the ordered list of recency units: one per <see cref="HarnessContextEntryKind.ConversationalMessage"/>
    /// entry, and one per complete-or-incomplete tool-exchange group (spanning every entry id the group
    /// covers) — never splitting a group across two units.
    /// </summary>
    private static List<List<string>> BuildRecencyUnits(
        IReadOnlyList<HarnessContextEntry> entries, HarnessToolExchangeAnalysis analysis)
    {
        var entryToGroup = new Dictionary<string, HarnessToolExchangeGroup>(StringComparer.Ordinal);
        foreach (var group in analysis.Groups)
        {
            foreach (var entryId in group.AllEntryIds)
            {
                entryToGroup[entryId] = group;
            }
        }

        var units = new List<List<string>>();
        var consumedGroups = new HashSet<HarnessToolExchangeGroup>();

        foreach (var entry in entries)
        {
            if (entry.Kind == HarnessContextEntryKind.ConversationalMessage)
            {
                units.Add([entry.EntryId]);
            }
            else if (entry.Kind == HarnessContextEntryKind.ToolExchange)
            {
                if (entryToGroup.TryGetValue(entry.EntryId, out var group))
                {
                    if (consumedGroups.Add(group))
                    {
                        units.Add([.. group.AllEntryIds]);
                    }
                }
                else
                {
                    // Reachable for orphaned result entries (ToolExchange entries whose every result
                    // call id has no matching call anywhere in the analyzed entries): orphan result
                    // entries are not captured in any group, so they fall here. Kept as a single-entry
                    // fallback unit rather than silently dropping the entry.
                    units.Add([entry.EntryId]);
                }
            }
        }

        return units;
    }

    /// <summary>
    /// Every entry id <see cref="HarnessToolExchangeAnalysis"/> flags as part of an incomplete tool
    /// exchange: the call entry and matched result entries of an incomplete
    /// <see cref="HarnessToolExchangeGroup"/> (orphaned call, reordered result, or a duplicate call/
    /// result id shared with another group), every orphaned result entry (which is never itself part
    /// of a group), and every result entry beyond the first that declares a duplicated call id's
    /// result (only the first such entry is ever captured by a group's <see cref="HarnessToolExchangeGroup.ResultEntryIds"/>).
    /// Computed independent of recency so <see cref="SelectRequiredPreservation"/> can require these
    /// entries regardless of how far outside the trailing retention window they fall.
    /// </summary>
    private static HashSet<string> ComputeIncompleteToolExchangeEntryIds(
        IReadOnlyList<HarnessContextEntry> entries, HarnessToolExchangeAnalysis analysis)
    {
        var requiredEntryIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var group in analysis.Groups)
        {
            if (group.IsComplete)
            {
                continue;
            }

            foreach (var entryId in group.AllEntryIds)
            {
                requiredEntryIds.Add(entryId);
            }
        }

        foreach (var entryId in analysis.OrphanResultEntryIds)
        {
            requiredEntryIds.Add(entryId);
        }

        if (analysis.DuplicateResultCallIds.Count > 0)
        {
            var duplicateResultCallIds = new HashSet<string>(analysis.DuplicateResultCallIds, StringComparer.Ordinal);
            foreach (var entry in entries)
            {
                if (entry.Kind != HarnessContextEntryKind.ToolExchange)
                {
                    continue;
                }

                foreach (var result in entry.Message.Contents.OfType<FunctionResultContent>())
                {
                    if (duplicateResultCallIds.Contains(result.CallId))
                    {
                        requiredEntryIds.Add(entry.EntryId);
                    }
                }
            }
        }

        return requiredEntryIds;
    }

    private static void EnsureUniqueEntryIds(IReadOnlyList<HarnessContextEntry> entries)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            if (!seen.Add(entry.EntryId))
            {
                throw new ArgumentException(
                    $"Duplicate entry id '{entry.EntryId}' in the supplied entries.", nameof(entries));
            }
        }
    }
}
