using Microsoft.Extensions.AI;

using NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Context;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Tests.Harness;

public sealed class HarnessUpstreamChatReducerAdapterTests
{
    [Fact]
    public async Task ReduceAsync_UnchangedMessages_ReusesOriginalEntryInstancesVerbatim()
    {
        var system = HarnessCompactionTestFixture.SystemEntry("system-1", "be helpful");
        var conversational = HarnessCompactionTestFixture.ConversationalEntry("conv-1", ChatRole.User, "hello");
        var request = HarnessContextReductionRequest.Create(
            [system, conversational], [system.EntryId], HarnessCompactionTestFixture.CreatePolicy(
                hardLimit: 1000, triggerMargin: 10, recentMessageRetentionCount: 5, maximumCompactionAttempts: 3,
                new HarnessFixedSizeContextEstimator(new Dictionary<string, int>())), attemptNumber: 1);

        var echo = HarnessScriptedUpstreamChatReducer.Echo();
        var bridge = new HarnessUpstreamChatReducerAdapter(echo);

        var result = await bridge.ReduceAsync(request, CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.Same(request.Entries[0], result[0]);
        Assert.Same(request.Entries[1], result[1]);
        Assert.Equal(1, echo.InvocationCount);
    }

    [Fact]
    public async Task ReduceAsync_DroppedMessage_OmitsCorrespondingEntry()
    {
        var system = HarnessCompactionTestFixture.SystemEntry("system-1", "be helpful");
        var conversational = HarnessCompactionTestFixture.ConversationalEntry("conv-1", ChatRole.User, "drop me");
        var request = HarnessContextReductionRequest.Create(
            [system, conversational], [system.EntryId], HarnessCompactionTestFixture.CreatePolicy(
                1000, 10, 5, 3, new HarnessFixedSizeContextEstimator(new Dictionary<string, int>())), 1);

        var reducer = new HarnessScriptedUpstreamChatReducer(
            (messages, _) => Task.FromResult(messages.Where(m => m.Role == ChatRole.System)));
        var bridge = new HarnessUpstreamChatReducerAdapter(reducer);

        var result = await bridge.ReduceAsync(request, CancellationToken.None);

        var resultEntry = Assert.Single(result);
        Assert.Same(request.Entries[0], resultEntry);
    }

    [Fact]
    public async Task ReduceAsync_NewNonToolMessage_IsLabeledSummaryWithFreshDeterministicId()
    {
        var conversational = HarnessCompactionTestFixture.ConversationalEntry("conv-1", ChatRole.User, "old content");
        var request = HarnessContextReductionRequest.Create(
            [conversational], [], HarnessCompactionTestFixture.CreatePolicy(
                1000, 10, 5, 3, new HarnessFixedSizeContextEstimator(new Dictionary<string, int>())), 1);

        var reducer = new HarnessScriptedUpstreamChatReducer(
            (_, _) => Task.FromResult<IEnumerable<ChatMessage>>(
                [new ChatMessage(ChatRole.Assistant, "a brief summary")]));
        var bridge = new HarnessUpstreamChatReducerAdapter(reducer);

        var result = await bridge.ReduceAsync(request, CancellationToken.None);

        var summaryEntry = Assert.Single(result);
        Assert.Equal(HarnessContextEntryKind.Summary, summaryEntry.Kind);
        Assert.NotEqual(conversational.EntryId, summaryEntry.EntryId);

        // Deterministic: an identical proposal in a second call mints the identical new id.
        var secondResult = await bridge.ReduceAsync(request, CancellationToken.None);
        Assert.Equal(summaryEntry.EntryId, Assert.Single(secondResult).EntryId);
    }

    [Fact]
    public async Task ReduceAsync_FabricatedToolContentNotMatchingOriginal_ThrowsContractException()
    {
        var conversational = HarnessCompactionTestFixture.ConversationalEntry("conv-1", ChatRole.User, "hi");
        var request = HarnessContextReductionRequest.Create(
            [conversational], [], HarnessCompactionTestFixture.CreatePolicy(
                1000, 10, 5, 3, new HarnessFixedSizeContextEstimator(new Dictionary<string, int>())), 1);

        var reducer = new HarnessScriptedUpstreamChatReducer(
            (_, _) => Task.FromResult<IEnumerable<ChatMessage>>(
                [new ChatMessage(
                    ChatRole.Assistant,
                    [new FunctionCallContent("forged-call", "forged-tool", new Dictionary<string, object?>())])]));
        var bridge = new HarnessUpstreamChatReducerAdapter(reducer);

        await Assert.ThrowsAsync<HarnessCompactionReducerContractException>(
            () => bridge.ReduceAsync(request, CancellationToken.None));
    }

    [Fact]
    public async Task ReduceAsync_NullReturnedSequence_ThrowsContractException()
    {
        var conversational = HarnessCompactionTestFixture.ConversationalEntry("conv-1", ChatRole.User, "hi");
        var request = HarnessContextReductionRequest.Create(
            [conversational], [], HarnessCompactionTestFixture.CreatePolicy(
                1000, 10, 5, 3, new HarnessFixedSizeContextEstimator(new Dictionary<string, int>())), 1);

        var reducer = new HarnessScriptedUpstreamChatReducer(
            (_, _) => Task.FromResult<IEnumerable<ChatMessage>>(null!));
        var bridge = new HarnessUpstreamChatReducerAdapter(reducer);

        await Assert.ThrowsAsync<HarnessCompactionReducerContractException>(
            () => bridge.ReduceAsync(request, CancellationToken.None));
    }

    [Fact]
    public async Task ReduceAsync_UpstreamReducerThrowsOperationCanceled_PropagatesWithoutWrapping()
    {
        var conversational = HarnessCompactionTestFixture.ConversationalEntry("conv-1", ChatRole.User, "hi");
        var request = HarnessContextReductionRequest.Create(
            [conversational], [], HarnessCompactionTestFixture.CreatePolicy(
                1000, 10, 5, 3, new HarnessFixedSizeContextEstimator(new Dictionary<string, int>())), 1);

        using var cts = new CancellationTokenSource();
        var reducer = new HarnessScriptedUpstreamChatReducer(
            (_, ct) =>
            {
                cts.Cancel();
                ct.ThrowIfCancellationRequested();
                return Task.FromResult<IEnumerable<ChatMessage>>([]);
            });
        var bridge = new HarnessUpstreamChatReducerAdapter(reducer);

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => bridge.ReduceAsync(request, cts.Token));
    }
}
