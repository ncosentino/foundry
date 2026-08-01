using Microsoft.Agents.AI;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Workflows.Declarative;

/// <summary>
/// An immutable, case-insensitive registry of agents addressable by the names used in a declarative
/// workflow document.
/// </summary>
/// <remarks>
/// Name matching is case-insensitive because workflow documents are hand-authored and frequently
/// edited by people who are not compiling anything; a casing mismatch there would surface as an
/// agent-not-found failure at run time rather than as an obvious typo.
/// </remarks>
public sealed class DeclarativeWorkflowAgentRegistry : IDeclarativeWorkflowAgentRegistry
{
    private readonly Dictionary<string, AIAgent> _agents;

    /// <exception cref="ArgumentNullException"><paramref name="agents"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// A name is empty or whitespace-only, an agent is <see langword="null"/>, or two names differ
    /// only by case.
    /// </exception>
    public DeclarativeWorkflowAgentRegistry(IReadOnlyDictionary<string, AIAgent> agents)
    {
        ArgumentNullException.ThrowIfNull(agents);

        _agents = new Dictionary<string, AIAgent>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, agent) in agents)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException(
                    "A declarative workflow agent name must be non-empty.",
                    nameof(agents));
            }

            if (agent is null)
            {
                throw new ArgumentException(
                    $"The agent registered as '{name}' is null.",
                    nameof(agents));
            }

            if (!_agents.TryAdd(name, agent))
            {
                throw new ArgumentException(
                    $"Two agents are registered under the name '{name}'. Names are matched " +
                    "case-insensitively, so they must be distinct ignoring case.",
                    nameof(agents));
            }
        }
    }

    /// <summary>Gets the registered agent names, in no particular order.</summary>
    public IReadOnlyCollection<string> AgentNames => _agents.Keys;

    /// <inheritdoc />
    public bool TryGetAgent(string agentName, out AIAgent? agent)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentName);

        return _agents.TryGetValue(agentName, out agent);
    }
}
