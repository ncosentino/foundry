using System.Reflection;

using Microsoft.Agents.AI;

namespace NexusLabs.Foundry.MicrosoftAgentFramework;

/// <summary>
/// Resolves the name a declared agent is published under.
/// </summary>
/// <remarks>
/// <para>
/// An agent is identified by a name in several places that are reached independently: the factory's
/// lookup map, <see cref="AIAgent.Name"/> and the message author that follows from it, the
/// <c>gen_ai.agent.name</c> telemetry dimension, and hosted agent registration. Each of those once
/// derived the name from <see cref="Type"/> on its own, which meant a change of policy had to be
/// applied in every one of them to take effect. They all call this instead, so the policy exists
/// once.
/// </para>
/// <para>
/// The policy is <see cref="FoundryAgentAttribute.Name"/> when declared, and the simple class name
/// otherwise.
/// </para>
/// </remarks>
public static class FoundryAgentName
{
    /// <summary>
    /// Resolves the published name of <paramref name="agentType"/>.
    /// </summary>
    /// <param name="agentType">The agent type, decorated with <see cref="FoundryAgentAttribute"/>.</param>
    /// <returns>
    /// <see cref="FoundryAgentAttribute.Name"/> when it is declared and not blank; otherwise the
    /// simple class name.
    /// </returns>
    /// <remarks>
    /// A type without <see cref="FoundryAgentAttribute"/> resolves to its class name rather than
    /// throwing, because callers that merely need a name — registration and diagnostics among them —
    /// must not fail ahead of the code whose job it is to report the missing declaration.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="agentType"/> is <see langword="null"/>.</exception>
    public static string Resolve(Type agentType)
    {
        ArgumentNullException.ThrowIfNull(agentType);

        return Resolve(agentType.GetCustomAttribute<FoundryAgentAttribute>(), agentType);
    }

    /// <summary>
    /// Resolves the published name of <paramref name="agentType"/> from an attribute the caller has
    /// already read.
    /// </summary>
    /// <param name="attribute">
    /// The agent's <see cref="FoundryAgentAttribute"/>, or <see langword="null"/> when the type does
    /// not declare one.
    /// </param>
    /// <param name="agentType">The agent type the attribute was read from.</param>
    /// <returns>
    /// <see cref="FoundryAgentAttribute.Name"/> when it is declared and not blank; otherwise the
    /// simple class name.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="agentType"/> is <see langword="null"/>.</exception>
    public static string Resolve(FoundryAgentAttribute? attribute, Type agentType)
    {
        ArgumentNullException.ThrowIfNull(agentType);

        return string.IsNullOrWhiteSpace(attribute?.Name)
            ? agentType.Name
            : attribute!.Name!;
    }
}
