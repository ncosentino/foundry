using Microsoft.Extensions.AI;

using NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Bundle;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Tests;

/// <summary>
/// Contrasts hybrid compaction against upstream compaction over the same deterministic two-round tool
/// loop. Upstream evaluates once per agent turn; hybrid evaluates once per provider request.
/// </summary>
public sealed class HarnessHybridCompactionBundleTests
{
    [Fact]
    public async Task Run_HybridCompactionEnabled_AssemblesContextForEveryProviderRound()
    {
        var reducer = new RecordingChatReducer();
        var toolCalls = 0;
        var tool = AIFunctionFactory.Create(
            () =>
            {
                Interlocked.Increment(ref toolCalls);
                return "tool-output";
            },
            "HybridTool");
        var chatClient = new TwoRoundToolLoopChatClient("HybridTool");

        var agent = new FoundryHarnessAgentFactory().Create(
            CreateConfiguration(chatClient, tool, reducer));
        await agent.RunAsync("go", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(2, chatClient.CallCount);
        Assert.Equal(1, toolCalls);
        Assert.Equal(2, reducer.Invocations.Count);
    }

    [Fact]
    public async Task Run_HybridCompactionEnabled_ObservesToolExchangeOnTheSecondRound()
    {
        var reducer = new RecordingChatReducer();
        var tool = AIFunctionFactory.Create(() => "tool-output", "HybridTool");
        var chatClient = new TwoRoundToolLoopChatClient("HybridTool");

        var agent = new FoundryHarnessAgentFactory().Create(
            CreateConfiguration(chatClient, tool, reducer));
        await agent.RunAsync("go", cancellationToken: TestContext.Current.CancellationToken);

        var firstRound = reducer.Invocations[0];
        var secondRound = reducer.Invocations[1];

        Assert.DoesNotContain(
            firstRound.SelectMany(message => message.Contents),
            content => content is FunctionCallContent or FunctionResultContent);
        Assert.Contains(
            secondRound.SelectMany(message => message.Contents),
            content => content is FunctionCallContent);
        Assert.Contains(
            secondRound.SelectMany(message => message.Contents),
            content => content is FunctionResultContent);
    }

    [Fact]
    public void Create_HybridCompactionEnabledWithoutOptions_FailsClosed()
    {
        var configuration = HarnessBundleTestsHelpers.CreateBaseline(
            HarnessBundleTestsHelpers.AllFeaturesDisabled() with
            {
                EnableHybridCompaction = true,
            });

        var exception = Assert.Throws<ArgumentException>(
            () => new FoundryHarnessAgentFactory().Create(configuration));
        Assert.Contains("HybridCompactionOptions was not supplied", exception.Message);
    }

    [Fact]
    public void Create_HybridCompactionOptionsWithoutFeature_FailsClosed()
    {
        var configuration = HarnessBundleTestsHelpers.CreateBaseline() with
        {
            HybridCompactionOptions = CreateOptions(new RecordingChatReducer()),
        };

        var exception = Assert.Throws<ArgumentException>(
            () => new FoundryHarnessAgentFactory().Create(configuration));
        Assert.Contains("Features.EnableHybridCompaction is false", exception.Message);
    }

    [Fact]
    public void DescribeEffectiveDefaults_HybridCompactionDisabled_ReportsDisabled()
    {
        var defaults = new FoundryHarnessAgentFactory().DescribeEffectiveDefaults(
            HarnessBundleTestsHelpers.CreateBaseline());

        var disposition = defaults.GetDisposition(FoundryHarnessFeature.HybridCompaction);

        Assert.Equal(FoundryHarnessFeatureEffectiveState.Disabled, disposition.EffectiveState);
    }

    [Fact]
    public void DescribeEffectiveDefaults_UpstreamCompaction_DisclosesPerTurnLimitation()
    {
        var configuration = HarnessBundleTestsHelpers.CreateBaseline(
            HarnessBundleTestsHelpers.AllFeaturesDisabled() with { EnableCompaction = true }) with
        {
            MaxContextWindowTokens = 16000,
            MaxOutputTokens = 1024,
        };

        var defaults = new FoundryHarnessAgentFactory().DescribeEffectiveDefaults(configuration);
        var disposition = defaults.GetDisposition(FoundryHarnessFeature.Compaction);

        Assert.Equal(FoundryHarnessFeatureEffectiveState.Enabled, disposition.EffectiveState);
        Assert.NotNull(disposition.Limitation);
        Assert.Contains("once per agent turn", disposition.Limitation);
    }

    [Fact]
    public void DescribeEffectiveDefaults_HybridCompactionEnabled_DisclosesLimitation()
    {
        var configuration = HarnessBundleTestsHelpers.CreateBaseline(
            HarnessBundleTestsHelpers.AllFeaturesDisabled() with
            {
                EnableHybridCompaction = true,
            }) with
        {
            HybridCompactionOptions = CreateOptions(new RecordingChatReducer()),
        };

        var defaults = new FoundryHarnessAgentFactory().DescribeEffectiveDefaults(configuration);
        var disposition = defaults.GetDisposition(FoundryHarnessFeature.HybridCompaction);

        Assert.Equal(FoundryHarnessFeatureEffectiveState.Enabled, disposition.EffectiveState);
        Assert.NotNull(disposition.Limitation);
        Assert.Contains("UTF-8 bytes", disposition.Limitation);
    }

    private static FoundryHarnessAgentConfiguration CreateConfiguration(
        IChatClient chatClient,
        AITool tool,
        IChatReducer reducer) =>
        HarnessBundleTestsHelpers.CreateBaseline(
            HarnessBundleTestsHelpers.AllFeaturesDisabled() with
            {
                EnableHybridCompaction = true,
            }) with
        {
            ChatClient = chatClient,
            Tools = [tool],
            HybridCompactionOptions = CreateOptions(reducer),
        };

    /// <remarks>
    /// The trigger margin is one byte below the hard limit so the threshold is effectively zero and
    /// every provider call reaches the reducer. That makes reducer invocations a direct count of
    /// assembled provider rounds, which is the property under test; the reducer proposes no change so
    /// the assertions do not also depend on a particular reduction converging.
    /// </remarks>
    private static FoundryHarnessHybridCompactionOptions CreateOptions(IChatReducer reducer) =>
        new()
        {
            HardLimitBytes = 1_000_000,
            TriggerMarginBytes = 999_999,
            RecentMessageRetentionCount = 2,
            MaximumCompactionAttempts = 3,
            UpstreamReducer = reducer,
        };

    private sealed class RecordingChatReducer : IChatReducer
    {
        private readonly List<IReadOnlyList<ChatMessage>> _invocations = [];

        public IReadOnlyList<IReadOnlyList<ChatMessage>> Invocations
        {
            get
            {
                lock (_invocations)
                {
                    return _invocations.ToList();
                }
            }
        }

        public Task<IEnumerable<ChatMessage>> ReduceAsync(
            IEnumerable<ChatMessage> messages,
            CancellationToken cancellationToken)
        {
            var materialized = messages.ToList();
            lock (_invocations)
            {
                _invocations.Add(materialized);
            }

            return Task.FromResult<IEnumerable<ChatMessage>>(materialized);
        }
    }

    private sealed class TwoRoundToolLoopChatClient(string functionName) : IChatClient
    {
        private int _callCount;

        public int CallCount => Volatile.Read(ref _callCount);

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(
                Interlocked.Increment(ref _callCount) == 1
                    ? new ChatResponse(
                        new ChatMessage(
                            ChatRole.Assistant,
                            [
                                new FunctionCallContent(
                                    "hybrid-call",
                                    functionName,
                                    new Dictionary<string, object?>()),
                            ]))
                    : new ChatResponse(new ChatMessage(ChatRole.Assistant, "done")));

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
