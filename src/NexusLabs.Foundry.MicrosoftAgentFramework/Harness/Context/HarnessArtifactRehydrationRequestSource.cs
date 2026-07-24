namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Context;

/// <summary>
/// The origin of an explicit <see cref="HarnessArtifactRehydrationRequest"/>, matching
/// <c>data-model.md</c>'s "Rehydration Decision" field "Request source: tool request or
/// deterministic policy". G4 only ever constructs a request from one of these two
/// deterministic-caller-driven sources — never from an automatic, relevance-based, or
/// compaction-triggered decision (that remains G5's "hybrid context" responsibility per
/// <c>harness-lifecycle-feasibility.md</c>'s "G4 rehydration mechanism boundary").
/// </summary>
internal enum HarnessArtifactRehydrationRequestSource
{
    /// <summary>An explicit tool call requested rehydration of a specific reference.</summary>
    ToolRequest,

    /// <summary>A deterministic, non-model-driven policy requested rehydration of a specific reference.</summary>
    DeterministicPolicy,
}
