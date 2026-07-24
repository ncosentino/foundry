using Microsoft.Agents.AI;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Providers;

internal interface IHarnessTodoAccessor
{
    Task<IReadOnlyList<HarnessTodoItemSnapshot>> GetAllTodosAsync(
        AgentSession session,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<HarnessTodoItemSnapshot>> GetRemainingTodosAsync(
        AgentSession session,
        CancellationToken cancellationToken);
}
