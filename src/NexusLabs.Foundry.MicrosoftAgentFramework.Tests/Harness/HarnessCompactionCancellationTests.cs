using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

using NexusLabs.Foundry.MicrosoftAgentFramework.Context;
using NexusLabs.Foundry.MicrosoftAgentFramework.Harness;
using NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Capabilities;
using NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Context;
using NexusLabs.Foundry.MicrosoftAgentFramework.Workspace;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Tests.Harness;

/// <summary>
/// Proves that every cancellation surface named for the hybrid compaction seam — a pre-canceled token
/// (non-streaming and streaming), cancellation raised by the upstream reducer itself, cancellation
/// observed at the exact instant assembly finishes but before dispatch, cancellation during a
/// message-injection-induced extra provider call, cancellation during snapshot/finalization capture, and
/// trust-binding invalidation between successful assembly and dispatch (non-streaming and streaming) —
/// always surfaces <see cref="OperationCanceledException"/> or <see cref="InvalidOperationException"/> as
/// appropriate, and never a successful agent response or a silently swallowed fallback. None of these
/// paths tolerates a broad catch.
/// </summary>
public sealed class HarnessCompactionCancellationTests
{
    [Fact]
    public async Task GetResponseAsync_PreCanceledToken_ThrowsOperationCanceled_LeafNeverInvoked()
    {
        using var services = HarnessCompositionTestFixture.CreateServices();
        var accessor = new AgentExecutionContextAccessor();
        var binding = HarnessCompositionTestFixture.CaptureBinding(accessor, out var scope);
        using (scope)
        {
            var leaf = new HarnessCompactionObservingChatClient("unused");
            var hybridProfile = HarnessCompactionSeamTestFixture.CreateHybridProfile(
                1000, 999, 5, 3, new HarnessConstantSizeContextEstimator(1));
            var compactionClient = new HarnessHybridCompactionChatClient(
                leaf, hybridProfile, binding, accessor, HarnessCompositionTestFixture.SessionId, runCoordinator: null, progressAccessor: null);

            using var cts = new CancellationTokenSource();
            cts.Cancel();
            var messages = new List<ChatMessage> { new(ChatRole.System, "be helpful") };

            await Assert.ThrowsAsync<OperationCanceledException>(
                () => compactionClient.GetResponseAsync(messages, cancellationToken: cts.Token));

            Assert.Equal(0, leaf.CallCount);
        }
    }

    [Fact]
    public async Task GetStreamingResponseAsync_PreCanceledToken_ThrowsOperationCanceled_LeafNeverInvoked()
    {
        using var services = HarnessCompositionTestFixture.CreateServices();
        var accessor = new AgentExecutionContextAccessor();
        var binding = HarnessCompositionTestFixture.CaptureBinding(accessor, out var scope);
        using (scope)
        {
            var leaf = new HarnessCompactionObservingChatClient("unused");
            var hybridProfile = HarnessCompactionSeamTestFixture.CreateHybridProfile(
                1000, 999, 5, 3, new HarnessConstantSizeContextEstimator(1));
            var compactionClient = new HarnessHybridCompactionChatClient(
                leaf, hybridProfile, binding, accessor, HarnessCompositionTestFixture.SessionId, runCoordinator: null, progressAccessor: null);

            using var cts = new CancellationTokenSource();
            cts.Cancel();
            var messages = new List<ChatMessage> { new(ChatRole.System, "be helpful") };

            var enumerator = compactionClient
                .GetStreamingResponseAsync(messages, cancellationToken: cts.Token)
                .GetAsyncEnumerator(cts.Token);

            await Assert.ThrowsAsync<OperationCanceledException>(
                async () => await enumerator.MoveNextAsync());

            Assert.Equal(0, leaf.CallCount);
        }
    }

    [Fact]
    public async Task GetResponseAsync_ReducerObservesCancellation_PropagatesWithoutWrapping_LeafNeverInvoked()
    {
        using var services = HarnessCompositionTestFixture.CreateServices();
        var accessor = new AgentExecutionContextAccessor();
        var binding = HarnessCompositionTestFixture.CaptureBinding(accessor, out var scope);
        using (scope)
        {
            var leaf = new HarnessCompactionObservingChatClient("unused");
            using var cts = new CancellationTokenSource();

            // A small hard limit/trigger margin forces the trigger threshold and drives the bounded
            // reducer loop; the reducer itself observes cancellation and throws exactly as the real
            // upstream abstraction contract requires (cancel first, then re-check its own token), never
            // wrapped or reinterpreted by the bridge or this node.
            var reducer = new HarnessScriptedUpstreamChatReducer(
                (_, ct) =>
                {
                    cts.Cancel();
                    ct.ThrowIfCancellationRequested();
                    return Task.FromResult<IEnumerable<ChatMessage>>([]);
                });
            var hybridProfile = HarnessCompactionSeamTestFixture.CreateHybridProfile(
                2, 1, 5, 1, new HarnessConstantSizeContextEstimator(5), reducer);
            var compactionClient = new HarnessHybridCompactionChatClient(
                leaf, hybridProfile, binding, accessor, HarnessCompositionTestFixture.SessionId, runCoordinator: null, progressAccessor: null);

            var messages = new List<ChatMessage>
            {
                new(ChatRole.System, "be helpful"),
                new(ChatRole.User, "hello"),
            };

            await Assert.ThrowsAsync<OperationCanceledException>(
                () => compactionClient.GetResponseAsync(messages, cancellationToken: cts.Token));

            Assert.Equal(0, leaf.CallCount);
        }
    }

    [Fact]
    public async Task GetResponseAsync_CancellationAfterReducerBeforeDispatch_ThrowsOperationCanceled_LeafNeverInvoked()
    {
        using var services = HarnessCompositionTestFixture.CreateServices();
        var accessor = new AgentExecutionContextAccessor();
        var binding = HarnessCompositionTestFixture.CaptureBinding(accessor, out var scope);
        using (scope)
        {
            var leaf = new HarnessCompactionObservingChatClient("unused");
            using var cts = new CancellationTokenSource();

            // The reducer completes normally (it neither throws nor itself observes cancellation) but
            // cancels the shared token the instant before returning, simulating a cancellation raised
            // by the caller in the narrow window between the reducer finishing and this assembly
            // attempt being dispatched. Assembly must never proceed to dispatch a completed proposal
            // once cancellation has been requested, whether the check that catches it lives inside the
            // assembler's own post-reducer recheck or this node's own explicit second checkpoint.
            var reducer = new HarnessScriptedUpstreamChatReducer(
                (messages, _) =>
                {
                    cts.Cancel();
                    return Task.FromResult(messages);
                });
            var hybridProfile = HarnessCompactionSeamTestFixture.CreateHybridProfile(
                2, 1, 5, 1, new HarnessConstantSizeContextEstimator(5), reducer);
            var compactionClient = new HarnessHybridCompactionChatClient(
                leaf, hybridProfile, binding, accessor, HarnessCompositionTestFixture.SessionId, runCoordinator: null, progressAccessor: null);

            var messages = new List<ChatMessage>
            {
                new(ChatRole.System, "be helpful"),
                new(ChatRole.User, "hello"),
            };

            await Assert.ThrowsAsync<OperationCanceledException>(
                () => compactionClient.GetResponseAsync(messages, cancellationToken: cts.Token));

            Assert.Equal(0, leaf.CallCount);
        }
    }

    [Fact]
    public async Task GetResponseAsync_BindingInvalidatedAfterAssembly_ThrowsInvalidOperation_LeafNeverInvoked()
    {
        using var services = HarnessCompositionTestFixture.CreateServices();
        var accessor = new MutableExecutionContextAccessor();
        using var scope = accessor.BeginScope(
            new AgentExecutionContext(
                "user-1", "orchestration-1", Workspace: new InMemoryWorkspace()));
        var capture = HarnessExecutionBinding.Capture(
            accessor, HarnessCompositionTestFixture.SessionId, requireWorkspace: true);
        var binding = Assert.IsType<HarnessExecutionBinding>(capture.Binding);

        var leaf = new HarnessCompactionObservingChatClient("unused");

        // The reducer completes normally but invalidates the trusted execution context in the narrow
        // window between assembly succeeding and this node's own post-assembly, pre-dispatch trust
        // revalidation. That second revalidation — deliberately distinct from and in addition to the
        // entry-point check — must still surface here rather than allowing a completed assembly to be
        // dispatched to the real provider regardless.
        var reducer = new HarnessScriptedUpstreamChatReducer(
            (messages, _) =>
            {
                accessor.Clear();
                return Task.FromResult(messages);
            });
        var hybridProfile = HarnessCompactionSeamTestFixture.CreateHybridProfile(
            1000, 999, 5, 3, new HarnessConstantSizeContextEstimator(1), reducer);
        var compactionClient = new HarnessHybridCompactionChatClient(
            leaf, hybridProfile, binding, accessor, HarnessCompositionTestFixture.SessionId, runCoordinator: null, progressAccessor: null);

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, "be helpful"),
            new(ChatRole.User, "hello"),
        };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => compactionClient.GetResponseAsync(
                messages, cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal(0, leaf.CallCount);
    }

    [Fact]
    public async Task GetStreamingResponseAsync_BindingInvalidatedAfterAssembly_ThrowsInvalidOperation_LeafNeverInvoked()
    {
        using var services = HarnessCompositionTestFixture.CreateServices();
        var accessor = new MutableExecutionContextAccessor();
        using var scope = accessor.BeginScope(
            new AgentExecutionContext(
                "user-1", "orchestration-1", Workspace: new InMemoryWorkspace()));
        var capture = HarnessExecutionBinding.Capture(
            accessor, HarnessCompositionTestFixture.SessionId, requireWorkspace: true);
        var binding = Assert.IsType<HarnessExecutionBinding>(capture.Binding);

        var leaf = new HarnessCompactionObservingChatClient("unused");
        var reducer = new HarnessScriptedUpstreamChatReducer(
            (messages, _) =>
            {
                accessor.Clear();
                return Task.FromResult(messages);
            });
        var hybridProfile = HarnessCompactionSeamTestFixture.CreateHybridProfile(
            1000, 999, 5, 3, new HarnessConstantSizeContextEstimator(1), reducer);
        var compactionClient = new HarnessHybridCompactionChatClient(
            leaf, hybridProfile, binding, accessor, HarnessCompositionTestFixture.SessionId, runCoordinator: null, progressAccessor: null);

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, "be helpful"),
            new(ChatRole.User, "hello"),
        };

        var enumerator = compactionClient
            .GetStreamingResponseAsync(messages, cancellationToken: TestContext.Current.CancellationToken)
            .GetAsyncEnumerator(TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await enumerator.MoveNextAsync());

        Assert.Equal(0, leaf.CallCount);
    }

    [Fact]
    public async Task RunAsync_CancellationDuringMessageInjectionInducedExtraCall_ThrowsOperationCanceled_SecondRoundNeverDispatched()
    {
        using var services = HarnessCompositionTestFixture.CreateServices();
        var accessor = new MutableExecutionContextAccessor();
        using var scope = accessor.BeginScope(
            new AgentExecutionContext(
                "user-1", "orchestration-1", Workspace: new InMemoryWorkspace()));
        var capture = HarnessExecutionBinding.Capture(
            accessor, HarnessCompositionTestFixture.SessionId, requireWorkspace: true);
        var binding = Assert.IsType<HarnessExecutionBinding>(capture.Binding);

        var function = AIFunctionFactory.Create(() => "unused", "G2Tool");
        IHarnessMessageInjector? injector = null;
        AgentSession? session = null;
        using var cts = new CancellationTokenSource();

        // The first round completes successfully and enqueues an injected message exactly as the
        // baseline message-injection seam proof does, but this time it also cancels the shared token
        // used for the whole agent run — the second, injection-induced provider round must never reach
        // the real provider client once that token is canceled.
        var leaf = new HarnessScriptedChatClient(
            function.Name,
            () =>
            {
                injector!.EnqueueMessagesAsync(
                    session!,
                    [new ChatMessage(ChatRole.User, "injected")],
                    CancellationToken.None).GetAwaiter().GetResult();
                cts.Cancel();
            },
            requestFunctionCall: false);
        var reducer = HarnessScriptedUpstreamChatReducer.Echo();
        var hybridProfile = HarnessCompactionSeamTestFixture.CreateHybridProfile(
            1000, 999, 5, 3, new HarnessConstantSizeContextEstimator(1), reducer);

        // One coherent capability profile carrying both Compaction and the ordinary selected
        // capabilities, and one HarnessProviderComposition.Compose call.
        var profile = HarnessCompactionSeamTestFixture.CreateCompactionEnabledProfile(
            HarnessToolLoopOwner.Harness, HarnessTelemetryOwner.Harness);

        var request = HarnessCompositionTestFixture.CreateRequest(
            leaf,
            services,
            profile,
            HarnessCompositionTestFixture.CreateToolResolution(function),
            binding,
            accessor,
            hybridProfile);
        var composition = new HarnessProviderComposition().Compose(request);
        Assert.Equal(HarnessProviderCompositionStatus.Success, composition.Status);
        var agent = Assert.IsAssignableFrom<AIAgent>(composition.Agent);
        injector = Assert.IsAssignableFrom<IHarnessMessageInjector>(
            agent.GetService<IHarnessMessageInjector>());
        session = await agent.CreateSessionAsync(TestContext.Current.CancellationToken);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => agent.RunAsync("run", session, cancellationToken: cts.Token));

        // Only the first round ever reached the real provider client — the injection-induced second
        // round was never dispatched once the token was canceled.
        Assert.Equal(1, leaf.CallCount);
    }

    [Fact]
    public async Task GetResponseAsync_CancellationDuringFinalizationSnapshotCapture_ThrowsOperationCanceled_LeafNeverInvoked()
    {
        using var services = HarnessCompositionTestFixture.CreateServices();
        var accessor = new AgentExecutionContextAccessor();
        var binding = HarnessCompositionTestFixture.CaptureBinding(accessor, out var scope);
        using (scope)
        {
            var leaf = new HarnessCompactionObservingChatClient("unused");
            using var cts = new CancellationTokenSource();
            var captureCount = 0;

            // A generous hard limit/trigger margin keeps this run on the strictly-below-trigger path,
            // which captures a snapshot exactly twice: once at the start of assembly, and once more as
            // the finalization recapture that guards every success return. Canceling on that second
            // capture — the finalization capture — proves cancellation observed during finalization
            // still surfaces rather than allowing a completed, unverified-for-churn assembly to be
            // dispatched regardless.
            HarnessContextSnapshotIntegration snapshotIntegration = baselineEntries =>
            {
                var provider = new HarnessMutableContextSnapshotProvider(baselineEntries);
                provider.OnCapture = () =>
                {
                    captureCount++;
                    if (captureCount == 2)
                    {
                        cts.Cancel();
                    }
                };
                return provider;
            };

            var hybridProfile = HarnessHybridProfile.Create(
                HarnessCompactionTestFixture.CreatePolicy(
                    1000, 999, 5, 3, new HarnessConstantSizeContextEstimator(1)),
                HarnessScriptedUpstreamChatReducer.Echo(),
                new HarnessScriptedMessageClassifier(),
                snapshotIntegration);
            var compactionClient = new HarnessHybridCompactionChatClient(
                leaf, hybridProfile, binding, accessor, HarnessCompositionTestFixture.SessionId, runCoordinator: null, progressAccessor: null);

            var messages = new List<ChatMessage>
            {
                new(ChatRole.System, "be helpful"),
                new(ChatRole.User, "hello"),
            };

            await Assert.ThrowsAsync<OperationCanceledException>(
                () => compactionClient.GetResponseAsync(messages, cancellationToken: cts.Token));

            Assert.Equal(2, captureCount);
            Assert.Equal(0, leaf.CallCount);
        }
    }
}
