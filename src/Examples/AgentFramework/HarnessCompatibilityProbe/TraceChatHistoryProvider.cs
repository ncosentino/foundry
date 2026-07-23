using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace HarnessCompatibilityProbe;

internal sealed class TraceChatHistoryProvider(
    LifecycleTrace lifecycleTrace) : ChatHistoryProvider
{
    protected override ValueTask<IEnumerable<ChatMessage>> ProvideChatHistoryAsync(
        InvokingContext context,
        CancellationToken cancellationToken)
    {
        lifecycleTrace.Add("history.provide");
        return ValueTask.FromResult<IEnumerable<ChatMessage>>(
            [new ChatMessage(ChatRole.User, "history-probe")]);
    }

    protected override ValueTask StoreChatHistoryAsync(
        InvokedContext context,
        CancellationToken cancellationToken)
    {
        lifecycleTrace.Add(
            $"history.store:{context.RequestMessages.Count()}:{context.ResponseMessages?.Count() ?? 0}");
        return ValueTask.CompletedTask;
    }
}
