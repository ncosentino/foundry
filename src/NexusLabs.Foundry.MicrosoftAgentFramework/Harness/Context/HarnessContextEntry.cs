using System.Text.Json;

using Microsoft.Extensions.AI;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Context;

/// <summary>
/// One explicitly classified, defensively-copied context entry over a single <see cref="ChatMessage"/>.
/// This is the unit the hybrid preservation policy and verifier reason about: an <see cref="EntryId"/>
/// (a stable logical identity independent of any upstream reducer rebuilding message instances), a
/// structural <see cref="HarnessContextEntryKind"/> label, and the message itself.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Defensive copy, always.</strong> <see cref="Create"/> never stores the caller's
/// <see cref="ChatMessage"/> instance directly. <see cref="ChatMessage.Contents"/> is a mutable
/// <see cref="IList{T}"/> that a caller could keep a reference to and mutate after this entry is
/// constructed; <see cref="Create"/> copies that list (and the message's own settable metadata) into a
/// new <see cref="ChatMessage"/> so a later out-of-band mutation of the original can never change what
/// this entry reports. <see cref="Message"/> itself returns a freshly re-cloned instance on every
/// access — never the exact stored instance — so a caller cannot mutate this entry's own stored state
/// by mutating a previously returned value either; <see cref="Copy"/> applies the same cloning to hand
/// out an independent entry (never sharing a <see cref="ChatMessage"/> or content-list instance with
/// its source) at every snapshot/request/result boundary this type crosses.
/// </para>
/// <para>
/// <strong>Argument and result normalization.</strong> <see cref="FunctionCallContent.Arguments"/>
/// values and <see cref="FunctionResultContent.Result"/> are normalized to an immutable, explicitly
/// AOT-safe snapshot by <see cref="NormalizeValue"/> before storage. Only the shapes it documents are
/// supported: <see langword="null"/>, <see cref="string"/>, common primitive value types,
/// <see cref="JsonElement"/> (cloned), and string-keyed dictionaries or object lists/arrays of any of
/// these, normalized recursively. There is no reflection-based fallback for an unrecognized object
/// graph — <see cref="NormalizeValue"/> throws <see cref="NotSupportedException"/> instead of silently
/// admitting an arbitrary type into preserved context. The same closed-world rule applies one level up,
/// at the <see cref="AIContent"/> shape itself: only <see cref="FunctionCallContent"/>,
/// <see cref="FunctionResultContent"/>, and <see cref="TextContent"/> are recognized and deep-copied;
/// any other <see cref="AIContent"/> subtype throws <see cref="NotSupportedException"/> rather than
/// being passed through by reference as an unverifiable caller-controlled mutable instance.
/// </para>
/// <para>
/// <strong>Structural validation, not prose parsing.</strong> <see cref="HarnessContextEntryKind.ArtifactReference"/>
/// and <see cref="HarnessContextEntryKind.ToolExchange"/> are the two kinds with a verifiable structural
/// shape, so <see cref="Create"/> enforces that shape directly: an artifact-reference entry's exact text
/// must be one canonical <c>artifact://sha256/{64 lowercase hex}</c> reference (never a bare path or an
/// arbitrary URI), and a tool-exchange entry's message must carry exactly one of a
/// <see cref="FunctionCallContent"/> or a <see cref="FunctionResultContent"/> — never both in the same
/// entry, so a downstream reader is never left to guess whether a mixed entry represents a call or a
/// result first. Conversely, every non-<see cref="HarnessContextEntryKind.ToolExchange"/> kind
/// (<see cref="HarnessContextEntryKind.SystemInstruction"/>,
/// <see cref="HarnessContextEntryKind.AuthoritativeSessionState"/>,
/// <see cref="HarnessContextEntryKind.ApprovalSecurityState"/>,
/// <see cref="HarnessContextEntryKind.ArtifactReference"/>,
/// <see cref="HarnessContextEntryKind.ConversationalMessage"/>, and
/// <see cref="HarnessContextEntryKind.Summary"/>) fails closed if its message carries any
/// <see cref="FunctionCallContent"/> or <see cref="FunctionResultContent"/> at all — tool content can
/// never be smuggled into preserved context under a label the reducer or verifier does not treat as a
/// tool exchange.
/// </para>
/// </remarks>
internal sealed record HarnessContextEntry
{
    private readonly ChatMessage _message;

    private HarnessContextEntry(
        string entryId,
        HarnessContextEntryKind kind,
        ChatMessage message,
        IReadOnlyList<string> artifactReferenceDigests,
        HarnessArtifactRecoverableContextSegment? recoverableSegment)
    {
        EntryId = entryId;
        Kind = kind;
        _message = message;
        // Defensive copy: always store an independently allocated array so callers cannot mutate
        // this entry's ArtifactReferenceDigests via a cast to IList<string> or ICollection<string>,
        // regardless of what concrete collection the caller supplied.
        ArtifactReferenceDigests = [.. artifactReferenceDigests];
        RecoverableSegment = recoverableSegment;
    }

    /// <summary>
    /// Stable logical identity for this entry, supplied by the caller. Comparisons between an
    /// original entry set and a proposed reduced entry set are always by <see cref="EntryId"/>, never
    /// by <see cref="ChatMessage"/> reference or by <see cref="ChatMessage.MessageId"/>.
    /// </summary>
    internal string EntryId { get; }

    /// <summary>The explicit structural classification of this entry.</summary>
    internal HarnessContextEntryKind Kind { get; }

    /// <summary>
    /// A defensively-copied <see cref="ChatMessage"/>, freshly cloned on every access: a new
    /// instance with its own copy of <see cref="ChatMessage.Contents"/>, never the exact instance
    /// stored by this entry or returned by any previous access. This means no caller — including a
    /// reducer that casts a returned message and mutates its contents list in place, or another
    /// consumer holding an earlier <see cref="Message"/> reference — can ever mutate this entry's
    /// own authoritative stored state through a value obtained from this property.
    /// </summary>
    internal ChatMessage Message => CloneMessage(_message);

    /// <summary>
    /// Every canonical lowercase hex SHA-256 artifact-reference digest this entry structurally carries,
    /// defensively copied and never empty-but-null. Populated as follows: exactly one digest (parsed
    /// from the message's canonical reference text) when <see cref="Kind"/> is
    /// <see cref="HarnessContextEntryKind.ArtifactReference"/>; exactly one digest (taken directly from
    /// <see cref="RecoverableSegment"/>'s reference) when <see cref="Kind"/> is
    /// <see cref="HarnessContextEntryKind.RecoverableContextSegment"/>; zero or more digests — one per
    /// <see cref="FunctionResultContent.Result"/> that structurally is a canonical
    /// <c>artifact://sha256/{64 lowercase hex}</c> reference (a bare <see cref="string"/> or a
    /// string-valued <see cref="JsonElement"/> in exactly that shape — never inferred from surrounding
    /// prose) — when <see cref="Kind"/> is <see cref="HarnessContextEntryKind.ToolExchange"/> and this
    /// entry carries one or more <see cref="FunctionResultContent"/> items; and empty for every other
    /// kind.
    /// </summary>
    internal IReadOnlyList<string> ArtifactReferenceDigests { get; }

    /// <summary>
    /// A single convenience digest, non-<see langword="null"/> only when <see cref="ArtifactReferenceDigests"/>
    /// carries exactly one coherent digest. A <see cref="HarnessContextEntryKind.ToolExchange"/> result
    /// entry whose payload structurally carries more than one canonical reference (or none at all)
    /// reports <see langword="null"/> here — callers that need every carried digest must read
    /// <see cref="ArtifactReferenceDigests"/> directly rather than assume a single-digest shape.
    /// </summary>
    internal string? ArtifactReferenceDigest =>
        ArtifactReferenceDigests.Count == 1 ? ArtifactReferenceDigests[0] : null;

    /// <summary>
    /// The marked recoverable rehydration segment — the canonical artifact reference identity it came
    /// from, plus its resolved body in <see cref="HarnessArtifactRecoverableContextSegment"/>'s own
    /// immutable shape — this entry carries. Non-<see langword="null"/> only when <see cref="Kind"/> is
    /// <see cref="HarnessContextEntryKind.RecoverableContextSegment"/>.
    /// </summary>
    internal HarnessArtifactRecoverableContextSegment? RecoverableSegment { get; }

    /// <exception cref="ArgumentNullException">
    /// <paramref name="entryId"/> or <paramref name="message"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="entryId"/> is empty or whitespace-only; <paramref name="kind"/> is
    /// <see cref="HarnessContextEntryKind.ArtifactReference"/> and <paramref name="message"/>'s text is
    /// not exactly one canonical <c>artifact://sha256/{64 lowercase hex}</c> reference;
    /// <paramref name="kind"/> is <see cref="HarnessContextEntryKind.ToolExchange"/> and
    /// <paramref name="message"/> carries no <see cref="FunctionCallContent"/> or
    /// <see cref="FunctionResultContent"/>, or carries both a call and a result; a call-bearing
    /// <see cref="HarnessContextEntryKind.ToolExchange"/> entry's message role is not
    /// <see cref="ChatRole.Assistant"/>; a result-bearing
    /// <see cref="HarnessContextEntryKind.ToolExchange"/> entry's message role is not
    /// <see cref="ChatRole.Tool"/>; or <paramref name="kind"/> is not
    /// <see cref="HarnessContextEntryKind.ToolExchange"/> and <paramref name="message"/> carries any
    /// <see cref="FunctionCallContent"/> or <see cref="FunctionResultContent"/>.
    /// </exception>
    /// <exception cref="NotSupportedException">
    /// A <see cref="FunctionCallContent.Arguments"/> value or a <see cref="FunctionResultContent.Result"/>
    /// value has a type <see cref="NormalizeValue"/> does not support. See its documented shapes.
    /// </exception>
    internal static HarnessContextEntry Create(string entryId, HarnessContextEntryKind kind, ChatMessage message)
    {
        ArgumentNullException.ThrowIfNull(entryId);
        ArgumentNullException.ThrowIfNull(message);

        if (string.IsNullOrWhiteSpace(entryId))
        {
            throw new ArgumentException("A non-empty, non-whitespace entry id is required.", nameof(entryId));
        }

        var copiedMessage = CloneMessage(message);

        var hasCall = copiedMessage.Contents.Any(content => content is FunctionCallContent);
        var hasResult = copiedMessage.Contents.Any(content => content is FunctionResultContent);

        if (kind != HarnessContextEntryKind.ToolExchange)
        {
            if (hasCall || hasResult)
            {
                throw new ArgumentException(
                    $"A '{kind}' entry's message must not contain any FunctionCallContent or " +
                    "FunctionResultContent. Tool call/result content is only permitted in a " +
                    $"{nameof(HarnessContextEntryKind.ToolExchange)} entry, never smuggled in under " +
                    "another label.",
                    nameof(message));
            }
        }

        IReadOnlyList<string> artifactReferenceDigests = [];

        switch (kind)
        {
            case HarnessContextEntryKind.ArtifactReference:
                var text = copiedMessage.Text;
                if (string.IsNullOrEmpty(text) || !HarnessArtifactIdentity.TryParseReferenceId(text, out var digest))
                {
                    throw new ArgumentException(
                        "An artifact-reference entry's message text must be exactly one canonical " +
                        "'artifact://sha256/{64 lowercase hex}' reference, not a bare path or an arbitrary URI.",
                        nameof(message));
                }

                artifactReferenceDigests = [digest];
                break;

            case HarnessContextEntryKind.ToolExchange:
                if (!hasCall && !hasResult)
                {
                    throw new ArgumentException(
                        "A tool-exchange entry's message must contain at least one FunctionCallContent " +
                        "or FunctionResultContent.",
                        nameof(message));
                }

                if (hasCall && hasResult)
                {
                    throw new ArgumentException(
                        "A tool-exchange entry's message must contain either FunctionCallContent or " +
                        "FunctionResultContent, never both in the same entry, so a downstream reader is " +
                        "never left to guess whether the entry represents a call or a result first.",
                        nameof(message));
                }

                if (hasCall && copiedMessage.Role != ChatRole.Assistant)
                {
                    throw new ArgumentException(
                        "A call-bearing tool-exchange entry's message must use ChatRole.Assistant " +
                        $"to maintain MEAI role coherence; received ChatRole '{copiedMessage.Role}'. " +
                        "User-role and system-role messages may never carry function-call content.",
                        nameof(message));
                }

                if (hasResult && copiedMessage.Role != ChatRole.Tool)
                {
                    throw new ArgumentException(
                        "A result-bearing tool-exchange entry's message must use ChatRole.Tool " +
                        $"to maintain MEAI role coherence; received ChatRole '{copiedMessage.Role}'. " +
                        "User-role and system-role messages may never carry function-result content.",
                        nameof(message));
                }

                if (hasResult)
                {
                    // A real-shape eager-offloaded tool result carries the canonical
                    // 'artifact://sha256/{digest}' reference string as FunctionResultContent.Result — the
                    // message stays ToolExchange for sequence validation, while these digests let the
                    // preservation policy and eviction logic treat it as durable, reference-bearing
                    // context too. Structural inspection only, never prose parsing.
                    artifactReferenceDigests = ExtractResultReferenceDigests(copiedMessage);
                }

                break;

            case HarnessContextEntryKind.RecoverableContextSegment:
                throw new ArgumentException(
                    $"A '{nameof(HarnessContextEntryKind.RecoverableContextSegment)}' entry must be " +
                    $"constructed via {nameof(CreateRecoverableSegment)}, which carries the canonical " +
                    "artifact reference identity and immutable rehydrated body this generic factory " +
                    "cannot validate from a bare message.",
                    nameof(kind));
        }

        return new HarnessContextEntry(entryId, kind, copiedMessage, artifactReferenceDigests, recoverableSegment: null);
    }

    /// <summary>
    /// Every canonical artifact-reference digest structurally carried by <paramref name="message"/>'s
    /// <see cref="FunctionResultContent"/> items, in declaration order. Only the exact shapes a real
    /// eager-offload result actually emits are recognized: a bare <see cref="string"/>
    /// <see cref="FunctionResultContent.Result"/> that is itself one canonical reference, or a
    /// string-valued <see cref="JsonElement"/> carrying the same. Anything else — including a bare
    /// workspace path, an arbitrary URI, or a non-string payload — is never treated as a reference.
    /// </summary>
    private static IReadOnlyList<string> ExtractResultReferenceDigests(ChatMessage message)
    {
        List<string>? digests = null;
        foreach (var result in message.Contents.OfType<FunctionResultContent>())
        {
            if (TryGetReferenceDigestFromResult(result.Result, out var digest))
            {
                digests ??= [];
                digests.Add(digest);
            }
        }

        return digests ?? (IReadOnlyList<string>)[];
    }

    /// <summary>
    /// Structurally recognizes a canonical artifact reference carried by a <see cref="FunctionResultContent.Result"/>
    /// payload: either a bare <see cref="string"/>, or a string-valued <see cref="JsonElement"/> (the
    /// shape <see cref="NormalizeValue"/> stores a result's string payload as after a round trip through
    /// source-generated JSON serialization). Never attempts to infer intent from surrounding prose or
    /// from a non-string payload shape.
    /// </summary>
    private static bool TryGetReferenceDigestFromResult(object? result, out string digest)
    {
        switch (result)
        {
            case string text:
                return HarnessArtifactIdentity.TryParseReferenceId(text, out digest);

            case JsonElement { ValueKind: JsonValueKind.String } element:
                var elementText = element.GetString();
                if (elementText is not null)
                {
                    return HarnessArtifactIdentity.TryParseReferenceId(elementText, out digest);
                }

                digest = string.Empty;
                return false;

            default:
                digest = string.Empty;
                return false;
        }
    }

    /// <summary>
    /// Constructs a <see cref="HarnessContextEntryKind.RecoverableContextSegment"/> entry directly from
    /// a recoverable rehydration segment. This is the only path that can construct
    /// this kind — the generic <see cref="Create"/> factory rejects it, because this kind's canonical
    /// data model is <paramref name="segment"/> itself (the exact artifact reference identity plus its
    /// resolved body, already carried in <see cref="HarnessArtifactRecoverableContextSegment"/>'s own
    /// immutable shape), never an arbitrary caller-supplied <see cref="ChatMessage"/> this factory would
    /// otherwise have to trust. This never converts the body into a reference — a durable
    /// <see cref="HarnessContextEntryKind.ArtifactReference"/> entry for the same digest, if present, is
    /// an entirely separate entry that this factory neither creates nor depends on.
    /// </summary>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="entryId"/> or <paramref name="segment"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException"><paramref name="entryId"/> is empty or whitespace-only.</exception>
    internal static HarnessContextEntry CreateRecoverableSegment(
        string entryId, HarnessArtifactRecoverableContextSegment segment)
    {
        ArgumentNullException.ThrowIfNull(entryId);
        ArgumentNullException.ThrowIfNull(segment);

        if (string.IsNullOrWhiteSpace(entryId))
        {
            throw new ArgumentException("A non-empty, non-whitespace entry id is required.", nameof(entryId));
        }

        // ChatRole.User, never ChatRole.Tool and never ChatRole.System: a transient recovered body has
        // no correlating FunctionCallContent/FunctionResultContent pair of its own, so dispatching it
        // under ChatRole.Tool would be an orphan tool-role message a real provider can validly reject.
        // ChatRole.User is the non-privileged, always provider-valid role for arbitrary transient
        // context; ChatRole.System is never used here because this content is neither pinned nor
        // authoritative instruction. The body is preserved exactly, unwrapped.
        var message = new ChatMessage(ChatRole.User, segment.Body);

        return new HarnessContextEntry(
            entryId,
            HarnessContextEntryKind.RecoverableContextSegment,
            message,
            [segment.Reference.ContentDigest],
            segment);
    }

    /// <summary>
    /// Returns a new entry sharing this entry's exact <see cref="EntryId"/>, <see cref="Kind"/>,
    /// <see cref="ArtifactReferenceDigests"/>, and <see cref="RecoverableSegment"/> (already
    /// immutable value data), but with an independently, deeply defensively-copied
    /// <see cref="Message"/> — never the same underlying <see cref="ChatMessage"/> instance or
    /// content list as this entry or as any other <see cref="Copy"/> result. Used to seal every
    /// snapshot/request/result boundary a <see cref="HarnessContextEntry"/> crosses (see
    /// <see cref="HarnessContextSnapshot.Create"/>, <see cref="HarnessContextReductionRequest.Create"/>,
    /// and <see cref="HarnessContextAssemblyResult"/>'s factories) so that a mutation performed
    /// through one boundary's copy — for example a reducer that casts a reduction request's entry
    /// and edits its message's contents list in place — can never reach another boundary's
    /// authoritative copy, including the assembler's own snapshot-derived entries.
    /// </summary>
    internal HarnessContextEntry Copy() =>
        new HarnessContextEntry(EntryId, Kind, CloneMessage(_message), ArtifactReferenceDigests, RecoverableSegment);

    /// <summary>
    /// Every canonical artifact-reference digest treated as durable — independently preservable by some
    /// entry other than a <see cref="HarnessContextEntryKind.RecoverableContextSegment"/> itself — across
    /// <paramref name="entries"/>: every <see cref="ArtifactReferenceDigests"/> value of a standalone
    /// <see cref="HarnessContextEntryKind.ArtifactReference"/> entry, plus every
    /// <see cref="ArtifactReferenceDigests"/> value of a <see cref="HarnessContextEntryKind.ToolExchange"/>
    /// entry whose <see cref="FunctionResultContent.Result"/> payload structurally carries one or more
    /// canonical references. A <see cref="HarnessContextEntryKind.RecoverableContextSegment"/> entry's
    /// own digest is deliberately never included: this set exists to answer whether some other,
    /// non-recoverable entry already durably carries a given digest, not to make a recoverable segment
    /// durable with itself.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="entries"/> is <see langword="null"/>.</exception>
    internal static HashSet<string> CollectDurableArtifactReferenceDigests(IReadOnlyList<HarnessContextEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var digests = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            if (entry.Kind is HarnessContextEntryKind.ArtifactReference or HarnessContextEntryKind.ToolExchange)
            {
                foreach (var digest in entry.ArtifactReferenceDigests)
                {
                    digests.Add(digest);
                }
            }
        }

        return digests;
    }

    /// <summary>
    /// Builds a fresh <see cref="ChatMessage"/> with its own independently-copied
    /// <see cref="ChatMessage.Contents"/> list (via <see cref="CopyContent"/>) and copied settable
    /// metadata, so a caller mutating <paramref name="message"/> (or its contents list) after this
    /// method returns can never change what the returned instance reports.
    /// </summary>
    private static ChatMessage CloneMessage(ChatMessage message)
    {
        var copiedContents = message.Contents.Select(CopyContent).ToList();
        return new ChatMessage(message.Role, copiedContents)
        {
            AuthorName = message.AuthorName,
            MessageId = message.MessageId,
            CreatedAt = message.CreatedAt,
        };
    }

    /// <summary>
    /// Reconstructs a fresh <see cref="AIContent"/> instance for every content shape this entry
    /// classification cares about (<see cref="FunctionCallContent"/>, <see cref="FunctionResultContent"/>,
    /// <see cref="TextContent"/>), deep-normalizing their mutable payload properties via
    /// <see cref="NormalizeValue"/> so a caller mutating the original content instance or any nested
    /// mutable value after <see cref="Create"/> returns can never change what this entry reports.
    /// Each type's <c>CallId</c> (and <see cref="FunctionCallContent.Name"/>) is read-only on the
    /// source types already. There is no reflection-based, pass-through-by-reference fallback for an
    /// unrecognized <see cref="AIContent"/> shape: an unsupported type throws
    /// <see cref="NotSupportedException"/> instead of being forwarded as the exact caller-controlled
    /// mutable instance, so an unverifiable reference can never enter preserved context under a
    /// shape this type cannot itself defensively copy.
    /// </summary>
    /// <exception cref="NotSupportedException">
    /// <paramref name="content"/>'s runtime type is not <see cref="FunctionCallContent"/>,
    /// <see cref="FunctionResultContent"/>, or <see cref="TextContent"/>.
    /// </exception>
    private static AIContent CopyContent(AIContent content) => content switch
    {
        FunctionCallContent call => new FunctionCallContent(
            call.CallId,
            call.Name,
            call.Arguments is null ? null : NormalizeArgumentDictionary(call.Arguments))
        {
            Exception = call.Exception,
            InformationalOnly = call.InformationalOnly,
            Annotations = CopyAnnotations(call.Annotations),
            RawRepresentation = call.RawRepresentation,
            AdditionalProperties = CopyAdditionalProperties(call.AdditionalProperties),
        },
        FunctionResultContent result => new FunctionResultContent(result.CallId, NormalizeValue(result.Result))
        {
            Exception = result.Exception,
            Annotations = CopyAnnotations(result.Annotations),
            RawRepresentation = result.RawRepresentation,
            AdditionalProperties = CopyAdditionalProperties(result.AdditionalProperties),
        },
        TextContent text => new TextContent(text.Text)
        {
            Annotations = CopyAnnotations(text.Annotations),
            RawRepresentation = text.RawRepresentation,
            AdditionalProperties = CopyAdditionalProperties(text.AdditionalProperties),
        },
        _ => throw new NotSupportedException(
            $"AIContent type '{content.GetType()}' is not a supported closed-world content shape. " +
            $"Supported shapes are: {nameof(FunctionCallContent)}, {nameof(FunctionResultContent)}, " +
            $"and {nameof(TextContent)}, each deep-copied explicitly. An unrecognized content type " +
            "is rejected rather than passed through by reference, so an unverifiable mutable " +
            "instance this type cannot defensively copy can never enter preserved context."),
    };

    /// <summary>
    /// Returns an immutable normalized snapshot of <paramref name="value"/> so a caller mutating
    /// the original object after <see cref="Create"/> returns cannot change what this entry stores.
    /// There is no reflection-based fallback: an unsupported type throws
    /// <see cref="NotSupportedException"/> rather than being serialized by
    /// <see cref="JsonSerializer.SerializeToUtf8Bytes(object?, Type, JsonSerializerOptions?)"/>'s
    /// reflection-based (and NativeAOT-unsafe) unknown-object overload, so an unrecognized type can
    /// never enter preserved context under an unverifiable shape.
    /// </summary>
    /// <remarks>
    /// Supported types, in matching order:
    /// <list type="bullet">
    ///   <item><see langword="null"/> — returned as-is.</item>
    ///   <item><see cref="string"/> and common primitive value types — returned as-is (already immutable).</item>
    ///   <item><see cref="JsonElement"/> — cloned via <see cref="JsonElement.Clone"/> to own its backing document.</item>
    ///   <item><see cref="IDictionary{TKey,TValue}"/> of string to <see langword="object"/> — each value is normalized recursively into a new <see cref="Dictionary{TKey,TValue}"/>.</item>
    ///   <item><see cref="IList{T}"/> of <see langword="object"/> (including arrays) — each element is normalized recursively into a new <see cref="List{T}"/>.</item>
    /// </list>
    /// Anything else — including an arbitrary custom object graph — is unsupported and throws
    /// <see cref="NotSupportedException"/> before entering preserved context. Callers must convert such
    /// a value to one of the explicit shapes above (for example a <see cref="JsonElement"/> obtained
    /// from a type-safe, source-generated serialization) before calling <see cref="Create"/>.
    /// </remarks>
    /// <exception cref="NotSupportedException">
    /// <paramref name="value"/>'s runtime type is not one of the documented supported shapes.
    /// </exception>
    internal static object? NormalizeValue(object? value)
    {
        if (value is null) return null;

        if (value is string or bool or int or long or double or float
            or short or byte or uint or ulong or decimal)
        {
            return value;
        }

        if (value is JsonElement je)
        {
            return je.Clone();
        }

        if (value is IDictionary<string, object?> dict)
        {
            return NormalizeArgumentDictionary(dict);
        }

        if (value is IList<object?> list)
        {
            var copy = new List<object?>(list.Count);
            foreach (var item in list)
            {
                copy.Add(NormalizeValue(item));
            }

            return copy;
        }

        throw new NotSupportedException(
            $"Type '{value.GetType()}' is not a supported normalized value shape. Supported shapes are: " +
            "null, string, common primitive value types, JsonElement, IDictionary<string, object?>, and " +
            "IList<object?> (including arrays), normalized recursively. Convert the value to one of these " +
            "explicit, AOT-safe shapes before it is stored in preserved context.");
    }

    private static Dictionary<string, object?> NormalizeArgumentDictionary(IDictionary<string, object?> source)
    {
        var copy = new Dictionary<string, object?>(source.Count, StringComparer.Ordinal);
        foreach (var kvp in source)
        {
            copy[kvp.Key] = NormalizeValue(kvp.Value);
        }

        return copy;
    }

    private static IList<AIAnnotation>? CopyAnnotations(IList<AIAnnotation>? annotations) =>
        annotations is null ? null : new List<AIAnnotation>(annotations);

    private static AdditionalPropertiesDictionary? CopyAdditionalProperties(
        AdditionalPropertiesDictionary? additionalProperties) =>
        additionalProperties is null ? null : new AdditionalPropertiesDictionary(additionalProperties);
}
