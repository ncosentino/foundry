using System.Text.Json;

using Microsoft.Extensions.AI;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Context;

/// <summary>
/// Compares an original entry set against an upstream reducer's proposed reduced entry set and a
/// <see cref="HarnessHybridContextPolicy"/>, returning an explicit
/// <see cref="HarnessCompactionVerificationResult"/> that is never a silent success. Every check below
/// is structural: entry ids, kinds, message roles/authors/text, and function-call/result payloads —
/// never a guess at prose meaning.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Required-preservation check.</strong> Every id in
/// <see cref="HarnessHybridContextPolicy.SelectRequiredPreservation"/>'s selection must appear in the
/// proposed entries, in the same relative order, with byte-for-byte unchanged message content. A
/// required <see cref="HarnessContextEntryKind.AuthoritativeSessionState"/> or
/// <see cref="HarnessContextEntryKind.ApprovalSecurityState"/> entry that a reducer dropped —
/// regardless of whether a contradictory <see cref="HarnessContextEntryKind.Summary"/> entry remains —
/// always rejects categorically.
/// </para>
/// <para>
/// <strong>Retained-entry identity check.</strong> Every proposed entry that reuses an original entry's
/// id must preserve that entry's <see cref="HarnessContextEntryKind"/>, message role, author name, and
/// structural payload unchanged. A reducer may remove an eligible original entry or add a brand-new
/// <see cref="HarnessContextEntryKind.ConversationalMessage"/> or
/// <see cref="HarnessContextEntryKind.Summary"/> entry, but may never mutate an entry it retains.
/// Kind changes are detected here for all retained entries; message-content changes for non-required
/// retained entries (required entries' message content is already covered by the required-preservation
/// check above).
/// </para>
/// <para>
/// <strong>Tool-exchange self-consistency check.</strong> Independent of whether a given exchange was
/// required, the proposed entries themselves are re-analyzed with
/// <see cref="HarnessToolExchangeAnalysis.Build"/>: an orphaned call, an orphaned result, a duplicated
/// call or result id, or a call whose result(s) were reordered ahead of it all reject categorically. A
/// reducer may still omit an entire non-required exchange; it may never keep only half of one.
/// </para>
/// <para>
/// <strong>Forged-entry check.</strong> A proposed entry id absent from the original set may only
/// claim <see cref="HarnessContextEntryKind.ConversationalMessage"/> or
/// <see cref="HarnessContextEntryKind.Summary"/> — the two reducible, reducer-authored kinds. Any
/// other kind on a brand-new entry id is rejected as forged.
/// </para>
/// </remarks>
internal static class HarnessCompactionVerifier
{
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="originalEntries"/> or <paramref name="proposedEntries"/> contains two entries
    /// sharing the same <see cref="HarnessContextEntry.EntryId"/>.
    /// </exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
    internal static HarnessCompactionVerificationResult Verify(
        IReadOnlyList<HarnessContextEntry> originalEntries,
        IReadOnlyList<HarnessContextEntry> proposedEntries,
        HarnessHybridContextPolicy policy,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(originalEntries);
        ArgumentNullException.ThrowIfNull(proposedEntries);
        ArgumentNullException.ThrowIfNull(policy);
        cancellationToken.ThrowIfCancellationRequested();

        var originalById = BuildUniqueIndex(originalEntries, nameof(originalEntries));
        var proposedById = BuildUniqueIndex(proposedEntries, nameof(proposedEntries));

        var selection = policy.SelectRequiredPreservation(originalEntries, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        var requiredIdSet = new HashSet<string>(selection.RequiredEntryIds, StringComparer.Ordinal);

        var reasons = new List<HarnessCompactionRejectionReason>();
        var missingRequiredEntryIds = new List<string>();
        var invalidEntryIds = new HashSet<string>(StringComparer.Ordinal);

        CheckRequiredPreservation(
            selection, proposedEntries, originalById, proposedById, reasons, missingRequiredEntryIds, invalidEntryIds);
        CheckForgedEntries(originalById, proposedEntries, reasons, invalidEntryIds);
        CheckRetainedOriginalEntries(originalById, proposedEntries, requiredIdSet, reasons, invalidEntryIds);

        cancellationToken.ThrowIfCancellationRequested();

        var proposedAnalysis = HarnessToolExchangeAnalysis.Build(proposedEntries);
        cancellationToken.ThrowIfCancellationRequested();
        CheckToolExchangeSelfConsistency(proposedAnalysis, reasons, invalidEntryIds);

        var fallbackEntryIds = selection.RequiredEntryIds;
        if (reasons.Count == 0)
        {
            return HarnessCompactionVerificationResult.Accepted(fallbackEntryIds);
        }

        return HarnessCompactionVerificationResult.Rejected(
            Distinct(reasons), missingRequiredEntryIds, [.. invalidEntryIds], fallbackEntryIds);
    }

    private static void CheckRequiredPreservation(
        HarnessPreservationSelection selection,
        IReadOnlyList<HarnessContextEntry> proposedEntries,
        IReadOnlyDictionary<string, HarnessContextEntry> originalById,
        IReadOnlyDictionary<string, HarnessContextEntry> proposedById,
        List<HarnessCompactionRejectionReason> reasons,
        List<string> missingRequiredEntryIds,
        HashSet<string> invalidEntryIds)
    {
        var proposedPositions = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < proposedEntries.Count; i++)
        {
            proposedPositions[proposedEntries[i].EntryId] = i;
        }

        var lastSeenPosition = -1;
        var outOfOrder = false;

        foreach (var requiredId in selection.RequiredEntryIds)
        {
            if (!proposedPositions.TryGetValue(requiredId, out var position))
            {
                missingRequiredEntryIds.Add(requiredId);
                continue;
            }

            if (position <= lastSeenPosition)
            {
                outOfOrder = true;
            }

            lastSeenPosition = position;

            if (!MessagesStructurallyEqual(originalById[requiredId].Message, proposedById[requiredId].Message))
            {
                reasons.Add(HarnessCompactionRejectionReason.RequiredEntryContentMismatch);
                invalidEntryIds.Add(requiredId);
            }
        }

        if (missingRequiredEntryIds.Count > 0)
        {
            reasons.Add(HarnessCompactionRejectionReason.MissingRequiredEntry);
        }

        if (outOfOrder)
        {
            reasons.Add(HarnessCompactionRejectionReason.RequiredEntryOutOfOrder);
        }
    }

    private static void CheckForgedEntries(
        IReadOnlyDictionary<string, HarnessContextEntry> originalById,
        IReadOnlyList<HarnessContextEntry> proposedEntries,
        List<HarnessCompactionRejectionReason> reasons,
        HashSet<string> invalidEntryIds)
    {
        foreach (var proposedEntry in proposedEntries)
        {
            if (originalById.ContainsKey(proposedEntry.EntryId))
            {
                continue;
            }

            if (proposedEntry.Kind is HarnessContextEntryKind.ConversationalMessage or HarnessContextEntryKind.Summary)
            {
                continue;
            }

            reasons.Add(HarnessCompactionRejectionReason.ForgedStructuralEntry);
            invalidEntryIds.Add(proposedEntry.EntryId);
        }
    }

    /// <summary>
    /// Checks every proposed entry that reuses an original entry's id for identity preservation:
    /// a Kind change is rejected for any retained entry (required or not); a message-content change
    /// is rejected for non-required retained entries (required entries' message content is already
    /// verified by <see cref="CheckRequiredPreservation"/>).
    /// </summary>
    private static void CheckRetainedOriginalEntries(
        IReadOnlyDictionary<string, HarnessContextEntry> originalById,
        IReadOnlyList<HarnessContextEntry> proposedEntries,
        HashSet<string> requiredIdSet,
        List<HarnessCompactionRejectionReason> reasons,
        HashSet<string> invalidEntryIds)
    {
        foreach (var proposed in proposedEntries)
        {
            if (!originalById.TryGetValue(proposed.EntryId, out var original))
            {
                continue;
            }

            var kindChanged = original.Kind != proposed.Kind;
            var isRequired = requiredIdSet.Contains(proposed.EntryId);
            var messageChanged = !isRequired && !MessagesStructurallyEqual(original.Message, proposed.Message);

            if (kindChanged || messageChanged)
            {
                reasons.Add(HarnessCompactionRejectionReason.RetainedOriginalEntryMutated);
                invalidEntryIds.Add(proposed.EntryId);
            }
        }
    }

    private static void CheckToolExchangeSelfConsistency(
        HarnessToolExchangeAnalysis proposedAnalysis,
        List<HarnessCompactionRejectionReason> reasons,
        HashSet<string> invalidEntryIds)
    {
        if (proposedAnalysis.OrphanedCallEntryIds.Count > 0)
        {
            reasons.Add(HarnessCompactionRejectionReason.OrphanedToolCall);
            foreach (var id in proposedAnalysis.OrphanedCallEntryIds)
            {
                invalidEntryIds.Add(id);
            }
        }

        if (proposedAnalysis.OrphanResultEntryIds.Count > 0)
        {
            reasons.Add(HarnessCompactionRejectionReason.OrphanedToolResult);
            foreach (var id in proposedAnalysis.OrphanResultEntryIds)
            {
                invalidEntryIds.Add(id);
            }
        }

        if (proposedAnalysis.DuplicateCallIds.Count > 0)
        {
            reasons.Add(HarnessCompactionRejectionReason.DuplicateToolCall);
        }

        if (proposedAnalysis.DuplicateResultCallIds.Count > 0)
        {
            reasons.Add(HarnessCompactionRejectionReason.DuplicateToolResult);
        }

        if (proposedAnalysis.ReorderedCallEntryIds.Count > 0)
        {
            reasons.Add(HarnessCompactionRejectionReason.ReorderedToolGroup);
            foreach (var id in proposedAnalysis.ReorderedCallEntryIds)
            {
                invalidEntryIds.Add(id);
            }
        }
    }

    private static Dictionary<string, HarnessContextEntry> BuildUniqueIndex(
        IReadOnlyList<HarnessContextEntry> entries, string paramName)
    {
        var index = new Dictionary<string, HarnessContextEntry>(StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            if (!index.TryAdd(entry.EntryId, entry))
            {
                throw new ArgumentException($"Duplicate entry id '{entry.EntryId}' in {paramName}.", paramName);
            }
        }

        return index;
    }

    /// <summary>
    /// Structural equality for two <see cref="ChatMessage"/> instances: role, author name, text,
    /// content count and order, and per-content structural equality via
    /// <see cref="ContentStructurallyEqual"/>. Never parses prose.
    /// </summary>
    private static bool MessagesStructurallyEqual(ChatMessage original, ChatMessage proposed)
    {
        if (original.Role != proposed.Role)
        {
            return false;
        }

        if (!string.Equals(original.AuthorName, proposed.AuthorName, StringComparison.Ordinal))
        {
            return false;
        }

        if (!string.Equals(original.Text, proposed.Text, StringComparison.Ordinal))
        {
            return false;
        }

        if (original.Contents.Count != proposed.Contents.Count)
        {
            return false;
        }

        for (var i = 0; i < original.Contents.Count; i++)
        {
            if (!ContentStructurallyEqual(original.Contents[i], proposed.Contents[i]))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Structural equality for two <see cref="AIContent"/> instances. For known content types
    /// (<see cref="FunctionCallContent"/>, <see cref="FunctionResultContent"/>,
    /// <see cref="TextContent"/>) all identity-bearing fields are compared deterministically.
    /// For unknown types, reference identity catches the common case where a pass-through
    /// copy shares the same reference; otherwise the comparison fails closed (returns
    /// <see langword="false"/>) to avoid silently accepting an unverifiable payload.
    /// </summary>
    private static bool ContentStructurallyEqual(AIContent original, AIContent proposed)
    {
        if (ReferenceEquals(original, proposed))
        {
            return true;
        }

        if (original is FunctionCallContent originalCall && proposed is FunctionCallContent proposedCall)
        {
            return string.Equals(originalCall.CallId, proposedCall.CallId, StringComparison.Ordinal)
                && string.Equals(originalCall.Name, proposedCall.Name, StringComparison.Ordinal)
                && originalCall.InformationalOnly == proposedCall.InformationalOnly
                && ArgumentsStructurallyEqual(originalCall.Arguments, proposedCall.Arguments);
        }

        if (original is FunctionResultContent originalResult && proposed is FunctionResultContent proposedResult)
        {
            return string.Equals(originalResult.CallId, proposedResult.CallId, StringComparison.Ordinal)
                && ValuesStructurallyEqual(originalResult.Result, proposedResult.Result);
        }

        if (original is TextContent originalText && proposed is TextContent proposedText)
        {
            return string.Equals(originalText.Text, proposedText.Text, StringComparison.Ordinal);
        }

        return false;
    }

    private static bool ArgumentsStructurallyEqual(
        IDictionary<string, object?>? original, IDictionary<string, object?>? proposed)
    {
        if (original is null && proposed is null) return true;
        if (original is null || proposed is null) return false;

        return DictionariesStructurallyEqual(original, proposed);
    }

    private static bool DictionariesStructurallyEqual(
        IDictionary<string, object?> original, IDictionary<string, object?> proposed)
    {
        if (original.Count != proposed.Count) return false;

        foreach (var kvp in original)
        {
            if (!proposed.TryGetValue(kvp.Key, out var proposedValue)) return false;
            if (!ValuesStructurallyEqual(kvp.Value, proposedValue)) return false;
        }

        return true;
    }

    /// <summary>
    /// Deterministic structural equality for argument and result payload values. Handles
    /// <see langword="null"/>, <see cref="string"/>, common primitive value types,
    /// <see cref="JsonElement"/> (by raw text), <see cref="IDictionary{TKey,TValue}"/> of string to
    /// <see langword="object"/> (recursively), and <see cref="IList{T}"/> of <see langword="object"/>
    /// (recursively). Any other type fails closed — returns <see langword="false"/> — to avoid
    /// silently accepting an unverifiable payload.
    /// </summary>
    private static bool ValuesStructurallyEqual(object? original, object? proposed)
    {
        if (ReferenceEquals(original, proposed)) return true;
        if (original is null || proposed is null) return false;
        if (original.GetType() != proposed.GetType()) return false;

        return original switch
        {
            string s => string.Equals(s, (string)proposed, StringComparison.Ordinal),
            bool b => b == (bool)proposed,
            int i => i == (int)proposed,
            long l => l == (long)proposed,
            double d => d == (double)proposed,
            float f => f == (float)proposed,
            short sh => sh == (short)proposed,
            byte by => by == (byte)proposed,
            uint u => u == (uint)proposed,
            ulong ul => ul == (ulong)proposed,
            decimal dec => dec == (decimal)proposed,
            JsonElement je => string.Equals(
                je.GetRawText(), ((JsonElement)proposed).GetRawText(), StringComparison.Ordinal),
            IDictionary<string, object?> dictOriginal =>
                DictionariesStructurallyEqual(dictOriginal, (IDictionary<string, object?>)proposed),
            IList<object?> listOriginal =>
                ListsStructurallyEqual(listOriginal, (IList<object?>)proposed),
            _ => false,
        };
    }

    private static bool ListsStructurallyEqual(IList<object?> original, IList<object?> proposed)
    {
        if (original.Count != proposed.Count) return false;

        for (var i = 0; i < original.Count; i++)
        {
            if (!ValuesStructurallyEqual(original[i], proposed[i])) return false;
        }

        return true;
    }

    private static IReadOnlyList<HarnessCompactionRejectionReason> Distinct(
        List<HarnessCompactionRejectionReason> reasons)
    {
        var seen = new HashSet<HarnessCompactionRejectionReason>();
        var result = new List<HarnessCompactionRejectionReason>();
        foreach (var reason in reasons)
        {
            if (seen.Add(reason))
            {
                result.Add(reason);
            }
        }

        return result;
    }
}
