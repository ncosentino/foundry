using NexusLabs.Foundry.MicrosoftAgentFramework.Progress;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Workflows.Declarative;

/// <summary>
/// A declarative workflow action finished executing.
/// </summary>
/// <remarks>
/// The <c>ProgressEvent</c> suffix distinguishes this from upstream's identically-purposed
/// <c>Microsoft.Agents.AI.Workflows.Declarative.DeclarativeActionCompletedEvent</c>.
/// </remarks>
public sealed record DeclarativeActionCompletedProgressEvent(
    DateTimeOffset Timestamp,
    string WorkflowId,
    string? AgentId,
    string? ParentAgentId,
    int Depth,
    long SequenceNumber,
    string ActionId,
    string? ActionType) : IProgressEvent;
