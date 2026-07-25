namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Context;

/// <summary>
/// Structural classification of one <see cref="HarnessContextEntry"/>. A caller assigns this label
/// explicitly when the entry is constructed — it is never inferred by parsing message text — so a
/// preservation policy can decide what to keep by category rather than by guessing at prose content.
/// </summary>
internal enum HarnessContextEntryKind
{
    /// <summary>Pinned system instructions. Always required and never rewritten by compaction.</summary>
    SystemInstruction,

    /// <summary>
    /// Host-authoritative structured session state (for example accepted decisions, todos, or mode).
    /// Always required. Never sourced from, or superseded by, a <see cref="Summary"/> entry.
    /// </summary>
    AuthoritativeSessionState,

    /// <summary>
    /// Active approval or security state. Always required. Never sourced from, or superseded by, a
    /// <see cref="Summary"/> entry.
    /// </summary>
    ApprovalSecurityState,

    /// <summary>
    /// A message that is part of an atomic assistant tool-call/tool-result exchange (carries at least
    /// one function-call or function-result content item). Grouped and validated as a unit —
    /// see <see cref="HarnessToolExchangeGroup"/> and <see cref="HarnessToolExchangeAnalysis"/>.
    /// </summary>
    ToolExchange,

    /// <summary>
    /// A message whose entire text is one canonical <c>artifact://sha256/{64 lowercase hex}</c>
    /// reference. Bounded and always required, without ever rehydrating the referenced body.
    /// </summary>
    ArtifactReference,

    /// <summary>
    /// An ordinary conversational message. Reducible: required only when it falls inside the
    /// configured recent-message retention window.
    /// </summary>
    ConversationalMessage,

    /// <summary>
    /// A summary of previously reduced/compacted history. Always reducible and never required —
    /// specifically, a summary can never substitute for a required <see cref="AuthoritativeSessionState"/>
    /// or <see cref="ApprovalSecurityState"/> entry.
    /// </summary>
    Summary,
}
