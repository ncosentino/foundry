using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

using NexusLabs.Foundry.MicrosoftAgentFramework.Context;
using NexusLabs.Foundry.MicrosoftAgentFramework.Harness;
using NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Capabilities;
using NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Context;
using NexusLabs.Foundry.MicrosoftAgentFramework.Workspace;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Tests.Harness;

/// <summary>
/// Proves the actual MAF 1.15 seam: a <see cref="HarnessHybridCompactionChatClient"/> installed at the
/// innermost per-provider-call position observes every intermediate FICC tool round and every
/// message-injection-driven extra call — never merely the outer agent call — and never forwards a raw
/// recovered artifact body once a durable reference makes it evictable.
/// </summary>
public sealed class HarnessCompactionSeamTests
{
    [Fact]
    public async Task TwoRoundFicc_ReducerObservedOnEveryProviderRequest()
    {
        var invocationCount = 0;
        var function = AIFunctionFactory.Create(
            () =>
            {
                invocationCount++;
                return "tool-result";
            },
            "G2Tool");
        using var services = HarnessCompositionTestFixture.CreateServices();
        var accessor = new AgentExecutionContextAccessor();
        var binding = HarnessCompositionTestFixture.CaptureBinding(accessor, out var scope);
        using (scope)
        {
            var leaf = new HarnessCompactionObservingChatClient(function.Name);
            var reducer = new HarnessScriptedUpstreamChatReducer((messages, _) => Task.FromResult(messages));
            var hybridProfile = HarnessCompactionSeamTestFixture.CreateHybridProfile(
                1000, 999, 5, 3, new HarnessConstantSizeContextEstimator(1), reducer);

            // One coherent capability profile carrying both Compaction and the ordinary selected
            // capabilities, and one HarnessProviderComposition.Compose call: HarnessProviderComposition
            // invokes the narrow HarnessCompactionComposition internally against this exact profile and
            // chat client, and builds the rest of the pipeline from the chat client it returns.
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

            var result = new HarnessProviderComposition().Compose(request);
            Assert.Equal(HarnessProviderCompositionStatus.Success, result.Status);
            var agent = Assert.IsAssignableFrom<AIAgent>(result.Agent);

            var response = await agent.RunAsync(
                "run", cancellationToken: TestContext.Current.CancellationToken);

            Assert.Equal("tool-result", response.GetText());
            Assert.Equal(1, invocationCount);

            // Two provider rounds (initial call, tool-result call) — the reducer, and therefore the
            // compaction seam, observed both, never merely the outer agent call or only one round.
            Assert.Equal(2, leaf.CallCount);
            Assert.Equal(2, reducer.InvocationCount);
        }
    }

    [Fact]
    public async Task MessageInjection_ExtraProviderCall_CompactionObservesEveryCall()
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
        var reducer = new HarnessScriptedUpstreamChatReducer((messages, _) => Task.FromResult(messages));
        var leaf = new HarnessScriptedChatClient(
            function.Name,
            () =>
            {
                injector!.EnqueueMessagesAsync(
                    session!,
                    [new ChatMessage(ChatRole.User, "injected")],
                    CancellationToken.None).GetAwaiter().GetResult();
            },
            requestFunctionCall: false);
        var hybridProfile = HarnessCompactionSeamTestFixture.CreateHybridProfile(
            1000, 999, 5, 3, new HarnessConstantSizeContextEstimator(1), reducer);
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

        await agent.RunAsync("run", session, cancellationToken: TestContext.Current.CancellationToken);

        // The injected message forced a second provider round beyond the first — the compaction
        // seam, via the reducer it always invokes, observed both.
        Assert.Equal(2, leaf.CallCount);
        Assert.Equal(2, reducer.InvocationCount);
    }

    [Fact]
    public async Task AbsentProfile_PreservesBaseline_NoCompactionInstalled()
    {
        var function = AIFunctionFactory.Create(() => "tool-result", "G2Tool");
        using var services = HarnessCompositionTestFixture.CreateServices();
        var accessor = new AgentExecutionContextAccessor();
        var binding = HarnessCompositionTestFixture.CaptureBinding(accessor, out var scope);
        using (scope)
        {
            var leaf = new HarnessScriptedChatClient(function.Name);

            // No HarnessHybridProfile supplied at all, and the capability profile never requested
            // Compaction: HarnessProviderComposition.Compose must preserve the existing baseline
            // pipeline exactly, with no compaction component installed.
            var profile = HarnessCompositionTestFixture.CreateProfile(
                HarnessToolLoopOwner.Harness, HarnessTelemetryOwner.Harness);

            var request = HarnessCompositionTestFixture.CreateRequest(
                leaf,
                services,
                profile,
                HarnessCompositionTestFixture.CreateToolResolution(function),
                binding,
                accessor,
                hybridProfile: null);
            var result = new HarnessProviderComposition().Compose(request);
            Assert.Equal(HarnessProviderCompositionStatus.Success, result.Status);
            var agent = Assert.IsAssignableFrom<AIAgent>(result.Agent);

            // No compaction component anywhere in the composed pipeline — the existing baseline is
            // preserved exactly, with no behavior change whatsoever.
            Assert.Null(agent.GetService<HarnessHybridCompactionChatClient>());

            var response = await agent.RunAsync(
                "run", cancellationToken: TestContext.Current.CancellationToken);
            Assert.Equal("tool-result", response.GetText());
            Assert.Equal(2, leaf.CallCount);
        }
    }

    [Fact]
    public async Task HardLimitIrreducibleOutcome_BlocksProviderDispatch_LeafNeverInvoked()
    {
        using var services = HarnessCompositionTestFixture.CreateServices();
        var accessor = new AgentExecutionContextAccessor();
        var binding = HarnessCompositionTestFixture.CaptureBinding(accessor, out var scope);
        using (scope)
        {
            var leaf = new HarnessCompactionObservingChatClient("unused");
            var classifier = new HarnessScriptedMessageClassifier(
                classifyOverride: (_, index, _) =>
                    index == 0 ? HarnessContextEntryKind.SystemInstruction : null);
            var hybridProfile = HarnessHybridProfile.Create(
                HarnessCompactionTestFixture.CreatePolicy(
                    2, 1, 5, 1, new HarnessConstantSizeContextEstimator(5)),
                HarnessScriptedUpstreamChatReducer.Echo(),
                classifier,
                baselineEntries => new HarnessMutableContextSnapshotProvider(baselineEntries));
            var compactionClient = new HarnessHybridCompactionChatClient(
                leaf, hybridProfile, binding, accessor, HarnessCompositionTestFixture.SessionId, runCoordinator: null);

            var messages = new List<ChatMessage> { new(ChatRole.System, "be helpful") };

            var exception = await Assert.ThrowsAsync<HarnessCompactionIrreducibleException>(
                () => compactionClient.GetResponseAsync(
                    messages, cancellationToken: TestContext.Current.CancellationToken));

            Assert.Equal(2, exception.HardLimit);
            Assert.Equal(0, leaf.CallCount);
        }
    }

    [Fact]
    public async Task RecoverableBodyWithDurableReference_EvictedBeforeReducer_RawBodyNeverForwarded()
    {
        using var services = HarnessCompositionTestFixture.CreateServices();
        var accessor = new AgentExecutionContextAccessor();
        var binding = HarnessCompositionTestFixture.CaptureBinding(accessor, out var scope);
        using (scope)
        {
            const string rawBody = "SECRET-RAW-RECOVERED-BODY";
            const string artifactContentSeed = "seam-test-artifact-content";
            const string recoveredEntryId = "recovered-entry-id";
            var digest = HarnessArtifactIdentity.ComputeDigest(artifactContentSeed);
            var reference = HarnessCompactionTestFixture.SampleReference(
                artifactContentSeed, DateTimeOffset.UtcNow);
            var segment = HarnessArtifactRecoverableContextSegment.Create(
                reference, rawBody, DateTimeOffset.UtcNow);

            // The incoming request messages carry only the durable artifact reference — never the raw
            // recovered body. The body is added only inside the SnapshotIntegration seam below.
            var messages = new List<ChatMessage>
            {
                new(ChatRole.System, "be helpful"),
                new(ChatRole.Tool, HarnessArtifactIdentity.BuildReferenceId(digest)),
                new(ChatRole.User, "hello"),
            };

            var classifier = new HarnessScriptedMessageClassifier(
                classifyOverride: (_, index, _) =>
                    index == 0 ? HarnessContextEntryKind.SystemInstruction : null);

            var sizesById = new Dictionary<string, int>
            {
                [classifier.ResolveEntryId(messages[0], 0, messages)] = 10,
                [classifier.ResolveEntryId(messages[1], 1, messages)] = 10,
                [classifier.ResolveEntryId(messages[2], 2, messages)] = 10,
                [recoveredEntryId] = 50,
            };

            var leaf = new HarnessCompactionObservingChatClient("unused");
            var hybridProfile = HarnessHybridProfile.Create(
                HarnessCompactionTestFixture.CreatePolicy(
                    80, 20, 1, 3, new HarnessFixedSizeContextEstimator(sizesById)),
                HarnessScriptedUpstreamChatReducer.Echo(),
                classifier,
                baselineEntries => new HarnessMutableContextSnapshotProvider(
                    HarnessContextSnapshotAugmentation.WithRecoverableSegment(
                        baselineEntries, recoveredEntryId, segment)));
            var compactionClient = new HarnessHybridCompactionChatClient(
                leaf, hybridProfile, binding, accessor, HarnessCompositionTestFixture.SessionId, runCoordinator: null);

            await compactionClient.GetResponseAsync(
                messages, cancellationToken: TestContext.Current.CancellationToken);

            var forwarded = Assert.Single(leaf.ObservedCalls);
            Assert.DoesNotContain(forwarded, message => message.Text?.Contains(rawBody) == true);
            Assert.Contains(forwarded, message => message.Text == "be helpful");
            Assert.Contains(forwarded, message => message.Text == "hello");
        }
    }

    [Fact]
    public async Task OuterRecorder_NeverSeesRawBody_WhileInnerProviderReceivesTransientRecoveredBody()
    {
        using var services = HarnessCompositionTestFixture.CreateServices();
        var accessor = new AgentExecutionContextAccessor();
        var binding = HarnessCompositionTestFixture.CaptureBinding(accessor, out var scope);
        using (scope)
        {
            const string rawBody = "SECRET-RAW-RECOVERED-BODY";
            const string artifactContentSeed = "session-flow-artifact-content";
            const string recoveredEntryId = "recovered-entry-id";
            var digest = HarnessArtifactIdentity.ComputeDigest(artifactContentSeed);
            var reference = HarnessCompactionTestFixture.SampleReference(
                artifactContentSeed, DateTimeOffset.UtcNow);
            var segment = HarnessArtifactRecoverableContextSegment.Create(
                reference, rawBody, DateTimeOffset.UtcNow);

            // The incoming request messages — the exact ones an outer per-service history decorator
            // would already have observed and persisted — carry only the durable reference, never the
            // raw body.
            var messages = new List<ChatMessage>
            {
                new(ChatRole.System, "be helpful"),
                new(ChatRole.Tool, HarnessArtifactIdentity.BuildReferenceId(digest)),
                new(ChatRole.User, "hello"),
            };

            var classifier = new HarnessScriptedMessageClassifier(
                classifyOverride: (_, index, _) =>
                    index == 0 ? HarnessContextEntryKind.SystemInstruction : null);

            var leaf = new HarnessCompactionObservingChatClient("unused");

            // A generous policy, well below trigger: the augmented recoverable entry is neither
            // evicted nor reduced, so it is forwarded to the real provider exactly as-is.
            var hybridProfile = HarnessHybridProfile.Create(
                HarnessCompactionTestFixture.CreatePolicy(
                    1000, 1, 5, 3, new HarnessConstantSizeContextEstimator(1)),
                HarnessScriptedUpstreamChatReducer.Echo(),
                classifier,
                baselineEntries => new HarnessMutableContextSnapshotProvider(
                    HarnessContextSnapshotAugmentation.WithRecoverableSegment(
                        baselineEntries, recoveredEntryId, segment)));
            var compactionClient = new HarnessHybridCompactionChatClient(
                leaf, hybridProfile, binding, accessor, HarnessCompositionTestFixture.SessionId, runCoordinator: null);
            var outerRecorder = new HarnessRecordingPassthroughChatClient(compactionClient);

            await outerRecorder.GetResponseAsync(
                messages, cancellationToken: TestContext.Current.CancellationToken);

            // The outer, persistence-facing position (proxy for an outer per-service history decorator)
            // observes only the actual incoming request messages: the reference, never the raw body —
            // because the raw body is never part of the incoming message list at all.
            var recordedCall = Assert.Single(outerRecorder.ObservedCalls);
            Assert.DoesNotContain(recordedCall, message => message.Text == rawBody);
            Assert.Contains(
                recordedCall, message => message.Text == HarnessArtifactIdentity.BuildReferenceId(digest));

            // The real provider, inner to compaction, receives the transient recovered body: added only
            // at the SnapshotIntegration seam, after the outer position already observed the
            // reference-only baseline.
            var forwardedCall = Assert.Single(leaf.ObservedCalls);
            Assert.Contains(forwardedCall, message => message.Text == rawBody);
        }
    }
}
