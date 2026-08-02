using NexusLabs.Foundry.MicrosoftAgentFramework.Progress;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Workflows.Declarative;

/// <summary>
/// A declarative workflow raised an error.
/// </summary>
/// <remarks>
/// This is reported instead of a workflow-completed event carrying a failure flag, because the
/// upstream error event does not terminate the run: a workflow can raise an error and continue, so
/// treating it as completion would misreport the run''s shape.
/// </remarks>
public sealed record DeclarativeWorkflowErrorProgressEvent(
    DateTimeOffset Timestamp,
    string WorkflowId,
    string? AgentId,
    string? ParentAgentId,
    int Depth,
    long SequenceNumber,
    string ErrorMessage) : IProgressEvent;
