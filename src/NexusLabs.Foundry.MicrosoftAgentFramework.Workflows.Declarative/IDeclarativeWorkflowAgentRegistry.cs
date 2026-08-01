using Microsoft.Agents.AI;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Workflows.Declarative;

/// <summary>
/// Resolves the agent names that appear in a declarative workflow document to concrete agents.
/// </summary>
/// <remarks>
/// A declarative document refers to an agent only by name (for example
/// <c>agent: { name: Writer }</c>). Upstream resolves those names against a remote Azure AI Foundry
/// project; this seam resolves them against agents the host already owns, so a declarative workflow
/// can drive in-process agents with no remote project involved.
/// </remarks>
public interface IDeclarativeWorkflowAgentRegistry
{
    /// <summary>
    /// Attempts to resolve <paramref name="agentName"/> to an agent.
    /// </summary>
    /// <param name="agentName">The name exactly as written in the workflow document.</param>
    /// <param name="agent">The resolved agent when this returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> when a matching agent is registered.</returns>
    bool TryGetAgent(string agentName, out AIAgent? agent);
}
