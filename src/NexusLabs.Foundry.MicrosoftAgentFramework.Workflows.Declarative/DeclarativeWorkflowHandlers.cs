using Microsoft.Agents.AI.Workflows.Declarative;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Workflows.Declarative;

/// <summary>
/// The handlers a declarative workflow calls out through when a document invokes something that is
/// not a Foundry agent.
/// </summary>
/// <remarks>
/// <para>
/// A declarative document can reach outside the workflow in three ways: it can invoke an agent, an
/// MCP server tool, or an HTTP endpoint. Agent invocation is always wired, because resolving agents
/// against Foundry's declarations is the reason this package exists. The other two are not, because
/// each reaches a network endpoint the host — not the document, and not Foundry — must decide to
/// trust.
/// </para>
/// <para>
/// Both members are <see langword="required"/> so that decision is stated rather than defaulted.
/// Supplying <see langword="null"/> is a valid answer meaning "documents may not invoke this"; what
/// is not available is leaving the question unanswered and discovering the omission when an action
/// fails mid-run. Callers whose documents invoke neither should use the
/// <c>CreateDeclarativeWorkflow</c> overloads that take no handlers at all.
/// </para>
/// <para>
/// Foundry supplies no implementation of either interface. An implementation decides which servers
/// and endpoints may be reached, what credentials are attached, and what timeout and retry policy
/// applies; none of that is knowable here. <c>Microsoft.Agents.AI.Workflows.Declarative.Mcp</c>
/// provides <c>DefaultMcpToolHandler</c>, and <see cref="DefaultHttpRequestHandler"/> ships in the
/// package this one already depends on.
/// </para>
/// </remarks>
public sealed record DeclarativeWorkflowHandlers
{
    /// <summary>
    /// Gets the handler invoked for an <c>InvokeMcpTool</c> action, or <see langword="null"/> to
    /// leave MCP tool invocation unavailable.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The action supplies the handler with the document's <c>serverUrl</c> and <c>toolName</c>,
    /// which are required, and its optional <c>serverLabel</c>, <c>arguments</c>, <c>headers</c>,
    /// and <c>connectionName</c>. Argument values are Power Fx expressions evaluated against
    /// workflow state before the handler sees them, so a handler receives resolved values rather
    /// than expression text.
    /// </para>
    /// <para>
    /// When this is <see langword="null"/> and a document contains an <c>InvokeMcpTool</c> action,
    /// the document is rejected while the workflow is being built, not when the action runs. That
    /// makes an unwired handler one of the few authoring faults
    /// <c>ValidateDeclarativeWorkflow</c> does catch.
    /// </para>
    /// </remarks>
    public required IMcpToolHandler? McpToolHandler { get; init; }

    /// <summary>
    /// Gets the handler invoked for an HTTP action, or <see langword="null"/> to leave HTTP
    /// invocation unavailable.
    /// </summary>
    /// <remarks>
    /// <see cref="DefaultHttpRequestHandler"/> is an unrestricted implementation: it sends whatever
    /// the document asks for, to wherever the document names. That is appropriate for a trusted
    /// document and a poor default for one that is not, which is why it is not wired automatically.
    /// </remarks>
    public required IHttpRequestHandler? HttpRequestHandler { get; init; }
}
