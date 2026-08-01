using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text;

using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows.Declarative;
using Microsoft.Extensions.AI;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Workflows.Declarative;

/// <summary>
/// Resolves and invokes the agents named by a declarative workflow using agents the host already
/// owns, and holds the conversation state those workflows read through their expression language.
/// </summary>
/// <remarks>
/// <para>
/// Upstream's provider talks to a remote Azure AI Foundry project and requires agents to be deployed
/// there. This provider requires none of that: a declarative <c>InvokeAzureAgent</c> action resolves
/// through <see cref="ResponseAgentProvider.InvokeAgentAsync"/> regardless of where the agent lives,
/// so any registered <see cref="AIAgent"/> can serve one.
/// </para>
/// <para>
/// Conversation storage is not incidental. A workflow's <c>System.LastMessage</c> and the variable
/// typing that Power Fx checks are both derived from what this provider returns, so message identity,
/// chronological order, and paging semantics have to be correct or expressions fail to evaluate
/// rather than merely returning stale text.
/// </para>
/// <para>
/// State is held in memory for the lifetime of this instance. A host that needs conversations to
/// outlive the process should derive from <see cref="ResponseAgentProvider"/> directly rather than
/// layering persistence onto this type.
/// </para>
/// </remarks>
public sealed class FoundryAgentProvider : ResponseAgentProvider
{
    private readonly IDeclarativeWorkflowAgentRegistry _registry;
    private readonly ConcurrentDictionary<string, List<ChatMessage>> _conversations = new(StringComparer.Ordinal);

    /// <exception cref="ArgumentNullException"><paramref name="registry"/> is <see langword="null"/>.</exception>
    public FoundryAgentProvider(IDeclarativeWorkflowAgentRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);

        _registry = registry;
    }

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
    /// <exception cref="DeclarativeWorkflowAgentNotFoundException">
    /// The workflow named an agent that is not registered.
    /// </exception>
    public override async IAsyncEnumerable<AgentResponseUpdate> InvokeAgentAsync(
        string agentName,
        string? conversationId,
        string? additionalInstructions,
        IEnumerable<ChatMessage>? messages,
        IDictionary<string, object?>? arguments,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentName);

        if (!_registry.TryGetAgent(agentName, out var agent) || agent is null)
        {
            throw new DeclarativeWorkflowAgentNotFoundException(agentName);
        }

        var supplied = messages?.ToList() ?? [];
        var input = BuildAgentInput(conversationId, additionalInstructions, supplied);

        var responseText = new StringBuilder();
        await foreach (var update in agent
            .RunStreamingAsync(input, cancellationToken: cancellationToken)
            .WithCancellation(cancellationToken)
            .ConfigureAwait(false))
        {
            responseText.Append(update.Text);
            yield return update;
        }

        // The assistant turn is recorded only after the stream completes, so a caller that abandons
        // enumeration part-way never leaves a truncated turn behind for the next expression to read.
        if (conversationId is not null && responseText.Length > 0)
        {
            Append(conversationId, new ChatMessage(ChatRole.Assistant, responseText.ToString()));
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

    private List<ChatMessage> BuildAgentInput(
        string? conversationId,
        string? additionalInstructions,
        List<ChatMessage> supplied)
    {
        var input = new List<ChatMessage>();

        if (!string.IsNullOrWhiteSpace(additionalInstructions))
        {
            input.Add(new ChatMessage(ChatRole.System, additionalInstructions));
        }

        if (conversationId is null)
        {
            input.AddRange(supplied);
            return input;
        }

        var messages = GetConversation(conversationId);
        List<ChatMessage> history;
        lock (messages)
        {
            history = [.. messages];
        }

        input.AddRange(history);

        // The runtime may already have written this turn's input into the conversation before
        // invoking, so a supplied message that is already the recorded tail is context rather than a
        // new turn; appending it again would show the agent the same message twice.
        foreach (var message in supplied)
        {
            if (!ContainsEquivalent(history, message))
            {
                input.Add(Append(conversationId, message));
            }
        }

        return input;
    }

    private static bool ContainsEquivalent(List<ChatMessage> history, ChatMessage candidate) =>
        history.Any(existing =>
            existing.Role.Equals(candidate.Role) &&
            string.Equals(existing.Text, candidate.Text, StringComparison.Ordinal));
}
