namespace NexusLabs.Foundry.MicrosoftAgentFramework.Diagnostics;

/// <summary>
/// Which of the two explicit artifact decisions a <see cref="HarnessArtifactDiagnostics"/>
/// snapshot describes. Every snapshot belongs to exactly one category, and each category has its
/// own closed set of valid <see cref="HarnessArtifactOutcomeCategory"/> and
/// <see cref="HarnessArtifactDecisionReason"/> values.
/// </summary>
public enum HarnessArtifactOperationCategory
{
    /// <summary>
    /// The decision was whether to inline or offload a tool invocation result to the workspace.
    /// </summary>
    Offload,

    /// <summary>
    /// The decision was whether an explicit request to rehydrate a previously offloaded artifact
    /// reference could be resolved.
    /// </summary>
    Rehydration,
}
