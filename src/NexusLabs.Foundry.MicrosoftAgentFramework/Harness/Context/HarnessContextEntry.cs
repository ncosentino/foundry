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
/// this entry reports.
/// </para>
/// <para>
/// <strong>Argument and result normalization.</strong> <see cref="FunctionCallContent.Arguments"/>
/// values and <see cref="FunctionResultContent.Result"/> are normalized to an immutable snapshot by
/// <see cref="NormalizeValue"/> before storage. Supported value types — <see langword="null"/>,
/// <see cref="string"/>, common primitive value types, <see cref="JsonElement"/>, and
/// <see cref="IDictionary{TKey,TValue}"/> of string to <see langword="object"/> — are deep-copied
/// recursively so a caller mutating a nested dictionary or list after <see cref="Create"/> returns
/// cannot change what this entry reports. Any other object graph is serialized to a UTF-8 JSON
/// snapshot and parsed back as an owned <see cref="JsonElement"/>; types that cannot be serialized
/// by <see cref="JsonSerializer"/> will propagate the serializer's own exception, and NativeAOT
/// callers must ensure the relevant type metadata is registered.
/// </para>
/// <para>
/// <strong>Structural validation, not prose parsing.</strong> <see cref="HarnessContextEntryKind.ArtifactReference"/>
/// and <see cref="HarnessContextEntryKind.ToolExchange"/> are the two kinds with a verifiable structural
/// shape, so <see cref="Create"/> enforces that shape directly: an artifact-reference entry's exact text
/// must be one canonical <c>artifact://sha256/{64 lowercase hex}</c> reference (never a bare path or an
/// arbitrary URI), and a tool-exchange entry's message must carry at least one <see cref="FunctionCallContent"/>
/// or <see cref="FunctionResultContent"/>. Every other kind is an explicit caller label with no further
/// shape requirement.
/// </para>
/// </remarks>
internal sealed record HarnessContextEntry
{
    private HarnessContextEntry(
        string entryId,
        HarnessContextEntryKind kind,
        ChatMessage message,
        string? artifactReferenceDigest)
    {
        EntryId = entryId;
        Kind = kind;
        Message = message;
        ArtifactReferenceDigest = artifactReferenceDigest;
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
    /// A defensively-copied <see cref="ChatMessage"/>: a new instance with its own copy of
    /// <see cref="ChatMessage.Contents"/>, never the exact instance/list passed to <see cref="Create"/>.
    /// </summary>
    internal ChatMessage Message { get; }

    /// <summary>
    /// The lowercase hex SHA-256 digest parsed from this entry's canonical artifact reference text.
    /// Non-<see langword="null"/> only when <see cref="Kind"/> is
    /// <see cref="HarnessContextEntryKind.ArtifactReference"/>.
    /// </summary>
    internal string? ArtifactReferenceDigest { get; }

    /// <exception cref="ArgumentNullException">
    /// <paramref name="entryId"/> or <paramref name="message"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="entryId"/> is empty or whitespace-only; <paramref name="kind"/> is
    /// <see cref="HarnessContextEntryKind.ArtifactReference"/> and <paramref name="message"/>'s text is
    /// not exactly one canonical <c>artifact://sha256/{64 lowercase hex}</c> reference; or
    /// <paramref name="kind"/> is <see cref="HarnessContextEntryKind.ToolExchange"/> and
    /// <paramref name="message"/> carries no <see cref="FunctionCallContent"/> or
    /// <see cref="FunctionResultContent"/>.
    /// </exception>
    internal static HarnessContextEntry Create(string entryId, HarnessContextEntryKind kind, ChatMessage message)
    {
        ArgumentNullException.ThrowIfNull(entryId);
        ArgumentNullException.ThrowIfNull(message);

        if (string.IsNullOrWhiteSpace(entryId))
        {
            throw new ArgumentException("A non-empty, non-whitespace entry id is required.", nameof(entryId));
        }

        var copiedContents = message.Contents.Select(CopyContent).ToList();
        var copiedMessage = new ChatMessage(message.Role, copiedContents)
        {
            AuthorName = message.AuthorName,
            MessageId = message.MessageId,
            CreatedAt = message.CreatedAt,
        };

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
                var hasCallOrResult = copiedMessage.Contents
                    .Any(content => content is FunctionCallContent or FunctionResultContent);
                if (!hasCallOrResult)
                {
                    throw new ArgumentException(
                        "A tool-exchange entry's message must contain at least one FunctionCallContent " +
                        "or FunctionResultContent.",
                        nameof(message));
                }

                break;
        }

        return new HarnessContextEntry(entryId, kind, copiedMessage, artifactReferenceDigest);
    }

    /// <summary>
    /// Reconstructs a fresh <see cref="AIContent"/> instance for every content shape this entry
    /// classification cares about (<see cref="FunctionCallContent"/>, <see cref="FunctionResultContent"/>,
    /// <see cref="TextContent"/>), deep-normalizing their mutable payload properties via
    /// <see cref="NormalizeValue"/> so a caller mutating the original content instance or any nested
    /// mutable value after <see cref="Create"/> returns can never change what this entry reports.
    /// Each type's <c>CallId</c> (and <see cref="FunctionCallContent.Name"/>) is read-only on the
    /// source types already. Content types this leaf does not construct or reason about structurally
    /// are passed through unchanged by reference; the immediate MEAI content wrapper is what this
    /// entry preserves defensively, not arbitrary payload objects an unrecognized content type might
    /// hold.
    /// </summary>
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
        _ => content,
    };

    /// <summary>
    /// Returns an immutable normalized snapshot of <paramref name="value"/> so a caller mutating
    /// the original object after <see cref="Create"/> returns cannot change what this entry stores.
    /// </summary>
    /// <remarks>
    /// Supported types, in matching order:
    /// <list type="bullet">
    ///   <item><see langword="null"/> — returned as-is.</item>
    ///   <item><see cref="string"/> and common primitive value types — returned as-is (already immutable).</item>
    ///   <item><see cref="JsonElement"/> — cloned via <see cref="JsonElement.Clone"/> to own its backing document.</item>
    ///   <item><see cref="IDictionary{TKey,TValue}"/> of string to <see langword="object"/> — each value is normalized recursively into a new <see cref="Dictionary{TKey,TValue}"/>.</item>
    ///   <item><see cref="IList{T}"/> of <see langword="object"/> — each element is normalized recursively into a new <see cref="List{T}"/>.</item>
    ///   <item>Anything else — serialized to a UTF-8 JSON snapshot and parsed back as an owned <see cref="JsonElement"/>. NativeAOT callers must ensure the type's metadata is registered with the serializer.</item>
    /// </list>
    /// </remarks>
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

        var json = JsonSerializer.SerializeToUtf8Bytes(value, value.GetType());
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
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
