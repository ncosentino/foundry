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
        string? artifactReferenceDigest,
        HarnessArtifactRecoverableContextSegment? recoverableSegment)
    {
        EntryId = entryId;
        Kind = kind;
        _message = message;
        ArtifactReferenceDigest = artifactReferenceDigest;
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
    /// The lowercase hex SHA-256 digest identifying this entry's artifact. Non-<see langword="null"/>
    /// only when <see cref="Kind"/> is <see cref="HarnessContextEntryKind.ArtifactReference"/> (parsed
    /// from the message's canonical reference text) or <see cref="HarnessContextEntryKind.RecoverableContextSegment"/>
    /// (taken directly from <see cref="RecoverableSegment"/>'s reference).
    /// </summary>
    internal string? ArtifactReferenceDigest { get; }

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
    /// <see cref="FunctionResultContent"/>, or carries both a call and a result; or
    /// <paramref name="kind"/> is not <see cref="HarnessContextEntryKind.ToolExchange"/> and
    /// <paramref name="message"/> carries any <see cref="FunctionCallContent"/> or
    /// <see cref="FunctionResultContent"/>.
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

        string? artifactReferenceDigest = null;

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

                artifactReferenceDigest = digest;
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

                break;

            case HarnessContextEntryKind.RecoverableContextSegment:
                throw new ArgumentException(
                    $"A '{nameof(HarnessContextEntryKind.RecoverableContextSegment)}' entry must be " +
                    $"constructed via {nameof(CreateRecoverableSegment)}, which carries the canonical " +
                    "artifact reference identity and immutable rehydrated body this generic factory " +
                    "cannot validate from a bare message.",
                    nameof(kind));
        }

        return new HarnessContextEntry(entryId, kind, copiedMessage, artifactReferenceDigest, recoverableSegment: null);
    }

    /// <summary>
    /// Constructs a <see cref="HarnessContextEntryKind.RecoverableContextSegment"/> entry directly from
    /// a G4 rehydration primitive's marked recoverable segment. This is the only path that can construct
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

        var message = new ChatMessage(ChatRole.Tool, segment.Body);

        return new HarnessContextEntry(
            entryId,
            HarnessContextEntryKind.RecoverableContextSegment,
            message,
            segment.Reference.ContentDigest,
            segment);
    }

    /// <summary>
    /// Returns a new entry sharing this entry's exact <see cref="EntryId"/>, <see cref="Kind"/>,
    /// <see cref="ArtifactReferenceDigest"/>, and <see cref="RecoverableSegment"/> (already
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
        new HarnessContextEntry(EntryId, Kind, CloneMessage(_message), ArtifactReferenceDigest, RecoverableSegment);

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
