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

    /// <summary>
    /// A rehydrated artifact body, marked recoverable per <see cref="HarnessArtifactRecoverableContextSegment"/>.
    /// Evicted ahead of any other reduction only when a durable <see cref="ArtifactReference"/> entry
    /// for the same canonical digest exists in the same snapshot — that separate reference entry
    /// remains independently preservable, so the body itself is safely droppable. When no such
    /// reference exists, <see cref="HarnessHybridContextPolicy.SelectRequiredPreservation"/> requires
    /// this entry instead of merely retaining it opportunistically: it is the only durable copy of that
    /// content, and dropping it can make the context <see cref="HarnessContextAssemblyOutcome.Irreducible"/>
    /// if it alone prevents the remaining entries from fitting the hard limit. Constructed only via
    /// <see cref="HarnessContextEntry.CreateRecoverableSegment"/>; the generic
    /// <see cref="HarnessContextEntry.Create"/> factory rejects this kind, because this kind's canonical
    /// data model is the segment itself, never an arbitrary caller-supplied message.
    /// </summary>
    RecoverableContextSegment,

    /// <summary>
    /// Explicitly optional context. Never required by <see cref="HarnessHybridContextPolicy.SelectRequiredPreservation"/>
    /// and never able to substitute for a required entry. A <see cref="HarnessContextAssembler"/>'s
    /// deterministic fallback step is the one place this kind is treated specially: it is included
    /// alongside the required entries in the assembler's first fallback attempt, and dropped — never in
    /// place of anything required — only if that attempt still exceeds the hard limit, immediately before
    /// the assembler would otherwise return an irreducible termination.
    /// </summary>
    OptionalContext,
}
