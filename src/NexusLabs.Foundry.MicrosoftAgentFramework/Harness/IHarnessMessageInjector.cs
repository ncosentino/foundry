using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness;

internal interface IHarnessMessageInjector
{
    Task EnqueueMessagesAsync(
        AgentSession session,
        IEnumerable<ChatMessage> messages,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ChatMessage>> GetPendingMessagesAsync(
        AgentSession session,
        CancellationToken cancellationToken);
}
