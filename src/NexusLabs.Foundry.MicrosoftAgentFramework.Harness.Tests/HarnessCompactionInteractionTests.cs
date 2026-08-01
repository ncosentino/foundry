using Microsoft.Agents.AI.Compaction;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

using NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Bundle;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Tests;

/// <summary>
/// Pins how the two compaction dimensions relate: they are independent rather than layered, neither
/// suppresses the other, and they act on different things — upstream on what is remembered, hybrid on
/// what is sent.
/// </summary>
public sealed class HarnessCompactionInteractionTests
{
    [Theory]
    [InlineData(false, false, 0, 0)]
    [InlineData(true, false, 1, 0)]
    [InlineData(false, true, 0, 2)]
    [InlineData(true, true, 1, 2)]
    public async Task Run_CompactionCombination_RunsEachDimensionIndependently(
        bool upstreamEnabled,
        bool hybridEnabled,
        int expectedUpstreamCompactions,
        int expectedHybridAssemblies)
    {
        var strategy = new AlwaysFiringCompactionStrategy();
        var reducer = new PassthroughRecordingChatReducer();
        var chatClient = new ToolLoopChatClient("InteractionTool", toolRounds: 1);
        var tool = AIFunctionFactory.Create(() => "tool-output", "InteractionTool");

        var configuration = HarnessBundleTestsHelpers.CreateBaseline(
            HarnessBundleTestsHelpers.AllFeaturesDisabled() with
            {
                EnableCompaction = upstreamEnabled,
                EnableHybridCompaction = hybridEnabled,
            }) with
        {
            ChatClient = chatClient,
            Tools = [tool],
            CompactionStrategy = upstreamEnabled ? strategy : null,
            HybridCompactionOptions = hybridEnabled ? CreateOptions(reducer) : null,
        };

        var agent = new FoundryHarnessAgentFactory().Create(configuration);
        await agent.RunAsync("go", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(2, chatClient.CallCount);
        Assert.Equal(expectedUpstreamCompactions, strategy.CompactionCount);
        Assert.Equal(expectedHybridAssemblies, reducer.InputCounts.Count);
    }

    [Fact]
    public async Task Run_BothEnabled_UpstreamStillSeesOnlyThePreToolLoopState()
    {
        var strategy = new AlwaysFiringCompactionStrategy();
        var reducer = new PassthroughRecordingChatReducer();
        var chatClient = new ToolLoopChatClient("InteractionTool", toolRounds: 1);
        var tool = AIFunctionFactory.Create(() => "tool-output", "InteractionTool");

        var configuration = HarnessBundleTestsHelpers.CreateBaseline(
            HarnessBundleTestsHelpers.AllFeaturesDisabled() with
            {
                EnableCompaction = true,
                EnableHybridCompaction = true,
            }) with
        {
            ChatClient = chatClient,
            Tools = [tool],
            CompactionStrategy = strategy,
            HybridCompactionOptions = CreateOptions(reducer),
        };

        var agent = new FoundryHarnessAgentFactory().Create(configuration);
        await agent.RunAsync("go", cancellationToken: TestContext.Current.CancellationToken);

        // Enabling hybrid compaction does not extend upstream's reach: it still runs once, against
        // the state that preceded the tool loop, exactly as it does when hybrid is off.
        var upstreamIndexSize = Assert.Single(strategy.ObservedIndexSizes);
        Assert.Equal(2, upstreamIndexSize);
        Assert.Equal(2, reducer.InputCounts.Count);
    }

    /// <remarks>
    /// Hybrid compaction is installed inner to the per-service-call history decorator, so history is
    /// persisted before a reduction is applied and every call re-assembles from the full record. A
    /// third round is required to observe this: the second round is the first that can drop anything,
    /// so only the third round can reveal whether that drop persisted.
    /// </remarks>
    [Fact]
    public async Task Run_HybridCompaction_BoundsTheDispatchWithoutShrinkingStoredHistory()
    {
        var reducer = new DropOldestRecordingChatReducer();
        var chatClient = new ToolLoopChatClient("InteractionTool", toolRounds: 2);
        var tool = AIFunctionFactory.Create(() => "tool-output", "InteractionTool");

        var configuration = HarnessBundleTestsHelpers.CreateBaseline(
            HarnessBundleTestsHelpers.AllFeaturesDisabled() with
            {
                EnableHybridCompaction = true,
            }) with
        {
            ChatClient = chatClient,
            Tools = [tool],
            HybridCompactionOptions = CreateOptions(reducer),
        };

        var agent = new FoundryHarnessAgentFactory().Create(configuration);
        await agent.RunAsync("go", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal([1, 3, 5], reducer.InputCounts);
        Assert.Equal([1, 2, 4], reducer.OutputCounts);
        Assert.Equal([1, 2, 4], chatClient.ReceivedCounts);
    }

    private static FoundryHarnessHybridCompactionOptions CreateOptions(IChatReducer reducer) =>
        new()
        {
            HardLimitBytes = 1_000_000,
            TriggerMarginBytes = 999_999,
            RecentMessageRetentionCount = 1,
            MaximumCompactionAttempts = 3,
            UpstreamReducer = reducer,
        };

    private sealed class AlwaysFiringCompactionStrategy : CompactionStrategy
    {
        public AlwaysFiringCompactionStrategy()
            : base(_ => true, _ => true)
        {
        }

        public int CompactionCount { get; private set; }

        public List<int> ObservedIndexSizes { get; } = [];

        protected override ValueTask<bool> CompactCoreAsync(
            CompactionMessageIndex index,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            CompactionCount++;
            ObservedIndexSizes.Add(index.IncludedMessageCount);
            return ValueTask.FromResult(false);
        }
    }

    private sealed class PassthroughRecordingChatReducer : IChatReducer
    {
        public List<int> InputCounts { get; } = [];

        public Task<IEnumerable<ChatMessage>> ReduceAsync(
            IEnumerable<ChatMessage> messages,
            CancellationToken cancellationToken)
        {
            var materialized = messages.ToList();
            InputCounts.Add(materialized.Count);
            return Task.FromResult<IEnumerable<ChatMessage>>(materialized);
        }
    }

    private sealed class DropOldestRecordingChatReducer : IChatReducer
    {
        public List<int> InputCounts { get; } = [];

        public List<int> OutputCounts { get; } = [];

        public Task<IEnumerable<ChatMessage>> ReduceAsync(
            IEnumerable<ChatMessage> messages,
            CancellationToken cancellationToken)
        {
            var materialized = messages.ToList();
            InputCounts.Add(materialized.Count);
            var kept = materialized.Count > 1 ? materialized.Skip(1).ToList() : materialized;
            OutputCounts.Add(kept.Count);
            return Task.FromResult<IEnumerable<ChatMessage>>(kept);
        }
    }

    private sealed class ToolLoopChatClient(string functionName, int toolRounds) : IChatClient
    {
        private int _callCount;

        public int CallCount => Volatile.Read(ref _callCount);

        public List<int> ReceivedCounts { get; } = [];

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            ReceivedCounts.Add(messages.Count());
            var call = Interlocked.Increment(ref _callCount);
            return Task.FromResult(
                call <= toolRounds
                    ? new ChatResponse(
                        new ChatMessage(
                            ChatRole.Assistant,
                            [
                                new FunctionCallContent(
                                    $"interaction-call-{call}",
                                    functionName,
                                    new Dictionary<string, object?>()),
                            ]))
                    : new ChatResponse(new ChatMessage(ChatRole.Assistant, "done")));
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("Streaming is not required by these tests.");

        public object? GetService(Type serviceType, object? key) => null;

        public void Dispose()
        {
        }
    }
}
