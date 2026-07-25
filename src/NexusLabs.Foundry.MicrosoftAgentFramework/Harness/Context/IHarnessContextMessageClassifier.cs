using Microsoft.Extensions.AI;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Context;

/// <summary>
/// Required host-authored strategy for turning one raw MAF/MEAI <see cref="ChatMessage"/> into a
/// <see cref="HarnessContextEntry"/>'s stable identity and, where the shape is not already structurally
/// self-evident, its <see cref="HarnessContextEntryKind"/>. There is no built-in default implementation:
/// a <see cref="HarnessHybridProfile"/> always requires an explicit instance, so a host never gets
/// implicit or guessed classification from message prose.
/// </summary>
/// <remarks>
/// <see cref="HarnessMafMessageContextAdapter.Adapt"/> only ever consults this strategy for the kinds
/// that are not inherent in a message's own content shape or role. A message carrying a
/// <see cref="FunctionCallContent"/> or <see cref="FunctionResultContent"/> is always classified
/// <see cref="HarnessContextEntryKind.ToolExchange"/> structurally; a message with
/// <see cref="ChatRole.System"/> is always classified
/// <see cref="HarnessContextEntryKind.SystemInstruction"/> — both are structural assignments and
/// <see cref="ClassifyOverride"/> is never consulted for either. A message whose entire text is
/// one canonical <c>artifact://sha256/{64 lowercase hex}</c> reference is always classified
/// <see cref="HarnessContextEntryKind.ArtifactReference"/> structurally — <see cref="ClassifyOverride"/>
/// is never consulted for that case either, and returning any of those three kinds from it is rejected
/// outright, because those kinds are never a matter of host opinion.
/// </remarks>
internal interface IHarnessContextMessageClassifier
{
    /// <summary>
    /// Resolves the stable <see cref="HarnessContextEntry.EntryId"/> for <paramref name="message"/> at
    /// <paramref name="index"/> within <paramref name="allMessages"/>. Must be a deterministic function
    /// of the message's own content — never of <see cref="ChatMessage.MessageId"/> alone (which may be
    /// absent), and never of the message instance's own reference identity — so that two structurally
    /// identical messages presented across two separate adaptations of the same content always resolve
    /// to the same id.
    /// </summary>
    string ResolveEntryId(ChatMessage message, int index, IReadOnlyList<ChatMessage> allMessages);

    /// <summary>
    /// Optionally overrides the structural default classification for <paramref name="message"/> at
    /// <paramref name="index"/> within <paramref name="allMessages"/>. Return <see langword="null"/> to
    /// defer to the adapter's structural default (<see cref="HarnessContextEntryKind.ArtifactReference"/>
    /// when recognized, otherwise <see cref="HarnessContextEntryKind.ConversationalMessage"/>).
    /// Returning <see cref="HarnessContextEntryKind.ToolExchange"/>,
    /// <see cref="HarnessContextEntryKind.RecoverableContextSegment"/>, or
    /// <see cref="HarnessContextEntryKind.SystemInstruction"/> is rejected — those three kinds are
    /// never a matter of host opinion: the first two are derived from the message's own content shape,
    /// and <see cref="HarnessContextEntryKind.SystemInstruction"/> is derived from
    /// <see cref="ChatRole.System"/>, which is itself structurally inherent; see this type's remarks.
    /// This method is never called at all for a message whose role is <see cref="ChatRole.System"/> —
    /// system-role messages always map to <see cref="HarnessContextEntryKind.SystemInstruction"/>
    /// before this strategy is consulted.
    /// </summary>
    HarnessContextEntryKind? ClassifyOverride(ChatMessage message, int index, IReadOnlyList<ChatMessage> allMessages);
}
