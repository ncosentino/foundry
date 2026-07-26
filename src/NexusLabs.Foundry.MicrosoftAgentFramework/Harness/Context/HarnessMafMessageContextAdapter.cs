using Microsoft.Extensions.AI;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Context;

/// <summary>
/// Narrow, stateless adapter from a MAF/MEAI <see cref="ChatMessage"/> list — the exact messages a
/// <see cref="HarnessHybridCompactionChatClient"/> observes for one provider request — into the
/// <see cref="HarnessContextEntry"/> model <see cref="HarnessContextAssembler"/> already reasons about.
/// This adapter adapts only the actual request messages: tool structures, explicit host-authored labels,
/// canonical artifact references, and ordinary conversational/summary content. It never guesses structural
/// kind from prose, and it never turns an ordinary incoming message into a
/// <see cref="HarnessContextEntryKind.RecoverableContextSegment"/> entry — the only legitimate way a
/// recoverable segment ever enters a snapshot is <see cref="HarnessContextSnapshotIntegration"/> augmenting
/// the already-adapted baseline entries at the inner compaction seam, never this adapter inferring one from
/// the request messages it was handed.
/// </summary>
/// <remarks>
/// Classification precedence, in order:
/// <list type="number">
///   <item>
///     A message carrying any <see cref="FunctionCallContent"/> or <see cref="FunctionResultContent"/>
///     is always <see cref="HarnessContextEntryKind.ToolExchange"/>. The classifier is never consulted
///     for such a message.
///   </item>
///   <item>
///     A message with <see cref="ChatRole.System"/> is always
///     <see cref="HarnessContextEntryKind.SystemInstruction"/>. System instructions are structurally
///     inherent in the message's own role and can never be downgraded or overridden by the host
///     classifier. <see cref="IHarnessContextMessageClassifier.ClassifyOverride"/> is never consulted
///     for a system-role message.
///   </item>
///   <item>
///     <see cref="IHarnessContextMessageClassifier.ClassifyOverride"/>, if it returns
///     non-<see langword="null"/>, is used as-is — except <see cref="HarnessContextEntryKind.ToolExchange"/>,
///     <see cref="HarnessContextEntryKind.RecoverableContextSegment"/>, and
///     <see cref="HarnessContextEntryKind.SystemInstruction"/>, which a classifier may never assign
///     this way (see this adapter's <see cref="Adapt"/> exception documentation).
///   </item>
///   <item>
///     A message whose entire text is one canonical <c>artifact://sha256/{64 lowercase hex}</c>
///     reference is <see cref="HarnessContextEntryKind.ArtifactReference"/>.
///   </item>
///   <item>Otherwise, <see cref="HarnessContextEntryKind.ConversationalMessage"/>.</item>
/// </list>
/// </remarks>
internal static class HarnessMafMessageContextAdapter
{
    /// <exception cref="ArgumentNullException"><paramref name="messages"/> or <paramref name="classifier"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">
    /// <paramref name="classifier"/>'s <see cref="IHarnessContextMessageClassifier.ClassifyOverride"/>
    /// returned <see cref="HarnessContextEntryKind.ToolExchange"/>,
    /// <see cref="HarnessContextEntryKind.RecoverableContextSegment"/>, or
    /// <see cref="HarnessContextEntryKind.SystemInstruction"/> for a message. Those three kinds are
    /// derived only from a message's own structural shape or role, never from host opinion.
    /// </exception>
    internal static IReadOnlyList<HarnessContextEntry> Adapt(
        IReadOnlyList<ChatMessage> messages, IHarnessContextMessageClassifier classifier)
    {
        ArgumentNullException.ThrowIfNull(messages);
        ArgumentNullException.ThrowIfNull(classifier);

        var entries = new List<HarnessContextEntry>(messages.Count);
        for (var index = 0; index < messages.Count; index++)
        {
            entries.Add(AdaptOne(messages[index], index, messages, classifier));
        }

        return entries;
    }

    private static HarnessContextEntry AdaptOne(
        ChatMessage message,
        int index,
        IReadOnlyList<ChatMessage> allMessages,
        IHarnessContextMessageClassifier classifier)
    {
        var entryId = classifier.ResolveEntryId(message, index, allMessages);

        var hasToolContent = message.Contents.Any(
            content => content is FunctionCallContent or FunctionResultContent);
        if (hasToolContent)
        {
            return HarnessContextEntry.Create(entryId, HarnessContextEntryKind.ToolExchange, message);
        }

        // System instructions are structurally inherent in the message's own role: a ChatRole.System
        // message always maps to SystemInstruction, regardless of what the classifier would return.
        // ClassifyOverride is never consulted for system-role messages so the host classifier cannot
        // downgrade or override this structural assignment.
        if (message.Role == ChatRole.System)
        {
            return HarnessContextEntry.Create(entryId, HarnessContextEntryKind.SystemInstruction, message);
        }

        var overrideKind = classifier.ClassifyOverride(message, index, allMessages);
        if (overrideKind is HarnessContextEntryKind.ToolExchange
            or HarnessContextEntryKind.RecoverableContextSegment
            or HarnessContextEntryKind.SystemInstruction)
        {
            throw new InvalidOperationException(
                $"The configured classifier assigned the structural-only kind '{overrideKind}' to a " +
                "message via ClassifyOverride. ToolExchange, RecoverableContextSegment, and " +
                "SystemInstruction are derived only from a message's own content shape or role, " +
                "never from host opinion.");
        }

        if (overrideKind is HarnessContextEntryKind kind)
        {
            return HarnessContextEntry.Create(entryId, kind, message);
        }

        var text = message.Text;
        if (!string.IsNullOrEmpty(text) && HarnessArtifactIdentity.TryParseReferenceId(text, out _))
        {
            return HarnessContextEntry.Create(entryId, HarnessContextEntryKind.ArtifactReference, message);
        }

        return HarnessContextEntry.Create(entryId, HarnessContextEntryKind.ConversationalMessage, message);
    }
}
