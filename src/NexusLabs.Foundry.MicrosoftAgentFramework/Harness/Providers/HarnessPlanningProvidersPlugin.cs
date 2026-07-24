using Microsoft.Agents.AI;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Providers;

/// <summary>
/// Narrow, immutable selected-provider slice composing the upstream MAF 1.15
/// <see cref="TodoProvider"/> and <see cref="AgentModeProvider"/> <see cref="AIContextProvider"/>
/// implementations. Todo and AgentMode are independently selectable: supplying only one
/// provider never instantiates or exposes the other, and
/// <see cref="HarnessProviderComposition"/> is solely responsible for checking the supplied
/// providers against the capability profile before either one is composed into an agent.
/// </summary>
internal sealed class HarnessPlanningProvidersPlugin
{
    private HarnessPlanningProvidersPlugin(
        TodoProvider? todoProvider,
        AgentModeProvider? agentModeProvider)
    {
        TodoProvider = todoProvider;
        AgentModeProvider = agentModeProvider;

        var contextProviders = new List<AIContextProvider>();
        var stateKeys = new List<string>();
        if (todoProvider is not null)
        {
            contextProviders.Add(todoProvider);
            stateKeys.AddRange(todoProvider.StateKeys);
        }
        if (agentModeProvider is not null)
        {
            contextProviders.Add(agentModeProvider);
            stateKeys.AddRange(agentModeProvider.StateKeys);
        }

        if (stateKeys.Count == 0 ||
            stateKeys.Any(string.IsNullOrWhiteSpace) ||
            stateKeys.Distinct(StringComparer.Ordinal).Count() != stateKeys.Count)
        {
            throw new InvalidOperationException(
                "The selected Todo/AgentMode providers must expose unique, non-empty " +
                "state keys.");
        }

        AIContextProviders = contextProviders;
        ProviderStateKeys =
        [
            .. stateKeys.OrderBy(key => key, StringComparer.Ordinal),
        ];
    }

    /// <summary>
    /// The upstream Todo provider, or <see langword="null"/> when Todo was not selected.
    /// Never populated as a side effect of selecting AgentMode.
    /// </summary>
    internal TodoProvider? TodoProvider { get; }

    /// <summary>
    /// The upstream agent-mode provider, or <see langword="null"/> when AgentMode was not
    /// selected. Never populated as a side effect of selecting Todo.
    /// </summary>
    internal AgentModeProvider? AgentModeProvider { get; }

    /// <summary>
    /// Only the <see cref="AIContextProvider"/> instances backing the providers that were
    /// actually supplied, in <see cref="TodoProvider"/>-then-<see cref="AgentModeProvider"/>
    /// order, for composition into <c>ChatClientAgentOptions.AIContextProviders</c>.
    /// </summary>
    internal IReadOnlyList<AIContextProvider> AIContextProviders { get; }

    /// <summary>
    /// The canonical (ordinal, sorted, de-duplicated) set of state keys the supplied
    /// providers contribute to <see cref="AgentSession.StateBag"/>.
    /// </summary>
    internal IReadOnlyList<string> ProviderStateKeys { get; }

    /// <summary>
    /// Creates a planning-providers plugin from the providers the caller selected. At least
    /// one of <paramref name="todoProvider"/> or <paramref name="agentModeProvider"/> must be
    /// supplied; a plugin with neither would contribute nothing and must not be constructed.
    /// </summary>
    internal static HarnessPlanningProvidersPlugin Create(
        TodoProvider? todoProvider,
        AgentModeProvider? agentModeProvider)
    {
        if (todoProvider is null && agentModeProvider is null)
        {
            throw new InvalidOperationException(
                "At least one of the Todo or AgentMode providers must be supplied.");
        }

        return new HarnessPlanningProvidersPlugin(todoProvider, agentModeProvider);
    }
}
