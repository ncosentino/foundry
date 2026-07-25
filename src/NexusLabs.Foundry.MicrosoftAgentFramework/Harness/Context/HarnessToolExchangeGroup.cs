namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Context;

/// <summary>
/// One atomic assistant tool-call/tool-result exchange derived structurally from a set of
/// <see cref="HarnessContextEntry"/> values by <see cref="HarnessToolExchangeAnalysis.Build"/>. A
/// group is identified by exactly one call-bearing entry and the function call ids it declares; it
/// never merges two different call-bearing entries.
/// </summary>
internal sealed record HarnessToolExchangeGroup
{
    private HarnessToolExchangeGroup(
        string callEntryId,
        IReadOnlyList<string> callIds,
        IReadOnlyList<string> resultEntryIds,
        bool isComplete)
    {
        CallEntryId = callEntryId;
        CallIds = callIds;
        ResultEntryIds = resultEntryIds;
        IsComplete = isComplete;
        AllEntryIds = BuildAllEntryIds(callEntryId, resultEntryIds);
    }

    /// <summary>The entry id of the assistant message that declared this group's function call(s).</summary>
    internal string CallEntryId { get; }

    /// <summary>
    /// The function call ids declared by <see cref="CallEntryId"/>'s message, in declaration order.
    /// Contains more than one id when the assistant message issued multiple calls at once.
    /// </summary>
    internal IReadOnlyList<string> CallIds { get; }

    /// <summary>
    /// The entry ids carrying this group's matching function results, ordered by their position among
    /// the entries the group was built from. May contain more than one entry id when results are split
    /// across separate tool-result messages.
    /// </summary>
    internal IReadOnlyList<string> ResultEntryIds { get; }

    /// <summary>
    /// <see langword="true"/> only when every id in <see cref="CallIds"/> has exactly one matching
    /// function result, every result appears after <see cref="CallEntryId"/>, and no call or result id
    /// in this group is duplicated elsewhere in the analyzed entries.
    /// </summary>
    internal bool IsComplete { get; }

    /// <summary>
    /// <see cref="CallEntryId"/> followed by <see cref="ResultEntryIds"/> — every entry id this group
    /// spans.
    /// </summary>
    internal IReadOnlyList<string> AllEntryIds { get; }

    /// <exception cref="ArgumentNullException">
    /// <paramref name="callEntryId"/>, <paramref name="callIds"/>, or <paramref name="resultEntryIds"/>
    /// is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException"><paramref name="callIds"/> is empty.</exception>
    internal static HarnessToolExchangeGroup Create(
        string callEntryId,
        IReadOnlyList<string> callIds,
        IReadOnlyList<string> resultEntryIds,
        bool isComplete)
    {
        ArgumentNullException.ThrowIfNull(callEntryId);
        ArgumentNullException.ThrowIfNull(callIds);
        ArgumentNullException.ThrowIfNull(resultEntryIds);

        if (callIds.Count == 0)
        {
            throw new ArgumentException("A tool-exchange group requires at least one call id.", nameof(callIds));
        }

        return new HarnessToolExchangeGroup(callEntryId, callIds, resultEntryIds, isComplete);
    }

    private static IReadOnlyList<string> BuildAllEntryIds(string callEntryId, IReadOnlyList<string> resultEntryIds)
    {
        var all = new List<string>(resultEntryIds.Count + 1) { callEntryId };
        all.AddRange(resultEntryIds);
        return all;
    }
}
