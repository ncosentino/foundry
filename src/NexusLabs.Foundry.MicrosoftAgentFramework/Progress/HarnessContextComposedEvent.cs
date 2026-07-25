using NexusLabs.Foundry.MicrosoftAgentFramework.Diagnostics;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Progress;

/// <summary>
/// The final bounded context for one provider call is composed and ready for dispatch. Emitted only
/// on success — immediately after <see cref="HarnessContextCompactionCompletedEvent"/> for the same
/// attempt, once execution-binding revalidation has also passed and the bounded messages are about to
/// be handed to the real provider client — never emitted for a terminated attempt. Carries the exact
/// same <see cref="Diagnostics"/> instance as the preceding
/// <see cref="HarnessContextCompactionCompletedEvent"/>, so its final category attribution and
/// <see cref="HarnessContextDiagnostics.FinalSequenceValid"/> flag are inspectable at the
/// ready-for-dispatch point without a second, independently-built snapshot.
/// </summary>
/// <param name="Timestamp">When the context was composed and ready for dispatch.</param>
/// <param name="WorkflowId">Top-level workflow correlation ID.</param>
/// <param name="AgentId">Which agent emitted this event, or <see langword="null"/> for workflow-level events.</param>
/// <param name="ParentAgentId">Parent agent ID for sub-agent runs, enabling tree reconstruction.</param>
/// <param name="Depth">Nesting depth: 0 = workflow, 1 = agent, 2 = sub-agent, etc.</param>
/// <param name="SequenceNumber">Globally ordered sequence number for event ordering.</param>
/// <param name="AssemblyId">
/// The identical opaque per-assembly correlation ID carried by both the
/// <see cref="HarnessContextCompactionStartedEvent"/> and the preceding
/// <see cref="HarnessContextCompactionCompletedEvent"/> for this same attempt, so this Composed
/// event remains pairable with the rest of its lifecycle even when other concurrently-running
/// assemblies on the same agent interleave their own events' <see cref="SequenceNumber"/>s in
/// between.
/// </param>
/// <param name="Diagnostics">
/// The privacy-safe, structured evidence for this decision, identical to the instance carried by the
/// preceding <see cref="HarnessContextCompactionCompletedEvent"/> for the same attempt.
/// </param>
public sealed record HarnessContextComposedEvent(
    DateTimeOffset Timestamp,
    string WorkflowId,
    string? AgentId,
    string? ParentAgentId,
    int Depth,
    long SequenceNumber,
    Guid AssemblyId,
    HarnessContextDiagnostics Diagnostics) : IProgressEvent;
