namespace NexusLabs.Foundry.Evaluation.Harness;

/// <summary>
/// The lifecycle kind of a normalized progress record used for event-ordering and lifecycle-pairing
/// evaluation. Each kind corresponds to a Harness progress event family; paired kinds
/// (<c>*Started</c>/<c>*Completed</c>/<c>*Terminated</c>) are matched by correlation identity.
/// </summary>
public enum HarnessLifecycleEventKind
{
    /// <summary>Workflow-level lifecycle boundary.</summary>
    Workflow,

    /// <summary>Agent-turn lifecycle boundary.</summary>
    Agent,

    /// <summary>An LLM provider call boundary.</summary>
    LlmCall,

    /// <summary>A tool invocation boundary.</summary>
    ToolCall,

    /// <summary>A hybrid context compaction/assembly attempt boundary.</summary>
    ContextCompaction,

    /// <summary>The final bounded context composed and ready for dispatch.</summary>
    ContextComposed,

    /// <summary>An artifact offload or rehydration decision (an instantaneous record).</summary>
    ArtifactDecision,
}
