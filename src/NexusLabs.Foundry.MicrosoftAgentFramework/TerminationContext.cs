using Microsoft.Extensions.AI;

namespace NexusLabs.Foundry.MicrosoftAgentFramework;

/// <summary>
/// Provides context to an <see cref="IWorkflowTerminationCondition"/> when evaluating whether a
/// workflow should stop after an agent's response.
/// </summary>
public sealed class TerminationContext
{
    /// <summary>
    /// Gets the identity of the agent that produced this response, which a condition compares
    /// against to scope itself to one agent.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Scoping is the condition's own job: a condition is offered every agent's turn regardless of
    /// which class declared it, so this is the only thing distinguishing them.
    /// </para>
    /// <para>
    /// The value differs by layer, because the two evaluate at different points and have different
    /// information to hand. Under <see cref="AgentTerminationConditionAttribute"/> it is the author
    /// of the turn's last message, which is the agent's published name — the class name, or
    /// <see cref="FoundryAgentAttribute.Name"/> when one is declared. Under
    /// <see cref="WorkflowRunTerminationConditionAttribute"/> it is the workflow executor id.
    /// A condition written against one layer will not necessarily match under the other.
    /// </para>
    /// <para>
    /// It is empty when the turn's message carries no author, which happens for the initial input
    /// message. A condition that compares this value must therefore tolerate an empty string rather
    /// than assuming an agent name is always present.
    /// </para>
    /// </remarks>
    public required string AgentId { get; init; }

    /// <summary>
    /// Gets the last <see cref="ChatMessage"/> emitted by the agent for this turn
    /// (preserving full content including function calls, role, and metadata), or
    /// <see langword="null"/> if the agent produced no message. Use the <c>.Text</c>
    /// property on the message for a flat text view.
    /// </summary>
    public ChatMessage? LastMessage { get; init; }

    /// <summary>Gets the number of agent turns completed so far (1-based).</summary>
    public required int TurnCount { get; init; }

    /// <summary>
    /// Gets the accumulated conversation history up to and including this turn.
    /// Each entry corresponds to one completed agent response.
    /// </summary>
    public required IReadOnlyList<ChatMessage> ConversationHistory { get; init; }

    /// <summary>
    /// Gets token usage for this turn, if reported by the model. May be <see langword="null"/>
    /// when the model does not return usage metadata.
    /// </summary>
    public UsageDetails? Usage { get; init; }

    /// <summary>
    /// Gets the names of tools/functions called by the agent during this turn.
    /// Extracted from <see cref="FunctionCallContent"/> entries in the last message's
    /// <see cref="ChatMessage.Contents"/>. Empty if no tool calls were made.
    /// </summary>
    public IReadOnlyList<string> ToolCallNames { get; init; } = [];
}
