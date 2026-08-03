using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows.Declarative;
using Microsoft.Extensions.AI;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Workflows.Declarative;

/// <summary>
/// Resolves the agents a declarative workflow names against Foundry's declared agents, and holds the
/// conversation state the runtime records against.
/// </summary>
/// <remarks>
/// <para>
/// Upstream's provider resolves agent names against a remote Azure AI Foundry project and requires
/// agents to be deployed there. A declarative <c>InvokeAzureAgent</c> action actually resolves
/// through <see cref="ResponseAgentProvider.InvokeAgentAsync"/> regardless of where the agent lives,
/// so this provider resolves names through <see cref="IAgentFactory"/> instead — the same
/// <c>[FoundryAgent]</c> declarations and source-generated registration the rest of Foundry uses.
/// A workflow document therefore names an agent by its published name, exactly as
/// <see cref="IAgentFactory.CreateAgent(string)"/> already expects.
/// </para>
/// <para>
/// Conversation storage exists because the runtime writes to it, not because this provider reads it:
/// the runtime records both the user and the assistant turn through <see cref="CreateMessageAsync"/>
/// and invokes agents with the messages the document's input expression resolved to. Message
/// identity and chronological order still have to be correct, because the runtime addresses stored
/// messages by id and pages over them.
/// </para>
/// <para>
/// State is held in memory for the lifetime of this instance. A host needing conversations to
/// outlive the process should derive from <see cref="ResponseAgentProvider"/> directly rather than
/// layering persistence onto this type.
/// </para>
/// </remarks>
public sealed class FoundryAgentProvider : ResponseAgentProvider
{
    private readonly IAgentFactory _agentFactory;
    private readonly ConcurrentDictionary<string, List<ChatMessage>> _conversations = new(StringComparer.Ordinal);

    /// <exception cref="ArgumentNullException"><paramref name="agentFactory"/> is <see langword="null"/>.</exception>
    public FoundryAgentProvider(IAgentFactory agentFactory)
    {
        ArgumentNullException.ThrowIfNull(agentFactory);

        _agentFactory = agentFactory;
    }

    /// <summary>Gets the identifiers of every conversation this provider has created.</summary>
    public IReadOnlyCollection<string> ConversationIds => _conversations.Keys.ToList();

    /// <inheritdoc />
    public override Task<string> CreateConversationAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var conversationId = $"foundry-declarative-{Guid.NewGuid():N}";
        _conversations[conversationId] = [];
        return Task.FromResult(conversationId);
    }

    /// <inheritdoc />
    public override Task<ChatMessage> CreateMessageAsync(
        string conversationId,
        ChatMessage message,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(conversationId);
        ArgumentNullException.ThrowIfNull(message);
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(Append(conversationId, message));
    }

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">No message with that identifier exists.</exception>
    public override Task<ChatMessage> GetMessageAsync(
        string conversationId,
        string messageId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(conversationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
        cancellationToken.ThrowIfCancellationRequested();

        var messages = GetConversation(conversationId);
        lock (messages)
        {
            var match = messages.FirstOrDefault(
                m => string.Equals(m.MessageId, messageId, StringComparison.Ordinal));
            return match is null
                ? throw new InvalidOperationException(
                    $"Conversation '{conversationId}' contains no message '{messageId}'.")
                : Task.FromResult(match);
        }
    }

    /// <inheritdoc />
    public override async IAsyncEnumerable<ChatMessage> GetMessagesAsync(
        string conversationId,
        int? limit,
        string? after,
        string? before,
        bool newestFirst,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(conversationId);

        await Task.CompletedTask.ConfigureAwait(false);

        List<ChatMessage> snapshot;
        var messages = GetConversation(conversationId);
        lock (messages)
        {
            snapshot = [.. messages];
        }

        // Anchors are applied against chronological order first, so that `after`/`before` mean the
        // same span regardless of the direction the caller wants the results in.
        var startExclusive = IndexOf(snapshot, after);
        var endExclusive = IndexOf(snapshot, before);
        var window = snapshot
            .Skip(startExclusive + 1)
            .Take(endExclusive < 0 ? snapshot.Count : Math.Max(0, endExclusive - startExclusive - 1));

        if (newestFirst)
        {
            window = window.Reverse();
        }

        if (limit is { } maximum && maximum >= 0)
        {
            window = window.Take(maximum);
        }

        foreach (var message in window)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return message;
        }
    }

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">
    /// The workflow named an agent that is not declared or registered. The message names the agent
    /// and how to register it.
    /// </exception>
    /// <remarks>
    /// The messages supplied by the runtime are passed to the agent as-is. Conversation history is
    /// deliberately not merged in, and the resulting turn is deliberately not recorded here: the
    /// runtime records both turns itself through <see cref="CreateMessageAsync"/> and supplies
    /// exactly the messages the document's <c>input</c> expression resolved to. Merging history would
    /// show the agent messages the document did not ask for, and recording the turn would duplicate
    /// what the runtime already stored.
    /// </remarks>
    public override async IAsyncEnumerable<AgentResponseUpdate> InvokeAgentAsync(
        string agentName,
        string? conversationId,
        string? additionalInstructions,
        IEnumerable<ChatMessage>? messages,
        IDictionary<string, object?>? arguments,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentName);

        // Resolution and its failure message both come from IAgentFactory, so an unregistered agent
        // reports the same way here as anywhere else in Foundry.
        AIAgent agent = _agentFactory.CreateAgent(agentName);

        var input = new List<ChatMessage>();
        if (!string.IsNullOrWhiteSpace(additionalInstructions))
        {
            input.Add(new ChatMessage(ChatRole.System, additionalInstructions));
        }

        if (messages is not null)
        {
            input.AddRange(messages);
        }

        await foreach (var update in agent
            .RunStreamingAsync(input, cancellationToken: cancellationToken)
            .WithCancellation(cancellationToken)
            .ConfigureAwait(false))
        {
            yield return update;
        }
    }

    private static int IndexOf(List<ChatMessage> messages, string? messageId) =>
        string.IsNullOrEmpty(messageId)
            ? -1
            : messages.FindIndex(m => string.Equals(m.MessageId, messageId, StringComparison.Ordinal));

    private List<ChatMessage> GetConversation(string conversationId) =>
        _conversations.GetOrAdd(conversationId, static _ => []);

    private ChatMessage Append(string conversationId, ChatMessage message)
    {
        // A message without an identifier cannot be addressed by GetMessageAsync or used as a paging
        // anchor, so one is assigned rather than leaving later lookups to fail.
        message.MessageId ??= $"msg-{Guid.NewGuid():N}";

        var messages = GetConversation(conversationId);
        lock (messages)
        {
            messages.Add(message);
        }

        return message;
    }
}
