using Microsoft.Agents.AI.Workflows;
using Microsoft.Agents.AI.Workflows.Declarative;
using Microsoft.Extensions.AI;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Workflows.Declarative;

/// <summary>
/// Builds executable workflows from declarative YAML documents, resolving the agents they name
/// against Foundry-registered agents.
/// </summary>
/// <remarks>
/// <para>
/// The result is an ordinary <see cref="Workflow"/>, identical in kind to one built in code, so it
/// runs through the same execution, checkpointing, and event surfaces. This factory adds agent
/// resolution and deliberate validation; it does not introduce a second execution model.
/// </para>
/// <para>
/// Workflow input is converted to a user message before the document sees it, matching how a
/// declarative document reads its input through <c>System.LastMessage</c> rather than through a
/// typed input namespace.
/// </para>
/// </remarks>
public sealed class FoundryDeclarativeWorkflowFactory
{
    private readonly FoundryAgentProvider _agentProvider;

    /// <exception cref="ArgumentNullException"><paramref name="agentProvider"/> is <see langword="null"/>.</exception>
    public FoundryDeclarativeWorkflowFactory(FoundryAgentProvider agentProvider)
    {
        ArgumentNullException.ThrowIfNull(agentProvider);

        _agentProvider = agentProvider;
    }

    /// <summary>
    /// Builds a workflow from a declarative YAML document.
    /// </summary>
    /// <param name="workflowYaml">The complete document text.</param>
    /// <returns>An executable workflow accepting a string input.</returns>
    /// <exception cref="ArgumentException"><paramref name="workflowYaml"/> is empty or whitespace-only.</exception>
    /// <exception cref="DeclarativeWorkflowParseException">The document could not be parsed.</exception>
    public Workflow Create(string workflowYaml)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workflowYaml);

        using var reader = new StringReader(workflowYaml);
        return Create(reader);
    }

    /// <summary>
    /// Builds a workflow from a declarative YAML document.
    /// </summary>
    /// <param name="workflowYaml">A reader positioned at the start of the document.</param>
    /// <returns>An executable workflow accepting a string input.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="workflowYaml"/> is <see langword="null"/>.</exception>
    /// <exception cref="DeclarativeWorkflowParseException">The document could not be parsed.</exception>
    public Workflow Create(TextReader workflowYaml)
    {
        ArgumentNullException.ThrowIfNull(workflowYaml);

        var options = new DeclarativeWorkflowOptions(_agentProvider);
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
    /// is not registered, because neither is knowable until the action runs.
    /// </para>
    /// </remarks>
    /// <param name="workflowYaml">The complete document text.</param>
    /// <returns>The validation outcome, including parse failure detail when invalid.</returns>
    /// <exception cref="ArgumentException"><paramref name="workflowYaml"/> is empty or whitespace-only.</exception>
    public DeclarativeWorkflowValidationResult Validate(string workflowYaml)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workflowYaml);

        try
        {
            _ = Create(workflowYaml);
            return DeclarativeWorkflowValidationResult.Valid();
        }
        catch (DeclarativeWorkflowParseException ex)
        {
            return DeclarativeWorkflowValidationResult.Invalid(ex.Message);
        }
    }
}
