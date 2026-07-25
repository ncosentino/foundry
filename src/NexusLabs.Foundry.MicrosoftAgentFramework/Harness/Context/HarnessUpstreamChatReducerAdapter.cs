using Microsoft.Extensions.AI;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Context;

/// <summary>
/// Bridges a selected upstream <see cref="IChatReducer"/> — the raw-<see cref="ChatMessage"/>
/// reduction abstraction MEAI 10.6 defines — into the <see cref="IHarnessContextReducer"/>
/// abstraction <see cref="HarnessContextAssembler"/> invokes. The upstream reducer never sees, and can
/// never forge, a <see cref="HarnessContextEntry"/> directly: it only ever proposes a replacement
/// <see cref="ChatMessage"/> sequence, and this bridge alone decides what that sequence means in terms
/// of retained identity and structural kind.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Retained vs. new, by structural content match, never by instance identity.</strong> Because
/// <see cref="HarnessContextEntry.Message"/> always hands back a freshly-cloned instance, this bridge
/// can never rely on reference equality to recognize which of the upstream reducer's returned messages
/// correspond to original entries. Instead, each returned message is matched — in order, against the
/// not-yet-consumed original entries in their original order — by structural content equality (role,
/// author, and content-by-content equality; see <see cref="AreStructurallyEqual"/>). A match reuses the
/// exact original <see cref="HarnessContextEntry"/> instance verbatim: its <see cref="HarnessContextEntry.EntryId"/>,
/// <see cref="HarnessContextEntry.Kind"/>, and message content can therefore never be forged or mutated
/// by this bridge, regardless of what the upstream reducer's returned instance looks like. An original
/// entry with no match among the returned messages is treated as dropped. A returned message with no
/// match among the not-yet-consumed original entries is a brand-new reducer-authored message.
/// </para>
/// <para>
/// <strong>New messages are always labeled <see cref="HarnessContextEntryKind.Summary"/>, or rejected.</strong>
/// A brand-new message that carries no function-call or function-result content becomes a
/// <see cref="HarnessContextEntryKind.Summary"/> entry with a deterministic, freshly-minted id (see
/// <see cref="MintSummaryEntryId"/>) — <see cref="HarnessCompactionVerifier"/> only ever accepts
/// <see cref="HarnessContextEntryKind.ConversationalMessage"/> or <see cref="HarnessContextEntryKind.Summary"/>
/// for an entry id absent from the original set, and a reducer-authored replacement is, by definition, a
/// summary of what it replaces. A brand-new message that <em>does</em> carry function-call or
/// function-result content can never be a legitimate reducer output — an upstream reducer may only
/// retain or drop an original tool exchange verbatim, never fabricate one — so this bridge throws
/// <see cref="HarnessCompactionReducerContractException"/> instead of ever admitting it.
/// </para>
/// <para>
/// <strong>Fallback rather than weakened verification.</strong> This bridge never itself decides whether
/// a proposal is acceptable beyond the tool-fabrication check above — <see cref="HarnessCompactionVerifier"/>,
/// invoked by <see cref="HarnessContextAssembler"/>, remains the sole authority for required-entry
/// preservation and structural tool-sequence validity. If the upstream reducer cannot preserve exact
/// structural messages for entries it retains, the mismatch surfaces as a "new" entry per the matching
/// rule above rather than a silently accepted mutation, and the verifier's own required-entry and
/// tool-sequence checks reject a proposal that drops or breaks required content — this bridge adds
/// nothing that could relax that outcome.
/// </para>
/// </remarks>
internal sealed class HarnessUpstreamChatReducerAdapter : IHarnessContextReducer
{
    private readonly IChatReducer _upstreamReducer;

    /// <exception cref="ArgumentNullException"><paramref name="upstreamReducer"/> is <see langword="null"/>.</exception>
    internal HarnessUpstreamChatReducerAdapter(IChatReducer upstreamReducer)
    {
        ArgumentNullException.ThrowIfNull(upstreamReducer);
        _upstreamReducer = upstreamReducer;
    }

    /// <exception cref="ArgumentNullException"><paramref name="request"/> is <see langword="null"/>.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
    /// <exception cref="HarnessCompactionReducerContractException">
    /// The upstream reducer returned <see langword="null"/>, or returned a brand-new message carrying
    /// function-call or function-result content not present verbatim among the original tool-exchange
    /// entries.
    /// </exception>
    public async Task<IReadOnlyList<HarnessContextEntry>> ReduceAsync(
        HarnessContextReductionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var originalEntries = request.Entries;
        var originalMessages = originalEntries.Select(entry => entry.Message).ToList();

        var reduced = await _upstreamReducer
            .ReduceAsync(originalMessages, cancellationToken)
            .ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        if (reduced is null)
        {
            throw new HarnessCompactionReducerContractException(
                "The upstream IChatReducer returned a null message sequence from ReduceAsync. An " +
                "upstream reducer must throw rather than return null.");
        }

        var reducedMessages = reduced as IReadOnlyList<ChatMessage> ?? reduced.ToList();

        var consumed = new bool[originalEntries.Count];
        var result = new List<HarnessContextEntry>(reducedMessages.Count);
        var syntheticOrdinal = 0;

        foreach (var message in reducedMessages)
        {
            var matchedIndex = FindUnconsumedMatch(originalEntries, consumed, message);
            if (matchedIndex is int index)
            {
                consumed[index] = true;
                result.Add(originalEntries[index]);
                continue;
            }

            if (HasToolContent(message))
            {
                throw new HarnessCompactionReducerContractException(
                    "The upstream reducer returned a message carrying function-call or function-result " +
                    "content that does not match any original tool-exchange entry verbatim. An upstream " +
                    "reducer may only retain or drop an original tool exchange, never fabricate one.");
            }

            var entryId = MintSummaryEntryId(message, syntheticOrdinal);
            syntheticOrdinal++;
            result.Add(HarnessContextEntry.Create(entryId, HarnessContextEntryKind.Summary, message));
        }

        return result;
    }

    private static int? FindUnconsumedMatch(
        IReadOnlyList<HarnessContextEntry> originalEntries, bool[] consumed, ChatMessage candidate)
    {
        for (var i = 0; i < originalEntries.Count; i++)
        {
            if (consumed[i])
            {
                continue;
            }

            if (AreStructurallyEqual(originalEntries[i].Message, candidate))
            {
                return i;
            }
        }

        return null;
    }

    private static bool HasToolContent(ChatMessage message) =>
        message.Contents.Any(content => content is FunctionCallContent or FunctionResultContent);

    private static string MintSummaryEntryId(ChatMessage message, int ordinal)
    {
        var digestSeed = $"{ordinal}:{message.Role}:{message.Text}";
        return $"upstream-summary-{ordinal}-{HarnessArtifactIdentity.ComputeDigest(digestSeed)}";
    }

    private static bool AreStructurallyEqual(ChatMessage left, ChatMessage right)
    {
        if (left.Role != right.Role ||
            !string.Equals(left.AuthorName, right.AuthorName, StringComparison.Ordinal) ||
            left.Contents.Count != right.Contents.Count)
        {
            return false;
        }

        for (var i = 0; i < left.Contents.Count; i++)
        {
            if (!AreContentsEqual(left.Contents[i], right.Contents[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool AreContentsEqual(AIContent left, AIContent right)
    {
        if (left.GetType() != right.GetType())
        {
            return false;
        }

        return (left, right) switch
        {
            (TextContent l, TextContent r) => string.Equals(l.Text, r.Text, StringComparison.Ordinal),
            (FunctionCallContent l, FunctionCallContent r) =>
                string.Equals(l.CallId, r.CallId, StringComparison.Ordinal) &&
                string.Equals(l.Name, r.Name, StringComparison.Ordinal) &&
                AreArgumentsEqual(l.Arguments, r.Arguments),
            (FunctionResultContent l, FunctionResultContent r) =>
                string.Equals(l.CallId, r.CallId, StringComparison.Ordinal) &&
                AreValuesEqual(l.Result, r.Result),
            _ => false,
        };
    }

    private static bool AreArgumentsEqual(
        IDictionary<string, object?>? left, IDictionary<string, object?>? right)
    {
        if (left is null || right is null)
        {
            return left is null && right is null;
        }

        if (left.Count != right.Count)
        {
            return false;
        }

        foreach (var (key, value) in left)
        {
            if (!right.TryGetValue(key, out var otherValue) || !AreValuesEqual(value, otherValue))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Recursively compares two already-normalized values (see <see cref="HarnessContextEntry.NormalizeValue"/>'s
    /// closed set of supported shapes) by structural content — never via a reflection-based, AOT-unsafe
    /// generic serialization fallback.
    /// </summary>
    private static bool AreValuesEqual(object? left, object? right)
    {
        if (left is null || right is null)
        {
            return left is null && right is null;
        }

        if (left is System.Text.Json.JsonElement leftElement &&
            right is System.Text.Json.JsonElement rightElement)
        {
            return leftElement.GetRawText() == rightElement.GetRawText();
        }

        if (left is IDictionary<string, object?> leftDict && right is IDictionary<string, object?> rightDict)
        {
            if (leftDict.Count != rightDict.Count)
            {
                return false;
            }

            foreach (var (key, value) in leftDict)
            {
                if (!rightDict.TryGetValue(key, out var otherValue) || !AreValuesEqual(value, otherValue))
                {
                    return false;
                }
            }

            return true;
        }

        if (left is IList<object?> leftList && right is IList<object?> rightList)
        {
            if (leftList.Count != rightList.Count)
            {
                return false;
            }

            for (var i = 0; i < leftList.Count; i++)
            {
                if (!AreValuesEqual(leftList[i], rightList[i]))
                {
                    return false;
                }
            }

            return true;
        }

        return left.Equals(right);
    }
}
