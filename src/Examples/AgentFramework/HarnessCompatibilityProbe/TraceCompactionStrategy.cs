using Microsoft.Agents.AI.Compaction;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace HarnessCompatibilityProbe;

internal sealed class TraceCompactionStrategy(
    LifecycleTrace lifecycleTrace) : CompactionStrategy(CompactionTriggers.Always)
{
    protected override ValueTask<bool> CompactCoreAsync(
        CompactionMessageIndex index,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var messages = index.GetIncludedMessages().ToList();
        var hasFunctionCall = messages
            .SelectMany(message => message.Contents)
            .OfType<FunctionCallContent>()
            .Any();
        var hasFunctionResult = messages
            .SelectMany(message => message.Contents)
            .OfType<FunctionResultContent>()
            .Any();

        lifecycleTrace.Add(
            $"compaction:call={hasFunctionCall}:result={hasFunctionResult}:messages={messages.Count}");
        return ValueTask.FromResult(false);
    }
}
