using NexusLabs.Foundry.MicrosoftAgentFramework.Diagnostics;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Progress;

/// <summary>
/// One hybrid context compaction/assembly attempt has started. Emitted exactly once per assembly
/// attempt that reaches the assembly phase — after message adaptation, snapshot integration, and
/// assembler construction have all succeeded, immediately before <c>AssembleAsync</c> is called —
/// while an experimental <c>HarnessHybridProfile</c> is configured. Never emitted for a call made
/// while compaction is absent, and never emitted when a classifier or snapshot-construction exception
/// aborts before assembly begins. Carries no size observation yet, only the configured measurement
/// unit and thresholds, because the original size is only known once the assembler captures its own
/// snapshot.
/// </summary>
/// <param name="Timestamp">When the assembly attempt started.</param>
/// <param name="WorkflowId">Top-level workflow correlation ID.</param>
/// <param name="AgentId">Which agent emitted this event, or <see langword="null"/> for workflow-level events.</param>
/// <param name="ParentAgentId">Parent agent ID for sub-agent runs, enabling tree reconstruction.</param>
/// <param name="Depth">Nesting depth: 0 = workflow, 1 = agent, 2 = sub-agent, etc.</param>
/// <param name="SequenceNumber">Globally ordered sequence number for event ordering.</param>
/// <param name="MeasurementUnit">The explicit unit every size on the eventual terminal event is expressed in.</param>
/// <param name="HardLimit">The hard limit in force for this assembly, in <paramref name="MeasurementUnit"/>.</param>
/// <param name="TriggerThreshold">
/// The trigger threshold (hard limit minus trigger margin) in force for this assembly, in
/// <paramref name="MeasurementUnit"/>.
/// </param>
public sealed record HarnessContextCompactionStartedEvent(
    DateTimeOffset Timestamp,
    string WorkflowId,
    string? AgentId,
    string? ParentAgentId,
    int Depth,
    long SequenceNumber,
    HarnessContextMeasurementUnit MeasurementUnit,
    int HardLimit,
    int TriggerThreshold) : IProgressEvent;
