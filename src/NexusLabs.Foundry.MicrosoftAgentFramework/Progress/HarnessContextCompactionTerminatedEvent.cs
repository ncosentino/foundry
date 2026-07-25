using NexusLabs.Foundry.MicrosoftAgentFramework.Diagnostics;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Progress;

/// <summary>
/// One hybrid context compaction/assembly attempt terminated without reaching a dispatchable
/// context — <see cref="Diagnostics"/>'s outcome is <c>Irreducible</c> or
/// <c>ConcurrentMutationLimit</c>. Emitted at most once per assembly attempt that reached the started
/// state, as one of exactly two mutually exclusive terminal events alongside
/// <see cref="HarnessContextCompactionCompletedEvent"/> — never both for the same attempt. Never
/// emitted for an attempt whose classifier or snapshot-construction phase threw before the
/// <see cref="HarnessContextCompactionStartedEvent"/> was emitted, and never emitted for exceptional
/// failures (cancellation, binding invalidation, or reducer exception) that propagate directly without
/// producing a structured <c>Irreducible</c> or <c>ConcurrentMutationLimit</c> result. Never carries
/// raw message text, exception text, or classifier output — only the categorical termination reached.
/// </summary>
/// <param name="Timestamp">When the termination was reached.</param>
/// <param name="WorkflowId">Top-level workflow correlation ID.</param>
/// <param name="AgentId">Which agent emitted this event, or <see langword="null"/> for workflow-level events.</param>
/// <param name="ParentAgentId">Parent agent ID for sub-agent runs, enabling tree reconstruction.</param>
/// <param name="Depth">Nesting depth: 0 = workflow, 1 = agent, 2 = sub-agent, etc.</param>
/// <param name="SequenceNumber">Globally ordered sequence number for event ordering.</param>
/// <param name="AssemblyId">
/// The identical opaque per-assembly correlation ID carried by the preceding
/// <see cref="HarnessContextCompactionStartedEvent"/> for this same attempt, so this Terminated
/// event is pairable with its Started event even when other concurrently-running assemblies on the
/// same agent interleave their own events' <see cref="SequenceNumber"/>s in between.
/// </param>
/// <param name="Diagnostics">The privacy-safe, structured evidence for this termination.</param>
public sealed record HarnessContextCompactionTerminatedEvent(
    DateTimeOffset Timestamp,
    string WorkflowId,
    string? AgentId,
    string? ParentAgentId,
    int Depth,
    long SequenceNumber,
    Guid AssemblyId,
    HarnessContextDiagnostics Diagnostics) : IProgressEvent;
