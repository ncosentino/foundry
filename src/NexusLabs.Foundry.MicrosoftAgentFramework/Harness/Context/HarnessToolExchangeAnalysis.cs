using Microsoft.Extensions.AI;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Context;

/// <summary>
/// The deterministic, structural result of scanning an ordered set of <see cref="HarnessContextEntry"/>
/// values for tool-call/tool-result exchanges — never by parsing message text. Built once by
/// <see cref="Build"/> and reused by both the hybrid context policy (to keep a tool exchange atomic
/// across the recent-message retention boundary) and the compaction verifier (to reject an orphaned,
/// duplicated, or reordered exchange).
/// </summary>
internal sealed record HarnessToolExchangeAnalysis
{
    private HarnessToolExchangeAnalysis(
        IReadOnlyList<HarnessToolExchangeGroup> groups,
        IReadOnlyList<string> orphanedCallEntryIds,
        IReadOnlyList<string> orphanResultEntryIds,
        IReadOnlyList<string> duplicateCallIds,
        IReadOnlyList<string> duplicateResultCallIds,
        IReadOnlyList<string> reorderedCallEntryIds)
    {
        Groups = groups;
        OrphanedCallEntryIds = orphanedCallEntryIds;
        OrphanResultEntryIds = orphanResultEntryIds;
        DuplicateCallIds = duplicateCallIds;
        DuplicateResultCallIds = duplicateResultCallIds;
        ReorderedCallEntryIds = reorderedCallEntryIds;
    }

    /// <summary>Every tool-exchange group discovered, one per distinct call-bearing entry.</summary>
    internal IReadOnlyList<HarnessToolExchangeGroup> Groups { get; }

    /// <summary>
    /// Entry ids of call-bearing messages that declared at least one function call id with no matching
    /// function result anywhere in the analyzed entries.
    /// </summary>
    internal IReadOnlyList<string> OrphanedCallEntryIds { get; }

    /// <summary>
    /// Entry ids of result-bearing messages that declared a function result for a call id with no
    /// matching function call anywhere in the analyzed entries.
    /// </summary>
    internal IReadOnlyList<string> OrphanResultEntryIds { get; }

    /// <summary>Function call ids declared by more than one call-bearing entry.</summary>
    internal IReadOnlyList<string> DuplicateCallIds { get; }

    /// <summary>Function call ids whose result was declared by more than one result-bearing entry.</summary>
    internal IReadOnlyList<string> DuplicateResultCallIds { get; }

    /// <summary>
    /// Entry ids of call-bearing messages whose matching result(s) all exist but appear at or before
    /// the call in the analyzed entry order.
    /// </summary>
    internal IReadOnlyList<string> ReorderedCallEntryIds { get; }

    /// <exception cref="ArgumentNullException"><paramref name="entries"/> is <see langword="null"/>.</exception>
    internal static HarnessToolExchangeAnalysis Build(IReadOnlyList<HarnessContextEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var entryIndex = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < entries.Count; i++)
        {
            entryIndex[entries[i].EntryId] = i;
        }

        var callOwner = new Dictionary<string, string>(StringComparer.Ordinal);
        var duplicateCallIds = new List<string>();
        var callEntries = new List<(string EntryId, IReadOnlyList<string> CallIds)>();

        foreach (var entry in entries)
        {
            if (entry.Kind != HarnessContextEntryKind.ToolExchange)
            {
                continue;
            }

            var callIds = entry.Message.Contents
                .OfType<FunctionCallContent>()
                .Select(content => content.CallId)
                .ToList();
            if (callIds.Count == 0)
            {
                continue;
            }

            callEntries.Add((entry.EntryId, callIds));
            foreach (var callId in callIds)
            {
                if (!callOwner.TryAdd(callId, entry.EntryId))
                {
                    duplicateCallIds.Add(callId);
                }
            }
        }

        var resultOwners = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            if (entry.Kind != HarnessContextEntryKind.ToolExchange)
            {
                continue;
            }

            foreach (var result in entry.Message.Contents.OfType<FunctionResultContent>())
            {
                if (!resultOwners.TryGetValue(result.CallId, out var owners))
                {
                    owners = [];
                    resultOwners[result.CallId] = owners;
                }

                owners.Add(entry.EntryId);
            }
        }

        var duplicateResultCallIds = resultOwners
            .Where(pair => pair.Value.Count > 1)
            .Select(pair => pair.Key)
            .ToList();

        var orphanResultEntryIds = resultOwners
            .Where(pair => !callOwner.ContainsKey(pair.Key))
            .SelectMany(pair => pair.Value)
            .Distinct()
            .ToList();

        var groups = new List<HarnessToolExchangeGroup>();
        var orphanedCallEntryIds = new List<string>();
        var reorderedCallEntryIds = new List<string>();

        foreach (var (callEntryId, callIds) in callEntries)
        {
            var callIndex = entryIndex[callEntryId];
            var resultEntryIds = new List<string>();
            var allMatched = true;
            var allInOrder = true;

            foreach (var callId in callIds)
            {
                if (!resultOwners.TryGetValue(callId, out var owners) || owners.Count == 0)
                {
                    allMatched = false;
                    continue;
                }

                var resultEntryId = owners[0];
                if (!resultEntryIds.Contains(resultEntryId))
                {
                    resultEntryIds.Add(resultEntryId);
                }

                if (entryIndex[resultEntryId] <= callIndex)
                {
                    allInOrder = false;
                }
            }

            resultEntryIds = [.. resultEntryIds.OrderBy(id => entryIndex[id])];

            var hasDuplicateIssue = callIds.Any(duplicateCallIds.Contains) ||
                callIds.Any(id => resultOwners.TryGetValue(id, out var owners) && owners.Count > 1);

            if (!allMatched)
            {
                orphanedCallEntryIds.Add(callEntryId);
            }
            else if (!allInOrder)
            {
                reorderedCallEntryIds.Add(callEntryId);
            }

            var isComplete = allMatched && allInOrder && !hasDuplicateIssue;
            groups.Add(HarnessToolExchangeGroup.Create(callEntryId, callIds, resultEntryIds, isComplete));
        }

        return new HarnessToolExchangeAnalysis(
            groups,
            orphanedCallEntryIds,
            orphanResultEntryIds,
            [.. duplicateCallIds.Distinct()],
            duplicateResultCallIds,
            reorderedCallEntryIds);
    }
}
