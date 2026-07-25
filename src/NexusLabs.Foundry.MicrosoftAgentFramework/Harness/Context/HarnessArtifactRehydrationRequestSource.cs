namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Context;

/// <summary>
/// The origin of an explicit <see cref="HarnessArtifactRehydrationRequest"/>, matching
/// <c>data-model.md</c>'s "Rehydration Decision" field "Request source: tool request or
/// deterministic policy". A request is only ever constructed from one of these two
/// deterministic-caller-driven sources — never from an automatic, relevance-based, or
/// compaction-triggered decision (that remains a separate hybrid-context responsibility per
/// <c>harness-lifecycle-feasibility.md</c>'s rehydration mechanism boundary).
/// </summary>
internal enum HarnessArtifactRehydrationRequestSource
{
    /// <summary>An explicit tool call requested rehydration of a specific reference.</summary>
    ToolRequest,

    /// <summary>A deterministic, non-model-driven policy requested rehydration of a specific reference.</summary>
    DeterministicPolicy,
}
