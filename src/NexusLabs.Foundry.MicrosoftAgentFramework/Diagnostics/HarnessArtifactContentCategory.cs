namespace NexusLabs.Foundry.MicrosoftAgentFramework.Diagnostics;

/// <summary>
/// The kind of content an artifact offload or rehydration decision was made about. Distinguishes
/// an ordinary tool invocation result from a previously-rehydrated recoverable context segment
/// being re-inlined by the offload transform's threshold bypass, so that bypass path remains
/// separately inspectable from an ordinary below-threshold decision.
/// </summary>
public enum HarnessArtifactContentCategory
{
    /// <summary>
    /// The content is an ordinary tool invocation result being evaluated for offload.
    /// </summary>
    ToolResult,

    /// <summary>
    /// The content is a recoverable context segment: either a prior rehydration's resolved content
    /// (the category every rehydration decision uses), or a recoverable segment being re-inlined by
    /// the offload transform's threshold bypass, which always inlines unconditionally rather than
    /// re-measuring against the configured threshold.
    /// </summary>
    RecoverableContextSegment,
}
