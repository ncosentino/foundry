namespace NexusLabs.Foundry.MicrosoftAgentFramework.Diagnostics;

/// <summary>
/// Public, privacy-safe mirror of <c>Harness.Context.HarnessContextEntryKind</c>: the structural
/// classification a final context entry contributes to <see cref="HarnessContextDiagnostics.CategoryContributions"/>
/// under. Every value here corresponds 1:1 to an internal entry kind, so a per-category byte/entry
/// count can be reported without ever exposing entry content.
/// </summary>
public enum HarnessContextCategory
{
    /// <summary>Pinned system instructions.</summary>
    SystemInstruction,

    /// <summary>Host-authoritative structured session state (for example accepted decisions, todos, or mode).</summary>
    AuthoritativeSessionState,

    /// <summary>Active approval or security state.</summary>
    ApprovalSecurityState,

    /// <summary>A message that is part of an atomic assistant tool-call/tool-result exchange.</summary>
    ToolExchange,

    /// <summary>A message whose entire text is one canonical artifact reference.</summary>
    ArtifactReference,

    /// <summary>An ordinary conversational message.</summary>
    ConversationalMessage,

    /// <summary>A summary of previously reduced/compacted history.</summary>
    Summary,

    /// <summary>A rehydrated artifact body, marked recoverable/evictable.</summary>
    RecoverableContextSegment,

    /// <summary>Explicitly optional context.</summary>
    OptionalContext,
}
