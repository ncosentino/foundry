using Microsoft.Agents.AI;

namespace NexusLabs.Foundry.MicrosoftAgentFramework;

/// <summary>
/// Marks a class as a declared agent type for Foundry's Agent Framework integration.
/// Apply this attribute to a class to enable compile-time registration via the source generator
/// and <see cref="IAgentFactory.CreateAgent{TAgent}()"/> lookup.
/// </summary>
/// <remarks>
/// When the <c>NexusLabs.Foundry.MicrosoftAgentFramework.Generators</c> package is referenced,
/// a <c>[ModuleInitializer]</c> is emitted that automatically registers the agent type
/// with <see cref="AgentFrameworkGeneratedBootstrap"/>. <c>UsingAgentFramework()</c>
/// then discovers and registers these types without any explicit <c>Add*FromGenerated()</c> calls.
/// </remarks>
/// <example>
/// <code>
/// [FoundryAgent(
///     Instructions = "You are a helpful customer support agent. Answer questions about orders.",
///     Description = "Customer support agent for order inquiries")]
/// [AgentHandoffsTo(typeof(BillingAgent), "Escalate billing or payment questions to the billing agent")]
/// public class CustomerSupportAgent
/// {
/// }
///
/// // In your composition root:
/// var agentFactory = syringe.BuildAgentFactory();
/// var agent = agentFactory.CreateAgent&lt;CustomerSupportAgent&gt;();
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class FoundryAgentAttribute : Attribute
{
    /// <summary>
    /// Gets or sets the name this agent is published under, overriding the class name.
    /// When <see langword="null"/>, the simple class name is used.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the agent's identity everywhere a name rather than a type identifies it: the key for
    /// <see cref="IAgentFactory.CreateAgent(string)"/>, the value of <see cref="AIAgent.Name"/> and
    /// therefore the author of the messages it produces, the <c>gen_ai.agent.name</c> telemetry
    /// dimension, and the key a hosted agent is registered under. Setting it here sets all of them,
    /// because they are all resolved from this one value.
    /// </para>
    /// <para>
    /// Set it when the published identity should outlive the class name — a workflow document that
    /// names agents in text, or a metrics dashboard that groups by agent, both keep working across a
    /// class rename only if the name is declared rather than derived.
    /// </para>
    /// <para>
    /// Setting a name <em>replaces</em> the class name as the addressable alias: once
    /// <c>[FoundryAgent(Name = "Triage")]</c> is applied to <c>TriageAgent</c>,
    /// <c>CreateAgent("TriageAgent")</c> no longer resolves and <c>CreateAgent("Triage")</c> does.
    /// There is one published name, not two. The fully-qualified type name always resolves as well,
    /// regardless of this property.
    /// </para>
    /// <para>
    /// Names must be unique across all declared agents, exactly as class names must be; a collision
    /// is reported by <c>FDRYMAF031</c> at compile time and rejected when the factory is built.
    /// Because the name reaches the model as part of a handoff tool name, prefer characters a
    /// provider accepts in a function name — letters, digits, hyphens, and underscores. Nothing
    /// enforces that here, so a name containing other characters may be rejected by the provider
    /// rather than by Foundry.
    /// </para>
    /// </remarks>
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the system prompt instructions for this agent.
    /// </summary>
    public string? Instructions { get; set; }

    /// <summary>
    /// Gets or sets a human-readable description of this agent's purpose.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the function types whose <see cref="AgentFunctionAttribute"/>-tagged methods
    /// are wired as tools for this agent. When null and <see cref="FunctionGroups"/> is also null,
    /// all registered function types are used.
    /// </summary>
    public Type[]? FunctionTypes { get; set; }

    /// <summary>
    /// Gets or sets named function groups (registered via <see cref="AgentFunctionGroupAttribute"/>)
    /// whose types are wired as tools for this agent.
    /// </summary>
    public string[]? FunctionGroups { get; set; }
}
