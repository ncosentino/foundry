using Microsoft.Agents.AI;

using NexusLabs.Foundry.MicrosoftAgentFramework.Context;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Providers;

internal sealed class HarnessAgentModeAccessor(
    AgentModeProvider provider,
    HarnessExecutionBinding binding,
    IAgentExecutionContextAccessor executionContextAccessor,
    string sessionId) : IHarnessAgentModeAccessor
{
    async Task<string> IHarnessAgentModeAccessor.GetModeAsync(
        AgentSession session,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);
        binding.EnsureCurrent(executionContextAccessor, sessionId);
        var mode = await provider
            .GetModeAsync(session, cancellationToken)
            .ConfigureAwait(false);
        binding.EnsureCurrent(executionContextAccessor, sessionId);
        return mode;
    }

    async Task IHarnessAgentModeAccessor.SetModeAsync(
        AgentSession session,
        string mode,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentException.ThrowIfNullOrWhiteSpace(mode);
        binding.EnsureCurrent(executionContextAccessor, sessionId);
        await provider
            .SetModeAsync(session, mode, cancellationToken)
            .ConfigureAwait(false);
        binding.EnsureCurrent(executionContextAccessor, sessionId);
    }
}
