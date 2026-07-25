using NexusLabs.Foundry.MicrosoftAgentFramework.Diagnostics;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Progress;

/// <summary>
/// One explicit tool-result offload decision made by the shared, caller-agnostic offload
/// transform. Emitted exactly once per <c>Transform</c> call that produces a decision — including
/// an <see cref="HarnessArtifactOutcomeCategory.Inline"/> decision, so threshold behavior remains
/// inspectable even when nothing is written to the workspace. Never emitted when the transform
/// throws before reaching a decision (for example a pre-canceled token or a stale execution
/// binding), because no decision was made in that case.
/// </summary>
/// <param name="Timestamp">When the decision was made.</param>
/// <param name="WorkflowId">Top-level workflow correlation ID.</param>
/// <param name="AgentId">Which agent emitted this event, or <see langword="null"/> for workflow-level events.</param>
/// <param name="ParentAgentId">Parent agent ID for sub-agent runs, enabling tree reconstruction.</param>
/// <param name="Depth">Nesting depth: 0 = workflow, 1 = agent, 2 = sub-agent, etc.</param>
/// <param name="SequenceNumber">Globally ordered sequence number for event ordering.</param>
/// <param name="Diagnostics">
/// The privacy-safe, structured evidence for this decision. The identical instance is also
/// attached to the internal offload outcome this decision produced.
/// </param>
public sealed record HarnessArtifactOffloadDecisionEvent(
    DateTimeOffset Timestamp,
    string WorkflowId,
    string? AgentId,
    string? ParentAgentId,
    int Depth,
    long SequenceNumber,
    HarnessArtifactDiagnostics Diagnostics) : IProgressEvent;
