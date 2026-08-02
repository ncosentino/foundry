using Microsoft.Agents.AI.Workflows;
using Microsoft.Agents.AI.Workflows.Declarative;
using Microsoft.Extensions.AI;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Workflows.Declarative;

/// <summary>
/// Builds executable workflows from declarative YAML documents, resolving the agents they name
/// against Foundry's declared agents.
/// </summary>
/// <remarks>
/// <para>
/// The result is an ordinary <see cref="Workflow"/>, identical in kind to one produced by
/// <c>IWorkflowFactory</c> from attribute-declared topology, and it runs through the same execution,
/// checkpointing, and event surfaces. Only the declaration source differs: a YAML document instead
/// of attributes.
/// </para>
/// <para>
/// These are extension methods on <see cref="IAgentFactory"/> rather than members of
/// <c>IWorkflowFactory</c> because agent resolution is the only thing declarative composition needs
/// from Foundry, and because <c>IWorkflowFactory</c> lives in the core package, which cannot take a
/// dependency on this one without pulling an interpreted expression engine into every consumer and
/// breaking the NativeAOT profile.
/// </para>
/// <para>
/// Workflow input is converted to a user message before the document sees it, matching how a
/// declarative document reads input through <c>System.LastMessage</c> rather than a typed input
/// namespace.
/// </para>
/// </remarks>
public static class DeclarativeWorkflowAgentFactoryExtensions
{
    /// <summary>
    /// Builds a workflow from a declarative YAML document.
    /// </summary>
    /// <param name="agentFactory">Resolves the agents the document names by class name.</param>
    /// <param name="workflowYaml">The complete document text.</param>
    /// <returns>An executable workflow accepting a string input.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="agentFactory"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="workflowYaml"/> is empty or whitespace-only.</exception>
    /// <exception cref="DeclarativeWorkflowParseException">The document could not be parsed.</exception>
    public static Workflow CreateDeclarativeWorkflow(
        this IAgentFactory agentFactory,
        string workflowYaml)
    {
        ArgumentNullException.ThrowIfNull(agentFactory);
        ArgumentException.ThrowIfNullOrWhiteSpace(workflowYaml);

        using var reader = new StringReader(workflowYaml);
        return agentFactory.CreateDeclarativeWorkflow(reader);
    }

    /// <summary>
    /// Builds a workflow from a declarative YAML document.
    /// </summary>
    /// <param name="agentFactory">Resolves the agents the document names by class name.</param>
    /// <param name="workflowYaml">A reader positioned at the start of the document.</param>
    /// <returns>An executable workflow accepting a string input.</returns>
    /// <exception cref="ArgumentNullException">Either argument is <see langword="null"/>.</exception>
    /// <exception cref="DeclarativeWorkflowParseException">The document could not be parsed.</exception>
    public static Workflow CreateDeclarativeWorkflow(
        this IAgentFactory agentFactory,
        TextReader workflowYaml)
    {
        ArgumentNullException.ThrowIfNull(agentFactory);
        ArgumentNullException.ThrowIfNull(workflowYaml);

        var options = new DeclarativeWorkflowOptions(new FoundryAgentProvider(agentFactory));
        try
        {
            return DeclarativeWorkflowBuilder.Build<string>(
                workflowYaml,
                options,
                static input => new ChatMessage(ChatRole.User, input));
        }
        catch (Exception ex) when (ex is not ArgumentException and not OperationCanceledException)
        {
            // Upstream surfaces authoring errors through several exception types originating in the
            // Power Platform object model, so they are normalized to one Foundry type rather than
            // leaking a parser detail a workflow author cannot act on.
            throw new DeclarativeWorkflowParseException(ex);
        }
    }

    /// <summary>
    /// Parses a declarative YAML document and reports whether it is well-formed, without executing
    /// it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Declarative workflows have no published JSON Schema, so parsing is the only validation
    /// available. This method exists so a host can perform it deliberately — at startup, in a test,
    /// or in a lint step — rather than discovering an authoring error partway through a run.
    /// </para>
    /// <para>
    /// It reports what upstream's builder rejects, which is structural malformation. It does
    /// <em>not</em> catch every authoring mistake: upstream accepts an action <c>kind</c> it does not
    /// recognize, and it cannot detect an expression that will fail to evaluate or an agent name that
    /// is not declared, because neither is knowable until the action runs.
    /// </para>
    /// </remarks>
    /// <param name="agentFactory">Resolves the agents the document names by class name.</param>
    /// <param name="workflowYaml">The complete document text.</param>
    /// <returns>The validation outcome, including parse failure detail when invalid.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="agentFactory"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="workflowYaml"/> is empty or whitespace-only.</exception>
    public static DeclarativeWorkflowValidationResult ValidateDeclarativeWorkflow(
        this IAgentFactory agentFactory,
        string workflowYaml)
    {
        ArgumentNullException.ThrowIfNull(agentFactory);
        ArgumentException.ThrowIfNullOrWhiteSpace(workflowYaml);

        try
        {
            _ = agentFactory.CreateDeclarativeWorkflow(workflowYaml);
            return DeclarativeWorkflowValidationResult.Valid();
        }
        catch (DeclarativeWorkflowParseException ex)
        {
            return DeclarativeWorkflowValidationResult.Invalid(ex.Message);
        }
    }
}
