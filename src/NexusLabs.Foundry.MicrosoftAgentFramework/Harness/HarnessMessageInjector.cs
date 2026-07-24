using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

using NexusLabs.Foundry.MicrosoftAgentFramework.Context;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness;

internal sealed class HarnessMessageInjector(
    MessageInjectingChatClient innerClient,
    HarnessExecutionBinding binding,
    IAgentExecutionContextAccessor executionContextAccessor,
    string sessionId) : IHarnessMessageInjector
{
    async Task IHarnessMessageInjector.EnqueueMessagesAsync(
        AgentSession session,
        IEnumerable<ChatMessage> messages,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(messages);
        binding.EnsureCurrent(executionContextAccessor, sessionId);
        await innerClient
            .EnqueueMessagesAsync(session, messages, cancellationToken)
            .ConfigureAwait(false);
    }

    async Task<IReadOnlyList<ChatMessage>>
        IHarnessMessageInjector.GetPendingMessagesAsync(
            AgentSession session,
            CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);
        binding.EnsureCurrent(executionContextAccessor, sessionId);
        var messages = await innerClient
            .GetPendingMessagesAsync(session, cancellationToken)
            .ConfigureAwait(false);
        binding.EnsureCurrent(executionContextAccessor, sessionId);
        return messages;
    }
}
