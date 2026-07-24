using Microsoft.Agents.AI;

using NexusLabs.Foundry.MicrosoftAgentFramework.Context;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Providers;

internal sealed class HarnessTodoAccessor(
    TodoProvider provider,
    HarnessExecutionBinding binding,
    IAgentExecutionContextAccessor executionContextAccessor,
    string sessionId) : IHarnessTodoAccessor
{
    async Task<IReadOnlyList<HarnessTodoItemSnapshot>>
        IHarnessTodoAccessor.GetAllTodosAsync(
        AgentSession session,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);
        binding.EnsureCurrent(executionContextAccessor, sessionId);
        var todos = await provider
            .GetAllTodosAsync(session, cancellationToken)
            .ConfigureAwait(false);
        binding.EnsureCurrent(executionContextAccessor, sessionId);
        return [.. todos.Select(ToSnapshot)];
    }

    async Task<IReadOnlyList<HarnessTodoItemSnapshot>>
        IHarnessTodoAccessor.GetRemainingTodosAsync(
            AgentSession session,
            CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);
        binding.EnsureCurrent(executionContextAccessor, sessionId);
        var todos = await provider
            .GetRemainingTodosAsync(session, cancellationToken)
            .ConfigureAwait(false);
        binding.EnsureCurrent(executionContextAccessor, sessionId);
        return [.. todos.Select(ToSnapshot)];
    }

    private static HarnessTodoItemSnapshot ToSnapshot(TodoItem item) =>
        new(item.Id, item.Title, item.Description, item.IsComplete);
}
