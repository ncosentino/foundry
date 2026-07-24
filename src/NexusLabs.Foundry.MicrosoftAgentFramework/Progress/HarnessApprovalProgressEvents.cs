namespace NexusLabs.Foundry.MicrosoftAgentFramework.Progress;

/// <summary>
/// A tool-approval request surfaced to the caller. Emitted once per distinct
/// request identifier when the composed agent's response contains a
/// <c>ToolApprovalRequestContent</c> item (MAF's approval-required tool-call surface).
/// The MAF content type itself is never exposed; only stable identifiers are carried.
/// </summary>
/// <param name="Timestamp">When the event occurred.</param>
/// <param name="WorkflowId">Top-level workflow correlation ID.</param>
/// <param name="AgentId">Which agent emitted this event, or <see langword="null"/> for workflow-level events.</param>
/// <param name="ParentAgentId">Parent agent ID for sub-agent runs, enabling tree reconstruction.</param>
/// <param name="Depth">Nesting depth: 0 = workflow, 1 = agent, 2 = sub-agent, etc.</param>
/// <param name="SequenceNumber">Globally ordered sequence number for event ordering.</param>
/// <param name="RequestId">
/// The stable, caller-visible request identifier the approval response must reference to
/// be bound back to this request (MAF's <c>InputRequestContent.RequestId</c>).
/// </param>
/// <param name="ToolName">The name of the tool the approval request is for.</param>
public sealed record HarnessApprovalRequestedEvent(
    DateTimeOffset Timestamp,
    string WorkflowId,
    string? AgentId,
    string? ParentAgentId,
    int Depth,
    long SequenceNumber,
    string RequestId,
    string ToolName) : IProgressEvent;

/// <summary>
/// An ordinary (non-standing) tool-approval response approved the pending request.
/// Emitted once per approved request; a standing ("always approve") response is reported
/// via <see cref="HarnessApprovalStandingReauthorizedEvent"/> instead, never both.
/// </summary>
/// <param name="Timestamp">When the event occurred.</param>
/// <param name="WorkflowId">Top-level workflow correlation ID.</param>
/// <param name="AgentId">Which agent emitted this event, or <see langword="null"/> for workflow-level events.</param>
/// <param name="ParentAgentId">Parent agent ID for sub-agent runs, enabling tree reconstruction.</param>
/// <param name="Depth">Nesting depth: 0 = workflow, 1 = agent, 2 = sub-agent, etc.</param>
/// <param name="SequenceNumber">Globally ordered sequence number for event ordering.</param>
/// <param name="RequestId">The request identifier the approval response answered.</param>
/// <param name="ToolName">The name of the tool that was approved.</param>
public sealed record HarnessApprovalApprovedEvent(
    DateTimeOffset Timestamp,
    string WorkflowId,
    string? AgentId,
    string? ParentAgentId,
    int Depth,
    long SequenceNumber,
    string RequestId,
    string ToolName) : IProgressEvent;

/// <summary>
/// An ordinary (non-standing) tool-approval response rejected the pending request. Emitted
/// once per rejected request. A rejected request results in zero invocations of the
/// underlying tool.
/// </summary>
/// <param name="Timestamp">When the event occurred.</param>
/// <param name="WorkflowId">Top-level workflow correlation ID.</param>
/// <param name="AgentId">Which agent emitted this event, or <see langword="null"/> for workflow-level events.</param>
/// <param name="ParentAgentId">Parent agent ID for sub-agent runs, enabling tree reconstruction.</param>
/// <param name="Depth">Nesting depth: 0 = workflow, 1 = agent, 2 = sub-agent, etc.</param>
/// <param name="SequenceNumber">Globally ordered sequence number for event ordering.</param>
/// <param name="RequestId">The request identifier the approval response answered.</param>
/// <param name="ToolName">The name of the tool that was rejected.</param>
/// <param name="Reason">The caller-supplied rejection reason, when provided.</param>
public sealed record HarnessApprovalRejectedEvent(
    DateTimeOffset Timestamp,
    string WorkflowId,
    string? AgentId,
    string? ParentAgentId,
    int Depth,
    long SequenceNumber,
    string RequestId,
    string ToolName,
    string? Reason) : IProgressEvent;

/// <summary>
/// A standing ("always approve") tool approval -- newly supplied this run, or implied by
/// continuing a session that could carry one from a prior turn -- was submitted for the
/// required host reauthorization check. Emitted exactly once per reauthorization attempt,
/// whether the host granted or declined it; a declined reauthorization fails the run closed
/// with zero tool invocations.
/// </summary>
/// <param name="Timestamp">When the event occurred.</param>
/// <param name="WorkflowId">Top-level workflow correlation ID.</param>
/// <param name="AgentId">Which agent emitted this event, or <see langword="null"/> for workflow-level events.</param>
/// <param name="ParentAgentId">Parent agent ID for sub-agent runs, enabling tree reconstruction.</param>
/// <param name="Depth">Nesting depth: 0 = workflow, 1 = agent, 2 = sub-agent, etc.</param>
/// <param name="SequenceNumber">Globally ordered sequence number for event ordering.</param>
/// <param name="ToolName">
/// The tool name the standing approval would apply to, when it could be determined from
/// the inbound messages; <see langword="null"/> when reauthorizing a restored session for
/// which no specific tool name is known.
/// </param>
/// <param name="Granted">Whether the host validator authorized reliance on the standing approval.</param>
public sealed record HarnessApprovalStandingReauthorizedEvent(
    DateTimeOffset Timestamp,
    string WorkflowId,
    string? AgentId,
    string? ParentAgentId,
    int Depth,
    long SequenceNumber,
    string? ToolName,
    bool Granted) : IProgressEvent;
