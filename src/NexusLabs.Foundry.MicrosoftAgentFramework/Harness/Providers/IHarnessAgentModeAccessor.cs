using Microsoft.Agents.AI;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Providers;

internal interface IHarnessAgentModeAccessor
{
    Task<string> GetModeAsync(
        AgentSession session,
        CancellationToken cancellationToken);

    Task SetModeAsync(
        AgentSession session,
        string mode,
        CancellationToken cancellationToken);
}
