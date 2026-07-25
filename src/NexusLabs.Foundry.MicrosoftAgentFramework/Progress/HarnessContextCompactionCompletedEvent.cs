using NexusLabs.Foundry.MicrosoftAgentFramework.Diagnostics;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Progress;

/// <summary>
/// One hybrid context compaction/assembly attempt completed successfully — <see cref="Diagnostics"/>'s
/// outcome is <c>WithinLimit</c>, <c>Reduced</c>, or <c>PreservationFallback</c>. Emitted at most once
/// per assembly attempt that reached the started state, as one of exactly two mutually exclusive
/// terminal events alongside <see cref="HarnessContextCompactionTerminatedEvent"/> — never both for
/// the same attempt. Never emitted for an attempt whose classifier or snapshot-construction phase
/// threw before the <see cref="HarnessContextCompactionStartedEvent"/> was emitted. Emitted
/// immediately once the decision is known, before the outer execution-binding revalidation that
/// precedes dispatch, so an already-successful compaction decision remains observable even if that
/// later revalidation itself fails.
/// </summary>
/// <param name="Timestamp">When the decision was reached.</param>
/// <param name="WorkflowId">Top-level workflow correlation ID.</param>
/// <param name="AgentId">Which agent emitted this event, or <see langword="null"/> for workflow-level events.</param>
/// <param name="ParentAgentId">Parent agent ID for sub-agent runs, enabling tree reconstruction.</param>
/// <param name="Depth">Nesting depth: 0 = workflow, 1 = agent, 2 = sub-agent, etc.</param>
/// <param name="SequenceNumber">Globally ordered sequence number for event ordering.</param>
/// <param name="Diagnostics">
/// The privacy-safe, structured evidence for this decision. The identical instance is also carried by
/// <see cref="HarnessContextComposedEvent"/> when this same attempt subsequently reaches dispatch.
/// </param>
public sealed record HarnessContextCompactionCompletedEvent(
    DateTimeOffset Timestamp,
    string WorkflowId,
    string? AgentId,
    string? ParentAgentId,
    int Depth,
    long SequenceNumber,
    HarnessContextDiagnostics Diagnostics) : IProgressEvent;
