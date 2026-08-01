namespace NexusLabs.Foundry.MicrosoftAgentFramework.Workflows.Declarative;

/// <summary>
/// Thrown when a declarative workflow names an agent that is not registered.
/// </summary>
/// <remarks>
/// Agent names live in a YAML document rather than in code, so this failure is a content error a
/// workflow author can fix. It carries the requested name so the message identifies the document
/// text to correct rather than only reporting that resolution failed.
/// </remarks>
public sealed class DeclarativeWorkflowAgentNotFoundException : InvalidOperationException
{
    /// <param name="agentName">The unresolved name exactly as written in the workflow document.</param>
    public DeclarativeWorkflowAgentNotFoundException(string agentName)
        : base($"The declarative workflow referenced agent '{agentName}', which is not registered.")
    {
        AgentName = agentName;
    }

    /// <summary>Gets the unresolved agent name.</summary>
    public string AgentName { get; }
}
