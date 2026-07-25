namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Context;

/// <summary>
/// Categorical reason <see cref="HarnessCompactionVerifier"/> rejected a proposed reduced entry set.
/// A <see cref="HarnessCompactionVerificationResult"/> may carry more than one of these at once.
/// </summary>
internal enum HarnessCompactionRejectionReason
{
    /// <summary>A required entry id is entirely absent from the proposed entries.</summary>
    MissingRequiredEntry,

    /// <summary>Required entries are all present but not in their original relative order.</summary>
    RequiredEntryOutOfOrder,

    /// <summary>A required entry is present but its message content no longer matches the original.</summary>
    RequiredEntryContentMismatch,

    /// <summary>A proposed function call has no matching function result anywhere in the proposed entries.</summary>
    OrphanedToolCall,

    /// <summary>A proposed function result has no matching function call anywhere in the proposed entries.</summary>
    OrphanedToolResult,

    /// <summary>The same function call id is declared by more than one call-bearing entry.</summary>
    DuplicateToolCall,

    /// <summary>The same function call id has a result declared by more than one result-bearing entry.</summary>
    DuplicateToolResult,

    /// <summary>A tool exchange's result(s) all exist but appear at or before its call in the proposed entries.</summary>
    ReorderedToolGroup,

    /// <summary>
    /// A proposed entry id that never existed in the original entries claims a structural kind
    /// (<see cref="HarnessContextEntryKind.SystemInstruction"/>,
    /// <see cref="HarnessContextEntryKind.AuthoritativeSessionState"/>,
    /// <see cref="HarnessContextEntryKind.ApprovalSecurityState"/>,
    /// <see cref="HarnessContextEntryKind.ArtifactReference"/>, or
    /// <see cref="HarnessContextEntryKind.ToolExchange"/>) that only an original entry may carry.
    /// </summary>
    ForgedStructuralEntry,

    /// <summary>
    /// A proposed entry that reuses an original entry's id has had its
    /// <see cref="HarnessContextEntryKind"/>, message role, author name, or structural payload
    /// (text, function-call name/arguments/call-id, function-result payload/call-id) changed. A
    /// reducer may remove an eligible original entry or introduce a brand-new entry under a new id
    /// (<see cref="HarnessContextEntryKind.ConversationalMessage"/> or
    /// <see cref="HarnessContextEntryKind.Summary"/> kinds only), but may never mutate an entry it
    /// chooses to retain.
    /// </summary>
    RetainedOriginalEntryMutated,
}
