using NexusLabs.Foundry.MicrosoftAgentFramework.Progress;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Workflows.Declarative;

/// <summary>
/// A declarative workflow action began executing.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="ActionId"/> is the <c>id</c> written in the workflow document, which is what an author
/// edits and what a failure report needs to name. It is deliberately not an agent identifier: most
/// declarative actions never invoke an agent at all.
/// </para>
/// <para>
/// The <c>ProgressEvent</c> suffix distinguishes this from upstream's identically-purposed
/// <c>Microsoft.Agents.AI.Workflows.Declarative.DeclarativeActionInvokedEvent</c>, which travels on
/// the workflow event stream rather than through Foundry progress reporting.
/// </para>
/// </remarks>
public sealed record DeclarativeActionStartedProgressEvent(
    DateTimeOffset Timestamp,
    string WorkflowId,
    string? AgentId,
    string? ParentAgentId,
    int Depth,
    long SequenceNumber,
    string ActionId,
    string? ActionType) : IProgressEvent;
