using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

using NexusLabs.Foundry.MicrosoftAgentFramework.Context;
using NexusLabs.Foundry.MicrosoftAgentFramework.Harness;
using NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Capabilities;
using NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Context;
using NexusLabs.Foundry.MicrosoftAgentFramework.Workspace;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Tests.Harness;

/// <summary>
/// Non-retransmission proof for <see cref="HarnessCompactionRunCoordinator"/>: a stable recovered
/// artifact body a host's <see cref="HarnessContextSnapshotIntegration"/> delegate re-selects on every
/// nested provider call must still reach the real provider at most once per outer agent run, while the
/// durable <see cref="HarnessContextEntryKind.ArtifactReference"/> entry itself is unaffected and every
/// nested call is still observed by the reducer/compaction seam as before. Covers both the composed
/// <see cref="HarnessGuardedAgent"/> path (one coordinator run scope shared across nested FICC/injection
/// calls) and direct/standalone <see cref="HarnessHybridCompactionChatClient"/> use (no leakage across
/// separate calls without a supplied coordinator).
/// </summary>
public sealed class HarnessCompactionRunCoordinatorTests
{
    [Fact]
    public async Task TwoRoundFicc_OneSelectedSegment_DurableReferenceBothRounds_RawBodyExactlyOnce()
    {
        const string rawBody = "SECRET-RAW-RECOVERED-BODY";
        const string artifactContentSeed = "coordinator-two-round-artifact-content";
        const string recoveredEntryId = "recovered-entry-id";
        var digest = HarnessArtifactIdentity.ComputeDigest(artifactContentSeed);
        var referenceText = HarnessArtifactIdentity.BuildReferenceId(digest);
        var reference = HarnessCompactionTestFixture.SampleReference(artifactContentSeed, DateTimeOffset.UtcNow);
        var segment = HarnessArtifactRecoverableContextSegment.Create(reference, rawBody, DateTimeOffset.UtcNow);

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
            var reducer = HarnessScriptedUpstreamChatReducer.Echo();
            var classifier = new HarnessScriptedMessageClassifier();

            // Generous policy, comfortably below the trigger margin: the augmented recoverable entry is
            // neither evicted nor reduced by HarnessContextAssembler on either round (eviction of a
            // recoverable body only ever happens once compaction actually triggers, which would strip the
            // body before it ever reaches the real provider at all). Non-retransmission is what
            // must additionally guarantee the raw body reaches the provider at most once — not eviction.
            var hybridProfile = HarnessHybridProfile.Create(
                HarnessCompactionTestFixture.CreatePolicy(
                    1000, 1, 5, 3, new HarnessConstantSizeContextEstimator(1)),
                reducer,
                classifier,
                baselineEntries => new HarnessMutableContextSnapshotProvider(
                    HarnessContextSnapshotAugmentation.WithRecoverableSegment(
                        baselineEntries, recoveredEntryId, segment)));
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

            // The exact same SnapshotIntegration selection (this composed agent's hybridProfile above)
            // is re-evaluated on every nested provider call — the host has no reason to know a body it
            // already selected was already dispatched — yet non-retransmission requires the raw body
            // reach the real provider at most once across this whole outer run.
            var response = await agent.RunAsync(
                referenceText, cancellationToken: TestContext.Current.CancellationToken);

            Assert.Equal("tool-result", response.GetText());
            Assert.Equal(1, invocationCount);

            // Two provider rounds reached the real provider (initial call, tool-result call) — the
            // compaction seam (HarnessHybridCompactionChatClient.AssembleBoundedMessagesAsync) ran on
            // both, never merely the outer agent call. The generous policy here never actually triggers
            // the pluggable IChatReducer (reducer.InvocationCount stays 0) — that is deliberate: proving
            // non-retransmission requires the raw body survives assembly and still reaches the provider
            // only once, which eviction/reduction would otherwise defeat by stripping it outright.
            Assert.Equal(2, leaf.CallCount);
            Assert.Equal(0, reducer.InvocationCount);

            // The durable reference is an ordinary baseline entry, unaffected by run-scope filtering:
            // it reaches the real provider on both rounds.
            Assert.All(
                leaf.ObservedCalls,
                call => Assert.Contains(call, message => message.Text == referenceText));

            // The raw recovered body reaches the real provider on exactly one of the two rounds — never
            // both, and never zero (it was selected and never evicted).
            Assert.Equal(
                1, leaf.ObservedCalls.Count(call => call.Any(message => message.Text == rawBody)));
        }
    }

    [Fact]
    public async Task MessageInjection_ExtraProviderCall_RawBodyReachesAtMostOneCall()
    {
        const string rawBody = "SECRET-RAW-RECOVERED-BODY";
        const string artifactContentSeed = "coordinator-message-injection-artifact-content";
        const string recoveredEntryId = "recovered-entry-id";
        var digest = HarnessArtifactIdentity.ComputeDigest(artifactContentSeed);
        var referenceText = HarnessArtifactIdentity.BuildReferenceId(digest);
        var reference = HarnessCompactionTestFixture.SampleReference(artifactContentSeed, DateTimeOffset.UtcNow);
        var segment = HarnessArtifactRecoverableContextSegment.Create(reference, rawBody, DateTimeOffset.UtcNow);

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
        var leaf = new InjectingObservingChatClient(
            () =>
            {
                injector!.EnqueueMessagesAsync(
                    session!,
                    [new ChatMessage(ChatRole.User, "injected")],
                    CancellationToken.None).GetAwaiter().GetResult();
            });
        var classifier = new HarnessScriptedMessageClassifier();

        // Conditional augmentation: only add the recoverable segment when this exact call's own baseline
        // actually carries the matching durable reference. MessageInjectingChatClient's injected-message
        // round is not guaranteed to re-present the full original conversation the first round saw, so a
        // realistic host-authored SnapshotIntegration delegate — which only ever selects a body for a
        // reference it can actually see in the current call's context — must behave the same way here.
        var hybridProfile = HarnessHybridProfile.Create(
            HarnessCompactionTestFixture.CreatePolicy(
                1000, 1, 5, 3, new HarnessConstantSizeContextEstimator(1)),
            HarnessScriptedUpstreamChatReducer.Echo(),
            classifier,
            baselineEntries =>
            {
                var hasMatchingReference = baselineEntries.Any(
                    entry => entry.Kind == HarnessContextEntryKind.ArtifactReference
                        && string.Equals(entry.ArtifactReferenceDigest, digest, StringComparison.Ordinal));
                return new HarnessMutableContextSnapshotProvider(
                    hasMatchingReference
                        ? HarnessContextSnapshotAugmentation.WithRecoverableSegment(
                            baselineEntries, recoveredEntryId, segment)
                        : baselineEntries);
            });
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

        await agent.RunAsync(
            referenceText, session, cancellationToken: TestContext.Current.CancellationToken);

        // The injected message forced a second provider round beyond the first.
        Assert.Equal(2, leaf.CallCount);

        // The raw body reaches at most one of the two calls — never both — regardless of whether the
        // injected round happens to re-present the durable reference to this call's own baseline.
        var rawBodyCallCount = leaf.ObservedCalls.Count(call => call.Any(message => message.Text == rawBody));
        Assert.True(
            rawBodyCallCount <= 1,
            $"Expected the raw recovered body to reach at most one provider call, but it reached {rawBodyCallCount}.");

        // The first round's own request carries the raw body: the recoverable segment is selected there
        // because the original referenceText input is unambiguously present in that call's baseline.
        Assert.Contains(leaf.ObservedCalls[0], message => message.Text == rawBody);
    }

    [Fact]
    public async Task TwoDifferentReferences_EachDeliveredOncePerRun()
    {
        const string rawBody1 = "SECRET-RAW-RECOVERED-BODY-ONE";
        const string rawBody2 = "SECRET-RAW-RECOVERED-BODY-TWO";
        const string artifactContentSeed1 = "coordinator-two-refs-artifact-content-one";
        const string artifactContentSeed2 = "coordinator-two-refs-artifact-content-two";
        const string recoveredEntryId1 = "recovered-entry-id-one";
        const string recoveredEntryId2 = "recovered-entry-id-two";
        var digest1 = HarnessArtifactIdentity.ComputeDigest(artifactContentSeed1);
        var digest2 = HarnessArtifactIdentity.ComputeDigest(artifactContentSeed2);
        var reference1 = HarnessCompactionTestFixture.SampleReference(artifactContentSeed1, DateTimeOffset.UtcNow);
        var reference2 = HarnessCompactionTestFixture.SampleReference(artifactContentSeed2, DateTimeOffset.UtcNow);
        var segment1 = HarnessArtifactRecoverableContextSegment.Create(reference1, rawBody1, DateTimeOffset.UtcNow);
        var segment2 = HarnessArtifactRecoverableContextSegment.Create(reference2, rawBody2, DateTimeOffset.UtcNow);

        using var services = HarnessCompositionTestFixture.CreateServices();
        var accessor = new AgentExecutionContextAccessor();
        var binding = HarnessCompositionTestFixture.CaptureBinding(accessor, out var scope);
        using (scope)
        {
            var messages = new List<ChatMessage>
            {
                new(ChatRole.System, "be helpful"),
                new(ChatRole.Assistant, [new FunctionCallContent("call-1", "G4Tool", new Dictionary<string, object?>())]),
                new(ChatRole.Tool, [new FunctionResultContent("call-1", HarnessArtifactIdentity.BuildReferenceId(digest1))]),
                new(ChatRole.Assistant, [new FunctionCallContent("call-2", "G4Tool", new Dictionary<string, object?>())]),
                new(ChatRole.Tool, [new FunctionResultContent("call-2", HarnessArtifactIdentity.BuildReferenceId(digest2))]),
                new(ChatRole.User, "hello"),
            };
            var classifier = new HarnessScriptedMessageClassifier();
            var hybridProfile = HarnessHybridProfile.Create(
                HarnessCompactionTestFixture.CreatePolicy(
                    1000, 1, 5, 3, new HarnessConstantSizeContextEstimator(1)),
                HarnessScriptedUpstreamChatReducer.Echo(),
                classifier,
                baselineEntries => new HarnessMutableContextSnapshotProvider(
                    HarnessContextSnapshotAugmentation.WithRecoverableSegment(
                        HarnessContextSnapshotAugmentation.WithRecoverableSegment(
                            baselineEntries, recoveredEntryId1, segment1),
                        recoveredEntryId2,
                        segment2)));
            var leaf = new HarnessCompactionObservingChatClient("unused");
            var coordinator = new HarnessCompactionRunCoordinator();

            // Simulates the one coordinator run scope HarnessGuardedAgent.RunCoreAsync begins around an
            // entire outer run: every nested HarnessHybridCompactionChatClient call below joins this same
            // scope via EnsureRunScope, exactly as three nested FICC/injection rounds within one outer
            // agent run would.
            using var runScope = coordinator.BeginRun();
            var compactionClient = new HarnessHybridCompactionChatClient(
                leaf, hybridProfile, binding, accessor, HarnessCompositionTestFixture.SessionId, coordinator, progressAccessor: null);

            await compactionClient.GetResponseAsync(
                messages, cancellationToken: TestContext.Current.CancellationToken);
            await compactionClient.GetResponseAsync(
                messages, cancellationToken: TestContext.Current.CancellationToken);
            await compactionClient.GetResponseAsync(
                messages, cancellationToken: TestContext.Current.CancellationToken);

            Assert.Equal(3, leaf.CallCount);

            // Reference one's raw body reaches the real provider on exactly the first call.
            Assert.Contains(leaf.ObservedCalls[0], message => message.Text == rawBody1);
            Assert.DoesNotContain(leaf.ObservedCalls[1], message => message.Text == rawBody1);
            Assert.DoesNotContain(leaf.ObservedCalls[2], message => message.Text == rawBody1);

            // Reference two's raw body independently reaches the real provider on exactly the first
            // call too — each reference's delivered-once tracking is per-digest, not a single flag for
            // the whole run.
            Assert.Contains(leaf.ObservedCalls[0], message => message.Text == rawBody2);
            Assert.DoesNotContain(leaf.ObservedCalls[1], message => message.Text == rawBody2);
            Assert.DoesNotContain(leaf.ObservedCalls[2], message => message.Text == rawBody2);
        }
    }

    [Fact]
    public async Task SecondOuterRun_GetsFreshScope_RedeliversSameRecoveredBody()
    {
        const string rawBody = "SECRET-RAW-RECOVERED-BODY";
        const string artifactContentSeed = "coordinator-second-run-artifact-content";
        const string recoveredEntryId = "recovered-entry-id";
        var digest = HarnessArtifactIdentity.ComputeDigest(artifactContentSeed);
        var referenceText = HarnessArtifactIdentity.BuildReferenceId(digest);
        var reference = HarnessCompactionTestFixture.SampleReference(artifactContentSeed, DateTimeOffset.UtcNow);
        var segment = HarnessArtifactRecoverableContextSegment.Create(reference, rawBody, DateTimeOffset.UtcNow);

        using var services = HarnessCompositionTestFixture.CreateServices();
        var accessor = new AgentExecutionContextAccessor();
        var binding = HarnessCompositionTestFixture.CaptureBinding(accessor, out var scope);
        using (scope)
        {
            var function = AIFunctionFactory.Create(() => "unused", "G2Tool");
            var leaf = new InjectingObservingChatClient(afterFirstResponse: static () => { });
            var classifier = new HarnessScriptedMessageClassifier();
            var hybridProfile = HarnessHybridProfile.Create(
                HarnessCompactionTestFixture.CreatePolicy(
                    1000, 1, 5, 3, new HarnessConstantSizeContextEstimator(1)),
                HarnessScriptedUpstreamChatReducer.Echo(),
                classifier,
                baselineEntries => new HarnessMutableContextSnapshotProvider(
                    HarnessContextSnapshotAugmentation.WithRecoverableSegment(
                        baselineEntries, recoveredEntryId, segment)));
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

            // Two entirely separate outer runs on the same composed agent, neither sharing a session:
            // HarnessGuardedAgent.RunCoreAsync begins a brand-new coordinator run scope for each one.
            await agent.RunAsync(
                referenceText, cancellationToken: TestContext.Current.CancellationToken);
            await agent.RunAsync(
                referenceText, cancellationToken: TestContext.Current.CancellationToken);

            Assert.Equal(2, leaf.CallCount);

            // A fresh run scope means the second outer run's explicit rehydration selection is allowed
            // to deliver the exact same body again — the delivered set from the first run never leaks
            // into the second.
            Assert.Contains(leaf.ObservedCalls[0], message => message.Text == rawBody);
            Assert.Contains(leaf.ObservedCalls[1], message => message.Text == rawBody);
        }
    }

    [Fact]
    public async Task DirectCompactionClient_NoCoordinatorSupplied_DoesNotLeakDeliveredStateAcrossSeparateCalls()
    {
        const string rawBody = "SECRET-RAW-RECOVERED-BODY";
        const string artifactContentSeed = "coordinator-no-leak-artifact-content";
        const string recoveredEntryId = "recovered-entry-id";
        var digest = HarnessArtifactIdentity.ComputeDigest(artifactContentSeed);
        var reference = HarnessCompactionTestFixture.SampleReference(artifactContentSeed, DateTimeOffset.UtcNow);
        var segment = HarnessArtifactRecoverableContextSegment.Create(reference, rawBody, DateTimeOffset.UtcNow);

        using var services = HarnessCompositionTestFixture.CreateServices();
        var accessor = new AgentExecutionContextAccessor();
        var binding = HarnessCompositionTestFixture.CaptureBinding(accessor, out var scope);
        using (scope)
        {
            var messages = new List<ChatMessage>
            {
                new(ChatRole.System, "be helpful"),
                new(ChatRole.Assistant, [new FunctionCallContent("call-1", "G4Tool", new Dictionary<string, object?>())]),
                new(ChatRole.Tool, [new FunctionResultContent("call-1", HarnessArtifactIdentity.BuildReferenceId(digest))]),
                new(ChatRole.User, "hello"),
            };
            var classifier = new HarnessScriptedMessageClassifier();
            var hybridProfile = HarnessHybridProfile.Create(
                HarnessCompactionTestFixture.CreatePolicy(
                    1000, 1, 5, 3, new HarnessConstantSizeContextEstimator(1)),
                HarnessScriptedUpstreamChatReducer.Echo(),
                classifier,
                baselineEntries => new HarnessMutableContextSnapshotProvider(
                    HarnessContextSnapshotAugmentation.WithRecoverableSegment(
                        baselineEntries, recoveredEntryId, segment)));
            var leaf = new HarnessCompactionObservingChatClient("unused");

            // No coordinator supplied at all — exactly how every seam/cancellation unit test in this
            // codebase constructs this node directly, outside any HarnessGuardedAgent.
            var compactionClient = new HarnessHybridCompactionChatClient(
                leaf, hybridProfile, binding, accessor, HarnessCompositionTestFixture.SessionId, runCoordinator: null, progressAccessor: null);

            await compactionClient.GetResponseAsync(
                messages, cancellationToken: TestContext.Current.CancellationToken);
            await compactionClient.GetResponseAsync(
                messages, cancellationToken: TestContext.Current.CancellationToken);

            Assert.Equal(2, leaf.CallCount);

            // Each call gets its own call-local run scope (HarnessCompactionRunCoordinator.EnsureRunScope,
            // via this node's private fallback coordinator) that is disposed before the call returns, so
            // the raw body is forwarded on both separate calls — never suppressed by the other call.
            Assert.Contains(leaf.ObservedCalls[0], message => message.Text == rawBody);
            Assert.Contains(leaf.ObservedCalls[1], message => message.Text == rawBody);
        }
    }

    [Fact]
    public async Task StreamingNestedFlow_TwoDirectCallsWithinOneRunScope_RawBodyOneShot()
    {
        const string rawBody = "SECRET-RAW-RECOVERED-BODY";
        const string artifactContentSeed = "coordinator-streaming-artifact-content";
        const string recoveredEntryId = "recovered-entry-id";
        var digest = HarnessArtifactIdentity.ComputeDigest(artifactContentSeed);
        var reference = HarnessCompactionTestFixture.SampleReference(artifactContentSeed, DateTimeOffset.UtcNow);
        var segment = HarnessArtifactRecoverableContextSegment.Create(reference, rawBody, DateTimeOffset.UtcNow);

        using var services = HarnessCompositionTestFixture.CreateServices();
        var accessor = new AgentExecutionContextAccessor();
        var binding = HarnessCompositionTestFixture.CaptureBinding(accessor, out var scope);
        using (scope)
        {
            var messages = new List<ChatMessage>
            {
                new(ChatRole.System, "be helpful"),
                new(ChatRole.Assistant, [new FunctionCallContent("call-1", "G4Tool", new Dictionary<string, object?>())]),
                new(ChatRole.Tool, [new FunctionResultContent("call-1", HarnessArtifactIdentity.BuildReferenceId(digest))]),
                new(ChatRole.User, "hello"),
            };
            var classifier = new HarnessScriptedMessageClassifier();
            var hybridProfile = HarnessHybridProfile.Create(
                HarnessCompactionTestFixture.CreatePolicy(
                    1000, 1, 5, 3, new HarnessConstantSizeContextEstimator(1)),
                HarnessScriptedUpstreamChatReducer.Echo(),
                classifier,
                baselineEntries => new HarnessMutableContextSnapshotProvider(
                    HarnessContextSnapshotAugmentation.WithRecoverableSegment(
                        baselineEntries, recoveredEntryId, segment)));
            var leaf = new StreamingObservingChatClient();
            var coordinator = new HarnessCompactionRunCoordinator();

            using var runScope = coordinator.BeginRun();
            var compactionClient = new HarnessHybridCompactionChatClient(
                leaf, hybridProfile, binding, accessor, HarnessCompositionTestFixture.SessionId, coordinator, progressAccessor: null);

            await foreach (var _ in compactionClient.GetStreamingResponseAsync(
                messages, cancellationToken: TestContext.Current.CancellationToken))
            {
            }

            await foreach (var _ in compactionClient.GetStreamingResponseAsync(
                messages, cancellationToken: TestContext.Current.CancellationToken))
            {
            }

            Assert.Equal(2, leaf.CallCount);
            Assert.Equal(
                1, leaf.ObservedCalls.Count(call => call.Any(message => message.Text == rawBody)));
        }
    }

    [Fact]
    public async Task CoordinatorState_AbsentFromSerializedSessionHistoryAndWorkspace()
    {
        const string rawBody = "SECRET-RAW-RECOVERED-BODY";
        const string artifactContentSeed = "coordinator-serialization-artifact-content";
        const string recoveredEntryId = "recovered-entry-id";
        var digest = HarnessArtifactIdentity.ComputeDigest(artifactContentSeed);
        var referenceText = HarnessArtifactIdentity.BuildReferenceId(digest);
        var reference = HarnessCompactionTestFixture.SampleReference(artifactContentSeed, DateTimeOffset.UtcNow);
        var segment = HarnessArtifactRecoverableContextSegment.Create(reference, rawBody, DateTimeOffset.UtcNow);

        var function = AIFunctionFactory.Create(() => "tool-result", "G2Tool");
        using var services = HarnessCompositionTestFixture.CreateServices();
        var accessor = new AgentExecutionContextAccessor();
        var binding = HarnessCompositionTestFixture.CaptureBinding(accessor, out var scope);
        using (scope)
        {
            var historyProvider = new InMemoryChatHistoryProvider(new InMemoryChatHistoryProviderOptions());
            var leaf = new HarnessCompactionObservingChatClient(function.Name);
            var classifier = new HarnessScriptedMessageClassifier();
            var hybridProfile = HarnessHybridProfile.Create(
                HarnessCompactionTestFixture.CreatePolicy(
                    1000, 1, 5, 3, new HarnessConstantSizeContextEstimator(1)),
                HarnessScriptedUpstreamChatReducer.Echo(),
                classifier,
                baselineEntries => new HarnessMutableContextSnapshotProvider(
                    HarnessContextSnapshotAugmentation.WithRecoverableSegment(
                        baselineEntries, recoveredEntryId, segment)));
            var profile = HarnessCompactionSeamTestFixture.CreateCompactionEnabledHistoryProfile(
                HarnessToolLoopOwner.Harness,
                HarnessTelemetryOwner.Harness,
                HarnessHistoryPersistenceMode.DurableProvider);

            var request = HarnessCompositionTestFixture.CreateRequest(
                leaf,
                services,
                profile,
                HarnessCompositionTestFixture.CreateToolResolution(function),
                binding,
                accessor,
                metrics: null,
                historyProvider: HarnessCompositionTestFixture.CreateHistoryProviderPlugin(
                    HarnessHistoryPersistenceMode.DurableProvider,
                    historyProvider),
                planningProviders: null,
                approvalPlugin: null,
                skillsPlugin: null,
                progressAccessor: null,
                webSearchPlugin: null,
                offloadPlugin: null,
                hybridProfile);

            var result = new HarnessProviderComposition().Compose(request);
            Assert.Equal(HarnessProviderCompositionStatus.Success, result.Status);
            var agent = Assert.IsAssignableFrom<AIAgent>(result.Agent);
            var session = await agent.CreateSessionAsync(TestContext.Current.CancellationToken);

            await agent.RunAsync(
                referenceText, session, cancellationToken: TestContext.Current.CancellationToken);

            // Non-vacuous precondition: the raw body actually reached the real provider at least once —
            // this test would otherwise trivially pass if the body were simply never dispatched at all.
            Assert.Contains(leaf.ObservedCalls, call => call.Any(message => message.Text == rawBody));

            var serialized = await agent.SerializeSessionAsync(
                session, cancellationToken: TestContext.Current.CancellationToken);
            var rawJson = serialized.GetRawText();

            // The coordinator's run/delivered-digest state is transient in-process call-graph memory
            // only — it is never written into session state at all, so none of its vocabulary (nor the
            // raw body it gates) ever appears in the serialized session.
            Assert.DoesNotContain(rawBody, rawJson, StringComparison.Ordinal);
            Assert.DoesNotContain("Coordinator", rawJson, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("RunCoordinator", rawJson, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Delivered", rawJson, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("AsyncLocal", rawJson, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("workspace", rawJson, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(referenceText, rawJson, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task ConcurrentOuterRuns_OnSameComposedAgent_HaveIsolatedDeliveredSets()
    {
        const string rawBody = "SECRET-RAW-RECOVERED-BODY";
        const string artifactContentSeed = "coordinator-concurrency-artifact-content";
        const string recoveredEntryId = "recovered-entry-id";
        var digest = HarnessArtifactIdentity.ComputeDigest(artifactContentSeed);
        var referenceText = HarnessArtifactIdentity.BuildReferenceId(digest);
        var reference = HarnessCompactionTestFixture.SampleReference(artifactContentSeed, DateTimeOffset.UtcNow);
        var segment = HarnessArtifactRecoverableContextSegment.Create(reference, rawBody, DateTimeOffset.UtcNow);

        using var services = HarnessCompositionTestFixture.CreateServices();
        var accessor = new AgentExecutionContextAccessor();
        var workspace = new InMemoryWorkspace();
        var context = new AgentExecutionContext("user-1", "orchestration-1", Workspace: workspace);
        HarnessExecutionBinding binding;
        using (accessor.BeginScope(context))
        {
            var captureResult = HarnessExecutionBinding.Capture(
                accessor, HarnessCompositionTestFixture.SessionId, requireWorkspace: true);
            binding = Assert.IsType<HarnessExecutionBinding>(captureResult.Binding);
        }

        var function = AIFunctionFactory.Create(() => "unused", "G2Tool");
        var leaf = new BarrierObservingChatClient(expectedConcurrentCalls: 2);

        // A classifier deliberately free of any shared mutable state (unlike
        // HarnessScriptedMessageClassifier's ResolvedIndices list), since this profile — and therefore
        // this one classifier instance — is shared by both concurrent outer runs below.
        var classifier = new NoOverrideMessageClassifier();
        var hybridProfile = HarnessHybridProfile.Create(
            HarnessCompactionTestFixture.CreatePolicy(
                1000, 1, 5, 3, new HarnessConstantSizeContextEstimator(1)),
            HarnessScriptedUpstreamChatReducer.Echo(),
            classifier,
            baselineEntries => new HarnessMutableContextSnapshotProvider(
                HarnessContextSnapshotAugmentation.WithRecoverableSegment(
                    baselineEntries, recoveredEntryId, segment)));
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

        AIAgent agent;
        using (accessor.BeginScope(context))
        {
            // Compose() itself revalidates the binding against the currently active execution context, so
            // this call — like every other request-capture pairing in this file — must happen while a
            // scope built from the exact same context/workspace reference the binding above was captured
            // against is active. The scope closes again immediately after; each concurrent run below
            // opens its own.
            var composition = new HarnessProviderComposition().Compose(request);
            Assert.Equal(HarnessProviderCompositionStatus.Success, composition.Status);
            agent = Assert.IsAssignableFrom<AIAgent>(composition.Agent);
        }

        async Task RunOnceAsync()
        {
            // Each concurrent flow independently establishes its own AsyncLocal-scoped execution
            // context (reusing the same context/workspace reference the binding above was captured
            // against) before invoking the shared composed agent — exactly how two independent
            // concurrent callers of the same composed agent would each bring their own ambient context.
            using var runScope = accessor.BeginScope(context);
            await agent.RunAsync(
                referenceText, cancellationToken: TestContext.Current.CancellationToken);
        }

        // Two simultaneous outer runs on the very same composed agent (same HarnessGuardedAgent, same
        // HarnessHybridCompactionChatClient, same HarnessCompactionRunCoordinator instance): if the
        // coordinator's AsyncLocal state were shared incorrectly across these two independent runs, the
        // second run to reach HarnessCompactionRunCoordinator.Commit would incorrectly find the digest
        // already delivered and the raw body would reach the real provider only once in total,
        // instead of once per run.
        await Task.WhenAll(RunOnceAsync(), RunOnceAsync());

        Assert.Equal(2, leaf.CallCount);
        Assert.Equal(2, leaf.ObservedCalls.Count(call => call.Any(message => message.Text == rawBody)));
    }

    [Fact]
    public async Task SameOuterRun_ConcurrentProviderCalls_ExactlyOneForwardsRawBody()
    {
        const string rawBody = "SECRET-RAW-RECOVERED-BODY";
        const string artifactContentSeed = "coordinator-same-run-concurrent-artifact-content";
        const string recoveredEntryId = "recovered-entry-id";
        var digest = HarnessArtifactIdentity.ComputeDigest(artifactContentSeed);
        var reference = HarnessCompactionTestFixture.SampleReference(artifactContentSeed, DateTimeOffset.UtcNow);
        var segment = HarnessArtifactRecoverableContextSegment.Create(reference, rawBody, DateTimeOffset.UtcNow);

        using var services = HarnessCompositionTestFixture.CreateServices();
        var accessor = new AgentExecutionContextAccessor();
        var binding = HarnessCompositionTestFixture.CaptureBinding(accessor, out var scope);
        using (scope)
        {
            var messages = new List<ChatMessage>
            {
                new(ChatRole.System, "be helpful"),
                new(ChatRole.Assistant, [new FunctionCallContent("call-1", "G4Tool", new Dictionary<string, object?>())]),
                new(ChatRole.Tool, [new FunctionResultContent("call-1", HarnessArtifactIdentity.BuildReferenceId(digest))]),
                new(ChatRole.User, "hello"),
            };

            // A classifier deliberately free of any shared mutable state, since it is invoked
            // concurrently by two provider calls racing within the very same run scope below.
            var classifier = new NoOverrideMessageClassifier();
            var hybridProfile = HarnessHybridProfile.Create(
                HarnessCompactionTestFixture.CreatePolicy(
                    1000, 1, 5, 3, new HarnessConstantSizeContextEstimator(1)),
                HarnessScriptedUpstreamChatReducer.Echo(),
                classifier,
                baselineEntries => new HarnessMutableContextSnapshotProvider(
                    HarnessContextSnapshotAugmentation.WithRecoverableSegment(
                        baselineEntries, recoveredEntryId, segment)));

            // Forces genuinely overlapping execution: both provider calls below must actually arrive
            // concurrently before either is allowed to complete, so this test cannot pass merely
            // because the two calls happened to run sequentially.
            var leaf = new BarrierObservingChatClient(expectedConcurrentCalls: 2);
            var coordinator = new HarnessCompactionRunCoordinator();

            // One shared outer run scope, exactly as HarnessGuardedAgent.RunCoreAsync begins around an
            // entire outer run: two nested provider calls below race concurrently within it, exactly as
            // two genuinely overlapping FICC tool rounds or message-injection-driven calls could.
            using var runScope = coordinator.BeginRun();
            var compactionClientA = new HarnessHybridCompactionChatClient(
                leaf, hybridProfile, binding, accessor, HarnessCompositionTestFixture.SessionId, coordinator, progressAccessor: null);
            var compactionClientB = new HarnessHybridCompactionChatClient(
                leaf, hybridProfile, binding, accessor, HarnessCompositionTestFixture.SessionId, coordinator, progressAccessor: null);

            var callA = compactionClientA.GetResponseAsync(
                messages, cancellationToken: TestContext.Current.CancellationToken);
            var callB = compactionClientB.GetResponseAsync(
                messages, cancellationToken: TestContext.Current.CancellationToken);

            await Task.WhenAll(callA, callB);

            Assert.Equal(2, leaf.CallCount);

            // Both calls genuinely overlapped inside the real provider (the barrier only releases once
            // both arrivals are observed), yet exactly one of them ever forwarded the raw recovered
            // body: the atomic reservation/lease protocol — not incidental sequencing — is what
            // prevents the other concurrent call from also forwarding it.
            var callsWithRawBody = leaf.ObservedCalls.Count(call => call.Any(message => message.Text == rawBody));
            Assert.Equal(1, callsWithRawBody);
        }
    }

    /// <summary>
    /// Reproduces, deterministically and without depending on any real thread scheduling, the exact
    /// concurrency gap that <see cref="HarnessCompactionRunCoordinator.GetRevision"/> and
    /// <see cref="HarnessDeliveredSegmentFilteringSnapshotProvider"/>'s own effective-version tracking
    /// close: lease B's own capture filters the recoverable body out because lease A reserved it first;
    /// A's own assembly attempt later discovers the body must be pressure-evicted (reserved during
    /// assembly, then stripped from the final entries before dispatch) and its caller closes the lease
    /// with an empty delivered set — Complete's atomic contract releases the reservation without ever
    /// promoting it. Before this fix, the inner snapshot provider's own version never changed across any
    /// of this (no new message was injected — only reservation/delivery state changed), so B's own
    /// filtered snapshot would report the exact same version both before and after A released, B's
    /// assembler would never restart to notice the digest became available again, and the raw body would
    /// reach neither call: A never forwarded it and B never saw it available again (zero delivery). With
    /// the fix, B's next capture (standing in for its finalization recheck) observes the digest's
    /// coordinator revision changed and reports a new effective version even though the inner snapshot's
    /// version is unchanged, so B is the one — and only one — call that ultimately forwards the raw body.
    /// </summary>
    [Fact]
    public void SameOuterRun_LoserFiltersThenWinnerPressureEvictsAndReleases_LoserRestartsAndDeliversExactlyOnce()
    {
        const string rawBody = "SECRET-RAW-RECOVERED-BODY";
        const string artifactContentSeed = "coordinator-revision-restart-artifact-content";
        const string recoveredEntryId = "recovered-entry-id";
        var digest = HarnessArtifactIdentity.ComputeDigest(artifactContentSeed);
        var reference = HarnessCompactionTestFixture.SampleReference(artifactContentSeed, DateTimeOffset.UtcNow);
        var segment = HarnessArtifactRecoverableContextSegment.Create(reference, rawBody, DateTimeOffset.UtcNow);

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, "be helpful"),
            new(ChatRole.Assistant, [new FunctionCallContent("call-1", "G4Tool", new Dictionary<string, object?>())]),
            new(ChatRole.Tool, [new FunctionResultContent("call-1", HarnessArtifactIdentity.BuildReferenceId(digest))]),
            new(ChatRole.User, "hello"),
        };
        var classifier = new HarnessScriptedMessageClassifier();
        var baselineEntries = HarnessMafMessageContextAdapter.Adapt(messages, classifier);
        var entriesWithSegment = HarnessContextSnapshotAugmentation.WithRecoverableSegment(
            baselineEntries, recoveredEntryId, segment);

        var coordinator = new HarnessCompactionRunCoordinator();
        var leaseA = Guid.NewGuid();
        var leaseB = Guid.NewGuid();

        using var runScope = coordinator.BeginRun();

        // Each call owns its own snapshot provider instance (as HarnessHybridCompactionChatClient's own
        // per-call snapshotProvider does), both offering the identical entries — reservation/delivery
        // filtering is the only thing that can ever differ between what each call's own capture reports.
        var providerA = new HarnessMutableContextSnapshotProvider(entriesWithSegment);
        var providerB = new HarnessMutableContextSnapshotProvider(entriesWithSegment);
        var filteringA = new HarnessDeliveredSegmentFilteringSnapshotProvider(providerA, coordinator, leaseA);
        var filteringB = new HarnessDeliveredSegmentFilteringSnapshotProvider(providerB, coordinator, leaseB);

        // Call A captures first within this shared outer run scope and reserves the digest — its own
        // snapshot keeps the raw recovered body.
        var snapshotA1 = filteringA.CaptureSnapshot();
        Assert.Contains(
            snapshotA1.Entries, e => e.Kind == HarnessContextEntryKind.RecoverableContextSegment);

        // Call B races within the very same run scope: the digest is already reserved by A, so B's own
        // first capture must filter the recoverable body back out of what it would otherwise forward.
        var snapshotB1 = filteringB.CaptureSnapshot();
        Assert.DoesNotContain(
            snapshotB1.Entries, e => e.Kind == HarnessContextEntryKind.RecoverableContextSegment);

        // A's own assembly attempt discovers the body must be pressure-evicted before dispatch — its
        // real provider call proceeds without the raw body, exactly like
        // HarnessHybridCompactionChatClient.AssembleBoundedMessagesAsync computing ForwardedDigests from
        // FinalEntries alone. Its caller then closes the lease with an empty delivered set: Complete's
        // atomic contract releases the reservation without ever promoting it to Delivered.
        coordinator.Complete(leaseA, []);

        // B's own assembler now performs its finalization recapture — the same recheck that guards every
        // success path in HarnessContextAssembler — sometime after A already released.
        var snapshotB2 = filteringB.CaptureSnapshot();

        // The fix: B's second capture reports a different effective version than its first, even though
        // neither inner snapshot provider's own version ever changed — only the coordinator's tracked
        // revision for this digest did, when Complete released it.
        Assert.NotEqual(snapshotB1.Version, snapshotB2.Version);
        var recoveredEntry = Assert.Single(
            snapshotB2.Entries, e => e.Kind == HarnessContextEntryKind.RecoverableContextSegment);
        Assert.Equal(rawBody, recoveredEntry.Message.Text);

        // B's assembler, having observed the version change, restarts and re-evaluates from this newest
        // snapshot; one more capture with nothing further racing in behind it confirms the result is
        // stable — exactly the "own repeated capture" case that must never spuriously restart again.
        var snapshotB3 = filteringB.CaptureSnapshot();
        Assert.Equal(snapshotB2.Version, snapshotB3.Version);

        // B's caller commits: the digest is the one, and only, forwarded body for the entire run.
        coordinator.Complete(leaseB, [digest]);

        // No later call — e.g. a third overlapping provider call — can ever reserve, and therefore never
        // redeliver, the exact same digest again: it is now permanently Delivered for this run. Combined
        // with the assertions above (B's second and third captures are the only ones ever observed
        // carrying the raw body, and A's own capture never actually dispatched it), this proves both
        // no-duplicate and no-zero-delivery for this run.
        var leaseC = Guid.NewGuid();
        Assert.False(
            coordinator.TryReserve(digest, leaseC),
            "The digest was already delivered by B; a later lease must never be able to reserve — and " +
            "therefore never redeliver — the exact same body again within the same run.");
    }

    [Fact]
    public async Task ProviderCallFails_ReservationReleased_RetryWithinSameRunRedeliversRawBody()
    {
        const string rawBody = "SECRET-RAW-RECOVERED-BODY";
        const string artifactContentSeed = "coordinator-fail-then-retry-artifact-content";
        const string recoveredEntryId = "recovered-entry-id";
        var digest = HarnessArtifactIdentity.ComputeDigest(artifactContentSeed);
        var reference = HarnessCompactionTestFixture.SampleReference(artifactContentSeed, DateTimeOffset.UtcNow);
        var segment = HarnessArtifactRecoverableContextSegment.Create(reference, rawBody, DateTimeOffset.UtcNow);

        using var services = HarnessCompositionTestFixture.CreateServices();
        var accessor = new AgentExecutionContextAccessor();
        var binding = HarnessCompositionTestFixture.CaptureBinding(accessor, out var scope);
        using (scope)
        {
            var messages = new List<ChatMessage>
            {
                new(ChatRole.System, "be helpful"),
                new(ChatRole.Assistant, [new FunctionCallContent("call-1", "G4Tool", new Dictionary<string, object?>())]),
                new(ChatRole.Tool, [new FunctionResultContent("call-1", HarnessArtifactIdentity.BuildReferenceId(digest))]),
                new(ChatRole.User, "hello"),
            };
            var classifier = new HarnessScriptedMessageClassifier();
            var hybridProfile = HarnessHybridProfile.Create(
                HarnessCompactionTestFixture.CreatePolicy(
                    1000, 1, 5, 3, new HarnessConstantSizeContextEstimator(1)),
                HarnessScriptedUpstreamChatReducer.Echo(),
                classifier,
                baselineEntries => new HarnessMutableContextSnapshotProvider(
                    HarnessContextSnapshotAugmentation.WithRecoverableSegment(
                        baselineEntries, recoveredEntryId, segment)));

            var leaf = new FailFirstCallThenSucceedChatClient();
            var coordinator = new HarnessCompactionRunCoordinator();

            // One shared outer run scope: the failed first call and its retry both nest inside it,
            // exactly as a caller-level retry loop around one outer agent run would observe.
            using var runScope = coordinator.BeginRun();
            var compactionClient = new HarnessHybridCompactionChatClient(
                leaf, hybridProfile, binding, accessor, HarnessCompositionTestFixture.SessionId, coordinator, progressAccessor: null);

            // The first call's real provider dispatch fails after assembly already reserved the
            // recoverable body's digest for that call's lease. The reservation must be released
            // rather than left stranded, so this exact digest can be reserved — and delivered — again
            // by the retry below.
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => compactionClient.GetResponseAsync(
                    messages, cancellationToken: TestContext.Current.CancellationToken));

            await compactionClient.GetResponseAsync(
                messages, cancellationToken: TestContext.Current.CancellationToken);

            Assert.Equal(2, leaf.CallCount);

            // The failed attempt still assembled and forwarded the raw body to the real provider
            // (proving the failure occurred at dispatch, not before assembly ever selected it) ...
            Assert.Contains(leaf.ObservedCalls[0], message => message.Text == rawBody);

            // ... and the retry, within the very same run scope, still receives the exact same body:
            // had the reservation not been released on failure, this second call's fresh lease would
            // have found the digest still reserved by the failed call's lease and filtered it out.
            Assert.Contains(leaf.ObservedCalls[1], message => message.Text == rawBody);
        }
    }

    /// <summary>
    /// Proves the closure fix in
    /// <see cref="HarnessHybridCompactionChatClient.GetStreamingResponseAsync"/>: when the inner
    /// <see cref="IAsyncEnumerable{T}.GetAsyncEnumerator"/> throws <em>synchronously</em> after
    /// assembly has already reserved the recoverable body's digest for the call's lease, the
    /// <see langword="finally"/> block must still release that reservation — not leave it
    /// stranded — so a retry within the same outer run scope can reserve the exact same digest
    /// and deliver the raw body again.
    /// <para>
    /// This is the streaming analogue of
    /// <see cref="ProviderCallFails_ReservationReleased_RetryWithinSameRunRedeliversRawBody"/>.
    /// The distinction being exercised here is specifically that the failure occurs synchronously
    /// inside <c>GetAsyncEnumerator</c> (enumerator initialization), not during
    /// <c>MoveNextAsync</c> iteration — a code path that is only guarded if both
    /// <c>GetStreamingResponseAsync</c> and <c>GetAsyncEnumerator</c> are inside the
    /// <see langword="try"/>/<see langword="finally"/> region.
    /// </para>
    /// </summary>
    [Fact]
    public async Task StreamingGetAsyncEnumeratorThrows_LeaseReleased_RetryWithinSameRunRedeliversRawBody()
    {
        const string rawBody = "SECRET-RAW-RECOVERED-BODY";
        const string artifactContentSeed = "coordinator-streaming-enum-throw-artifact-content";
        const string recoveredEntryId = "recovered-entry-id";
        var digest = HarnessArtifactIdentity.ComputeDigest(artifactContentSeed);
        var reference = HarnessCompactionTestFixture.SampleReference(artifactContentSeed, DateTimeOffset.UtcNow);
        var segment = HarnessArtifactRecoverableContextSegment.Create(reference, rawBody, DateTimeOffset.UtcNow);

        using var services = HarnessCompositionTestFixture.CreateServices();
        var accessor = new AgentExecutionContextAccessor();
        var binding = HarnessCompositionTestFixture.CaptureBinding(accessor, out var scope);
        using (scope)
        {
            var messages = new List<ChatMessage>
            {
                new(ChatRole.System, "be helpful"),
                new(ChatRole.Assistant, [new FunctionCallContent("call-1", "G4Tool", new Dictionary<string, object?>())]),
                new(ChatRole.Tool, [new FunctionResultContent("call-1", HarnessArtifactIdentity.BuildReferenceId(digest))]),
                new(ChatRole.User, "hello"),
            };
            var classifier = new HarnessScriptedMessageClassifier();
            var hybridProfile = HarnessHybridProfile.Create(
                HarnessCompactionTestFixture.CreatePolicy(
                    1000, 1, 5, 3, new HarnessConstantSizeContextEstimator(1)),
                HarnessScriptedUpstreamChatReducer.Echo(),
                classifier,
                baselineEntries => new HarnessMutableContextSnapshotProvider(
                    HarnessContextSnapshotAugmentation.WithRecoverableSegment(
                        baselineEntries, recoveredEntryId, segment)));

            var leaf = new StreamingEnumeratorThrowingFirstCallThenSucceedingChatClient();
            var coordinator = new HarnessCompactionRunCoordinator();

            // One shared outer run scope: the failed first call and its retry both nest inside it,
            // exactly as a caller-level retry loop within one outer agent run would.
            using var runScope = coordinator.BeginRun();
            var compactionClient = new HarnessHybridCompactionChatClient(
                leaf, hybridProfile, binding, accessor, HarnessCompositionTestFixture.SessionId, coordinator, progressAccessor: null);

            // The first streaming call's GetAsyncEnumerator() throws synchronously after assembly
            // already reserved the recoverable body's digest for that call's lease. The guarded
            // try/finally in GetStreamingResponseAsync must release the reservation — not strand
            // it — so the retry below can still deliver the body.
            var enumerator = compactionClient
                .GetStreamingResponseAsync(messages, cancellationToken: TestContext.Current.CancellationToken)
                .GetAsyncEnumerator(TestContext.Current.CancellationToken);
            await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await enumerator.MoveNextAsync());

            // The retry, within the very same run scope, must still receive the raw body: the
            // reservation the failed call held was released by the guarded finally, not stranded.
            await foreach (var _ in compactionClient.GetStreamingResponseAsync(
                messages, cancellationToken: TestContext.Current.CancellationToken))
            {
            }

            Assert.Equal(2, leaf.CallCount);

            // The first call was dispatched to the inner client (its GetStreamingResponseAsync
            // ran) and recorded those messages, including the raw recovered body.
            Assert.Contains(leaf.ObservedCalls[0], message => message.Text == rawBody);

            // The retry within the same run scope also receives the raw recovered body — had the
            // lease not been released by the first call's finally, this fresh lease would have
            // found the digest still reserved and filtered it out.
            Assert.Contains(leaf.ObservedCalls[1], message => message.Text == rawBody);
        }
    }

    // ── Direct coordinator unit tests ─────────────────────────────────────────────────────────

    /// <summary>
    /// A successful call whose selected recoverable segment was pressure-evicted during assembly
    /// (reserved via <see cref="HarnessCompactionRunCoordinator.TryReserve"/> but stripped from
    /// <c>FinalEntries</c> before dispatch) must not leave that digest stranded as reserved after
    /// <see cref="HarnessCompactionRunCoordinator.Complete"/> closes the lease. A later call in the
    /// same run scope must be able to reserve the exact same digest again so that, if a later snapshot
    /// or policy would forward it, it can still be delivered.
    /// </summary>
    [Fact]
    public void Complete_PressureEvictedDigestNotInDeliveredSet_IsReleasedAtomically_LaterLeaseCanReserve()
    {
        // Any non-empty, non-whitespace string is valid as a digest key in the coordinator;
        // this test exercises the reservation/release bookkeeping, not the artifact identity format.
        const string digest = "pressure-eviction-test-digest-placeholder";
        var coordinator = new HarnessCompactionRunCoordinator();
        var leaseA = Guid.NewGuid();
        var leaseB = Guid.NewGuid();

        using var runScope = coordinator.BeginRun();

        // Lease A reserves the digest — simulating what HarnessDeliveredSegmentFilteringSnapshotProvider
        // does during assembly when the recoverable segment is first observed.
        Assert.True(coordinator.TryReserve(digest, leaseA));

        // The segment is pressure-evicted before dispatch: it was reserved but is NOT in the
        // forwarded set.  Complete closes lease A with an empty delivered set.
        coordinator.Complete(leaseA, []);

        // The evicted segment's reservation must have been released atomically by Complete,
        // even though it was never delivered.  Lease B — representing a later provider call
        // within the same outer run — must now be able to reserve the exact same digest.
        Assert.True(
            coordinator.TryReserve(digest, leaseB),
            "Expected the pressure-evicted digest to be reservable again after Complete released it, " +
            "but TryReserve returned false — the reservation was stranded.");
    }

    /// <summary>
    /// <see cref="HarnessCompactionRunCoordinator.Complete"/> closes a lease atomically: digests in the
    /// delivered set are promoted, and ALL remaining reservations owned by the lease — including those
    /// not supplied to Complete — are released in the same lock acquisition, never stranded.
    /// </summary>
    [Fact]
    public void Complete_MultipleLeasedDigests_OnlyDeliveredOnesPromoted_RestReleased()
    {
        const string digestA = "delivered-digest-a";
        const string digestB = "evicted-digest-b";
        var coordinator = new HarnessCompactionRunCoordinator();
        var leaseA = Guid.NewGuid();
        var leaseC = Guid.NewGuid();

        using var runScope = coordinator.BeginRun();

        // Lease A reserves both digests — A survives assembly, B is pressure-evicted.
        Assert.True(coordinator.TryReserve(digestA, leaseA));
        Assert.True(coordinator.TryReserve(digestB, leaseA));

        // Complete with only the forwarded digest.
        coordinator.Complete(leaseA, [digestA]);

        // digestA was promoted to Delivered: lease C cannot reserve it.
        Assert.False(
            coordinator.TryReserve(digestA, leaseC),
            "digestA must be Delivered after Complete — a new lease must not be able to reserve it.");

        // digestB was NOT delivered and must have been released: lease C can now reserve it.
        Assert.True(
            coordinator.TryReserve(digestB, leaseC),
            "digestB must be released by Complete (not stranded) — a new lease must be able to reserve it.");
    }

    /// <summary>
    /// <see cref="HarnessCompactionRunCoordinator.TryReserve"/> must throw
    /// <see cref="InvalidOperationException"/> when no run scope is active. Every legitimate path
    /// establishes a scope via <see cref="HarnessCompactionRunCoordinator.EnsureRunScope"/> or
    /// <see cref="HarnessCompactionRunCoordinator.BeginRun"/> before invoking any lease-lifecycle
    /// operation; the absence of a scope is a contract violation, not a recoverable condition.
    /// </summary>
    [Fact]
    public void TryReserve_WithoutActiveRunScope_ThrowsInvalidOperationException()
    {
        var coordinator = new HarnessCompactionRunCoordinator();
        Assert.Throws<InvalidOperationException>(
            () => coordinator.TryReserve("any-non-empty-digest", Guid.NewGuid()));
    }

    /// <summary>
    /// <see cref="HarnessCompactionRunCoordinator.Complete"/> must throw
    /// <see cref="InvalidOperationException"/> when no run scope is active, for the same reason
    /// as <see cref="TryReserve_WithoutActiveRunScope_ThrowsInvalidOperationException"/>.
    /// </summary>
    [Fact]
    public void Complete_WithoutActiveRunScope_ThrowsInvalidOperationException()
    {
        var coordinator = new HarnessCompactionRunCoordinator();
        Assert.Throws<InvalidOperationException>(
            () => coordinator.Complete(Guid.NewGuid(), []));
    }

    /// <summary>
    /// <see cref="HarnessCompactionRunCoordinator.GetRevision"/> must throw
    /// <see cref="InvalidOperationException"/> when no run scope is active, for the same reason as
    /// every other lease-lifecycle operation on this type.
    /// </summary>
    [Fact]
    public void GetRevision_WithoutActiveRunScope_ThrowsInvalidOperationException()
    {
        var coordinator = new HarnessCompactionRunCoordinator();
        Assert.Throws<InvalidOperationException>(
            () => coordinator.GetRevision("any-non-empty-digest"));
    }

    /// <summary>
    /// <see cref="HarnessCompactionRunCoordinator.GetRevision"/> reports <c>0</c> for a digest that has
    /// never been reserved, delivered, or released in the active run — the closed-world default before
    /// any state exists for it.
    /// </summary>
    [Fact]
    public void GetRevision_BeforeAnyState_ReturnsZero()
    {
        var coordinator = new HarnessCompactionRunCoordinator();
        using var runScope = coordinator.BeginRun();

        Assert.Equal(0, coordinator.GetRevision("never-touched-digest"));
    }

    /// <summary>
    /// <see cref="HarnessCompactionRunCoordinator.GetRevision"/> advances by exactly one on a digest's
    /// first reservation, by exactly one more when <see cref="HarnessCompactionRunCoordinator.Complete"/>
    /// promotes it to Delivered, and does not advance at all when the very same lease re-reserves the
    /// digest it already holds — no externally-observable state changed in that case.
    /// </summary>
    [Fact]
    public void GetRevision_FirstReservationThenPromotion_AdvancesOnlyOnRealStateChanges()
    {
        const string digest = "revision-tracking-digest";
        var coordinator = new HarnessCompactionRunCoordinator();
        var leaseId = Guid.NewGuid();

        using var runScope = coordinator.BeginRun();

        Assert.Equal(0, coordinator.GetRevision(digest));

        Assert.True(coordinator.TryReserve(digest, leaseId));
        Assert.Equal(1, coordinator.GetRevision(digest));

        // The same lease re-reserving its own digest observes no externally-visible state change.
        Assert.True(coordinator.TryReserve(digest, leaseId));
        Assert.Equal(1, coordinator.GetRevision(digest));

        coordinator.Complete(leaseId, [digest]);
        Assert.Equal(2, coordinator.GetRevision(digest));
    }

    /// <summary>
    /// <see cref="HarnessCompactionRunCoordinator.GetRevision"/> advances when
    /// <see cref="HarnessCompactionRunCoordinator.Complete"/> releases a reserved-but-not-delivered
    /// digest (the pressure-eviction case), and again when a later lease's own
    /// <see cref="HarnessCompactionRunCoordinator.Release"/> releases its own reservation — every
    /// reserved-to-unclaimed transition is an externally-observable state change.
    /// </summary>
    [Fact]
    public void GetRevision_CompleteReleaseAndExplicitRelease_BothAdvanceRevision()
    {
        const string digest = "revision-release-digest";
        var coordinator = new HarnessCompactionRunCoordinator();
        var leaseA = Guid.NewGuid();
        var leaseB = Guid.NewGuid();

        using var runScope = coordinator.BeginRun();

        Assert.True(coordinator.TryReserve(digest, leaseA));
        var afterFirstReservation = coordinator.GetRevision(digest);

        // Pressure-evicted: reserved but never forwarded — Complete releases it without promoting.
        coordinator.Complete(leaseA, []);
        var afterCompleteRelease = coordinator.GetRevision(digest);
        Assert.True(afterCompleteRelease > afterFirstReservation);

        Assert.True(coordinator.TryReserve(digest, leaseB));
        var afterSecondReservation = coordinator.GetRevision(digest);
        Assert.True(afterSecondReservation > afterCompleteRelease);

        coordinator.Release(leaseB);
        var afterExplicitRelease = coordinator.GetRevision(digest);
        Assert.True(afterExplicitRelease > afterSecondReservation);
    }

    /// <summary>
    /// A revision change to some other digest never present in a given
    /// <see cref="HarnessDeliveredSegmentFilteringSnapshotProvider"/> instance's own captured entries can
    /// never affect that instance's effective version: its signature only ever tracks the recoverable
    /// digests its own inner snapshot actually reports, so it can never spuriously restart an assembly
    /// attempt that never observed the unrelated digest in the first place.
    /// </summary>
    [Fact]
    public void FilteringSnapshotProvider_UnrelatedDigestRevisionChange_EffectiveVersionUnaffected()
    {
        const string ownContentSeed = "unrelated-digest-test-own-artifact-content";
        const string rawBody = "own-raw-body";
        const string recoveredEntryId = "recovered-entry-id";
        var ownDigest = HarnessArtifactIdentity.ComputeDigest(ownContentSeed);
        var unrelatedDigest = HarnessArtifactIdentity.ComputeDigest("unrelated-digest-test-unrelated-content");
        var coordinator = new HarnessCompactionRunCoordinator();
        var ownLease = Guid.NewGuid();
        var unrelatedLease = Guid.NewGuid();

        using var runScope = coordinator.BeginRun();

        var reference = HarnessCompactionTestFixture.SampleReference(ownContentSeed, DateTimeOffset.UtcNow);
        var segment = HarnessArtifactRecoverableContextSegment.Create(reference, rawBody, DateTimeOffset.UtcNow);
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, "be helpful"),
            new(ChatRole.Assistant, [new FunctionCallContent("call-1", "G4Tool", new Dictionary<string, object?>())]),
            new(ChatRole.Tool, [new FunctionResultContent("call-1", HarnessArtifactIdentity.BuildReferenceId(ownDigest))]),
            new(ChatRole.User, "hello"),
        };
        var classifier = new HarnessScriptedMessageClassifier();
        var baselineEntries = HarnessMafMessageContextAdapter.Adapt(messages, classifier);
        var entriesWithOwnSegment = HarnessContextSnapshotAugmentation.WithRecoverableSegment(
            baselineEntries, recoveredEntryId, segment);

        var innerProvider = new HarnessMutableContextSnapshotProvider(entriesWithOwnSegment);
        var filteringProvider = new HarnessDeliveredSegmentFilteringSnapshotProvider(
            innerProvider, coordinator, ownLease);

        var firstCapture = filteringProvider.CaptureSnapshot();

        // An entirely unrelated digest — never present in this provider's own entries — has its
        // coordinator revision change via a completely separate lease.
        Assert.True(coordinator.TryReserve(unrelatedDigest, unrelatedLease));
        coordinator.Complete(unrelatedLease, []);

        // The exact same entries, recaptured: this provider's own effective version must be unaffected
        // by the unrelated digest's revision change.
        var secondCapture = filteringProvider.CaptureSnapshot();
        Assert.Equal(firstCapture.Version, secondCapture.Version);
    }

    /// <summary>
    /// A <see cref="HarnessDeliveredSegmentFilteringSnapshotProvider"/> instance's own repeated captures
    /// — the same lease observing the same entries and the same coordinator state every time, exactly
    /// like an assembler's own first-capture/finalization-recapture pair when nothing raced in — must
    /// report a perfectly stable effective version across every one of those captures.
    /// </summary>
    [Fact]
    public void FilteringSnapshotProvider_OwnRepeatedCaptures_EffectiveVersionStable()
    {
        const string contentSeed = "stable-repeated-capture-artifact-content";
        const string rawBody = "stable-raw-body";
        const string recoveredEntryId = "recovered-entry-id";
        var digest = HarnessArtifactIdentity.ComputeDigest(contentSeed);
        var coordinator = new HarnessCompactionRunCoordinator();
        var leaseId = Guid.NewGuid();

        using var runScope = coordinator.BeginRun();

        var reference = HarnessCompactionTestFixture.SampleReference(contentSeed, DateTimeOffset.UtcNow);
        var segment = HarnessArtifactRecoverableContextSegment.Create(reference, rawBody, DateTimeOffset.UtcNow);
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, "be helpful"),
            new(ChatRole.Assistant, [new FunctionCallContent("call-1", "G4Tool", new Dictionary<string, object?>())]),
            new(ChatRole.Tool, [new FunctionResultContent("call-1", HarnessArtifactIdentity.BuildReferenceId(digest))]),
            new(ChatRole.User, "hello"),
        };
        var classifier = new HarnessScriptedMessageClassifier();
        var baselineEntries = HarnessMafMessageContextAdapter.Adapt(messages, classifier);
        var entriesWithSegment = HarnessContextSnapshotAugmentation.WithRecoverableSegment(
            baselineEntries, recoveredEntryId, segment);

        var innerProvider = new HarnessMutableContextSnapshotProvider(entriesWithSegment);
        var filteringProvider = new HarnessDeliveredSegmentFilteringSnapshotProvider(
            innerProvider, coordinator, leaseId);

        var first = filteringProvider.CaptureSnapshot();
        var second = filteringProvider.CaptureSnapshot();
        var third = filteringProvider.CaptureSnapshot();

        Assert.Equal(first.Version, second.Version);
        Assert.Equal(second.Version, third.Version);
    }

    /// <summary>
    /// <see cref="HarnessCompactionRunCoordinator.Complete"/> releasing a digest this provider's own
    /// snapshot includes (the pressure-eviction case, where the releasing lease is a different,
    /// concurrently-running call) advances this provider's effective version on its very next capture,
    /// even when neither the inner snapshot's own version nor this provider's own reservation outcome
    /// for the digest otherwise changed.
    /// </summary>
    [Fact]
    public void FilteringSnapshotProvider_ConcurrentLeaseReleasesRelevantDigest_EffectiveVersionAdvances()
    {
        const string contentSeed = "release-relevant-digest-artifact-content";
        const string rawBody = "release-raw-body";
        const string recoveredEntryId = "recovered-entry-id";
        var digest = HarnessArtifactIdentity.ComputeDigest(contentSeed);
        var coordinator = new HarnessCompactionRunCoordinator();
        var observingLease = Guid.NewGuid();
        var otherLease = Guid.NewGuid();

        using var runScope = coordinator.BeginRun();

        var reference = HarnessCompactionTestFixture.SampleReference(contentSeed, DateTimeOffset.UtcNow);
        var segment = HarnessArtifactRecoverableContextSegment.Create(reference, rawBody, DateTimeOffset.UtcNow);
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, "be helpful"),
            new(ChatRole.Assistant, [new FunctionCallContent("call-1", "G4Tool", new Dictionary<string, object?>())]),
            new(ChatRole.Tool, [new FunctionResultContent("call-1", HarnessArtifactIdentity.BuildReferenceId(digest))]),
            new(ChatRole.User, "hello"),
        };
        var classifier = new HarnessScriptedMessageClassifier();
        var baselineEntries = HarnessMafMessageContextAdapter.Adapt(messages, classifier);
        var entriesWithSegment = HarnessContextSnapshotAugmentation.WithRecoverableSegment(
            baselineEntries, recoveredEntryId, segment);

        // A different, concurrently-running lease reserves the digest first — the observing provider's
        // own first capture below therefore filters the body out, exactly like the losing side of the
        // race in SameOuterRun_LoserFiltersThenWinnerPressureEvictsAndReleases_LoserRestartsAndDeliversExactlyOnce.
        Assert.True(coordinator.TryReserve(digest, otherLease));

        var innerProvider = new HarnessMutableContextSnapshotProvider(entriesWithSegment);
        var filteringProvider = new HarnessDeliveredSegmentFilteringSnapshotProvider(
            innerProvider, coordinator, observingLease);

        var firstCapture = filteringProvider.CaptureSnapshot();
        Assert.DoesNotContain(
            firstCapture.Entries, e => e.Kind == HarnessContextEntryKind.RecoverableContextSegment);

        // The other lease completes without forwarding — pressure-evicted — releasing the digest.
        coordinator.Complete(otherLease, []);

        var secondCapture = filteringProvider.CaptureSnapshot();
        Assert.NotEqual(firstCapture.Version, secondCapture.Version);
        Assert.Contains(
            secondCapture.Entries, e => e.Kind == HarnessContextEntryKind.RecoverableContextSegment);
    }

    /// <summary>
    /// <see cref="HarnessCompactionRunCoordinator.EnsureRunScope"/> must keep direct compaction
    /// paths working: when no outer run is active, it begins a call-local scope, and any
    /// <see cref="HarnessCompactionRunCoordinator.TryReserve"/> calls within that scope succeed
    /// without throwing.
    /// </summary>
    [Fact]
    public void EnsureRunScope_WhenNoOuterRunActive_BeginsFreshScope_TryReserveSucceeds()
    {
        var coordinator = new HarnessCompactionRunCoordinator();
        var leaseId = Guid.NewGuid();

        using (coordinator.EnsureRunScope())
        {
            // Must not throw — the scope established by EnsureRunScope satisfies the
            // fail-closed contract on TryReserve.
            var reserved = coordinator.TryReserve("any-non-empty-digest", leaseId);
            Assert.True(reserved);
        }

        // After the scope is disposed, TryReserve must again throw — the scope was transient.
        Assert.Throws<InvalidOperationException>(
            () => coordinator.TryReserve("any-non-empty-digest", leaseId));
    }

    /// <summary>
    /// Test-only leaf <see cref="IChatClient"/> recording the exact materialized message list observed
    /// on every call, whose first call invokes a caller-supplied callback (used to enqueue a
    /// message-injection extra call, or to no-op) before returning a plain assistant response — never a
    /// function call — so a test can exercise a message-injection-driven second round independently of
    /// any FICC tool round.
    /// </summary>
    private sealed class InjectingObservingChatClient(Action afterFirstResponse) : IChatClient
    {
        private readonly List<IReadOnlyList<ChatMessage>> _observedCalls = [];

        internal IReadOnlyList<IReadOnlyList<ChatMessage>> ObservedCalls => _observedCalls;

        internal int CallCount => _observedCalls.Count;

        Task<ChatResponse> IChatClient.GetResponseAsync(
            IEnumerable<ChatMessage> chatMessages, ChatOptions? options, CancellationToken cancellationToken)
        {
            var materialized = chatMessages.ToList();
            _observedCalls.Add(materialized);

            if (_observedCalls.Count == 1)
            {
                afterFirstResponse();
                return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "model-result")));
            }

            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "final-result")));
        }

        IAsyncEnumerable<ChatResponseUpdate> IChatClient.GetStreamingResponseAsync(
            IEnumerable<ChatMessage> chatMessages, ChatOptions? options, CancellationToken cancellationToken) =>
            throw new NotSupportedException("Streaming is not required by this test client.");

        object? IChatClient.GetService(Type serviceType, object? key) => null;

        void IDisposable.Dispose()
        {
        }
    }

    /// <summary>
    /// Test-only leaf <see cref="IChatClient"/> supporting only streaming, recording the exact
    /// materialized message list observed on every call and yielding a single fixed update.
    /// </summary>
    private sealed class StreamingObservingChatClient : IChatClient
    {
        private readonly List<IReadOnlyList<ChatMessage>> _observedCalls = [];

        internal IReadOnlyList<IReadOnlyList<ChatMessage>> ObservedCalls => _observedCalls;

        internal int CallCount => _observedCalls.Count;

        Task<ChatResponse> IChatClient.GetResponseAsync(
            IEnumerable<ChatMessage> chatMessages, ChatOptions? options, CancellationToken cancellationToken) =>
            throw new NotSupportedException("Non-streaming execution is not required by this test client.");

        async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsyncCore(
            IEnumerable<ChatMessage> chatMessages,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var materialized = chatMessages.ToList();
            _observedCalls.Add(materialized);
            await Task.Yield();
            yield return new ChatResponseUpdate(ChatRole.Assistant, "streamed");
        }

        IAsyncEnumerable<ChatResponseUpdate> IChatClient.GetStreamingResponseAsync(
            IEnumerable<ChatMessage> chatMessages, ChatOptions? options, CancellationToken cancellationToken) =>
            GetStreamingResponseAsyncCore(chatMessages, cancellationToken);

        object? IChatClient.GetService(Type serviceType, object? key) => null;

        void IDisposable.Dispose()
        {
        }
    }

    /// <summary>
    /// Test-only leaf <see cref="IChatClient"/> that blocks every call until the configured number of
    /// expected concurrent calls have all arrived, forcing genuinely overlapping execution so a
    /// concurrency test cannot pass merely because calls happened to run sequentially. Thread-safe:
    /// every mutable field is guarded by a lock or is itself thread-safe.
    /// </summary>
    private sealed class BarrierObservingChatClient(int expectedConcurrentCalls) : IChatClient
    {
        private readonly object _gate = new();
        private readonly List<IReadOnlyList<ChatMessage>> _observedCalls = [];
        private readonly TaskCompletionSource _allArrived = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _arrivals;

        internal IReadOnlyList<IReadOnlyList<ChatMessage>> ObservedCalls
        {
            get
            {
                lock (_gate)
                {
                    return [.. _observedCalls];
                }
            }
        }

        internal int CallCount
        {
            get
            {
                lock (_gate)
                {
                    return _observedCalls.Count;
                }
            }
        }

        async Task<ChatResponse> IChatClient.GetResponseAsync(
            IEnumerable<ChatMessage> chatMessages, ChatOptions? options, CancellationToken cancellationToken)
        {
            var materialized = chatMessages.ToList();
            lock (_gate)
            {
                _observedCalls.Add(materialized);
            }

            if (Interlocked.Increment(ref _arrivals) >= expectedConcurrentCalls)
            {
                _allArrived.TrySetResult();
            }

            await _allArrived.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken).ConfigureAwait(false);

            return new ChatResponse(new ChatMessage(ChatRole.Assistant, "ok"));
        }

        IAsyncEnumerable<ChatResponseUpdate> IChatClient.GetStreamingResponseAsync(
            IEnumerable<ChatMessage> chatMessages, ChatOptions? options, CancellationToken cancellationToken) =>
            throw new NotSupportedException("Streaming is not required by this test client.");

        object? IChatClient.GetService(Type serviceType, object? key) => null;

        void IDisposable.Dispose()
        {
        }
    }

    /// <summary>
    /// Test-only leaf <see cref="IChatClient"/> recording the exact materialized message list observed
    /// on every call, throwing <see cref="InvalidOperationException"/> on its first call (simulating a
    /// transient real-provider dispatch failure after assembly already reserved a recoverable body's
    /// digest) and succeeding with a plain assistant response on every subsequent call.
    /// </summary>
    private sealed class FailFirstCallThenSucceedChatClient : IChatClient
    {
        private readonly List<IReadOnlyList<ChatMessage>> _observedCalls = [];

        internal IReadOnlyList<IReadOnlyList<ChatMessage>> ObservedCalls => _observedCalls;

        internal int CallCount => _observedCalls.Count;

        async Task<ChatResponse> IChatClient.GetResponseAsync(
            IEnumerable<ChatMessage> chatMessages, ChatOptions? options, CancellationToken cancellationToken)
        {
            var materialized = chatMessages.ToList();
            _observedCalls.Add(materialized);
            await Task.Yield();

            if (_observedCalls.Count == 1)
            {
                throw new InvalidOperationException("Simulated transient real-provider dispatch failure.");
            }

            return new ChatResponse(new ChatMessage(ChatRole.Assistant, "ok"));
        }

        IAsyncEnumerable<ChatResponseUpdate> IChatClient.GetStreamingResponseAsync(
            IEnumerable<ChatMessage> chatMessages, ChatOptions? options, CancellationToken cancellationToken) =>
            throw new NotSupportedException("Streaming is not required by this test client.");

        object? IChatClient.GetService(Type serviceType, object? key) => null;

        void IDisposable.Dispose()
        {
        }
    }

    /// <summary>
    /// Test-only, content-derived <see cref="IHarnessContextMessageClassifier"/> mirroring
    /// <see cref="HarnessScriptedMessageClassifier"/>'s default (no-override) entry-id derivation, but
    /// without that fixture's <c>ResolvedIndices</c> tracking list — deliberately free of any shared
    /// mutable state, so a single instance is safe to use concurrently from two overlapping outer runs.
    /// </summary>
    private sealed class NoOverrideMessageClassifier : IHarnessContextMessageClassifier
    {
        public string ResolveEntryId(ChatMessage message, int index, IReadOnlyList<ChatMessage> allMessages)
        {
            var contentSeed = string.Join(
                '|',
                message.Contents.Select(content => content switch
                {
                    TextContent text => $"text:{text.Text}",
                    FunctionCallContent call => $"call:{call.CallId}:{call.Name}",
                    FunctionResultContent result => $"result:{result.CallId}",
                    _ => content.GetType().Name,
                }));
            var seed = $"{message.Role}|{message.AuthorName}|{contentSeed}";
            return $"entry-{HarnessArtifactIdentity.ComputeDigest(seed)}";
        }

        public HarnessContextEntryKind? ClassifyOverride(
            ChatMessage message, int index, IReadOnlyList<ChatMessage> allMessages) => null;
    }

    /// <summary>
    /// Test-only leaf <see cref="IChatClient"/> supporting only streaming. Records the exact
    /// materialized message list supplied to every <c>GetStreamingResponseAsync</c> call. On the
    /// first call, returns an <see cref="IAsyncEnumerable{T}"/> whose
    /// <see cref="IAsyncEnumerable{T}.GetAsyncEnumerator"/> throws synchronously with an
    /// <see cref="InvalidOperationException"/> — simulating a synchronous initialization failure
    /// that occurs after assembly has already reserved a recoverable body's lease. On every
    /// subsequent call, returns a normal enumerable that yields a single update, so a retry can
    /// prove the first call's reservation was correctly released.
    /// </summary>
    private sealed class StreamingEnumeratorThrowingFirstCallThenSucceedingChatClient : IChatClient
    {
        private readonly List<IReadOnlyList<ChatMessage>> _observedCalls = [];
        private int _callCount;

        internal IReadOnlyList<IReadOnlyList<ChatMessage>> ObservedCalls => _observedCalls;

        internal int CallCount => _callCount;

        Task<ChatResponse> IChatClient.GetResponseAsync(
            IEnumerable<ChatMessage> chatMessages, ChatOptions? options, CancellationToken cancellationToken) =>
            throw new NotSupportedException("Non-streaming execution is not required by this test client.");

        IAsyncEnumerable<ChatResponseUpdate> IChatClient.GetStreamingResponseAsync(
            IEnumerable<ChatMessage> chatMessages, ChatOptions? options, CancellationToken cancellationToken)
        {
            var callIndex = Interlocked.Increment(ref _callCount);
            var materialized = chatMessages.ToList();
            _observedCalls.Add(materialized);

            if (callIndex == 1)
            {
                // GetAsyncEnumerator() on this enumerable throws synchronously, triggering the
                // closure-fixing guard in HarnessHybridCompactionChatClient.GetStreamingResponseAsync.
                return new GetAsyncEnumeratorThrowingEnumerable();
            }

            return new SingleUpdateEnumerable();
        }

        object? IChatClient.GetService(Type serviceType, object? key) => null;

        void IDisposable.Dispose()
        {
        }

        /// <summary>
        /// An <see cref="IAsyncEnumerable{T}"/> whose <see cref="GetAsyncEnumerator"/> throws
        /// <see cref="InvalidOperationException"/> synchronously — exactly the failure mode the
        /// enumerator-initialization guard in
        /// <see cref="HarnessHybridCompactionChatClient.GetStreamingResponseAsync"/> must handle.
        /// </summary>
        private sealed class GetAsyncEnumeratorThrowingEnumerable : IAsyncEnumerable<ChatResponseUpdate>
        {
            public IAsyncEnumerator<ChatResponseUpdate> GetAsyncEnumerator(
                CancellationToken cancellationToken = default) =>
                throw new InvalidOperationException(
                    "Simulated synchronous GetAsyncEnumerator initialization failure.");
        }

        /// <summary>
        /// A plain single-update <see cref="IAsyncEnumerable{T}"/> used by the retry call to
        /// confirm streaming succeeds once the first call's reservation has been released.
        /// </summary>
        private sealed class SingleUpdateEnumerable : IAsyncEnumerable<ChatResponseUpdate>
        {
            public IAsyncEnumerator<ChatResponseUpdate> GetAsyncEnumerator(
                CancellationToken cancellationToken = default) =>
                new SingleUpdateEnumerator();

            private sealed class SingleUpdateEnumerator : IAsyncEnumerator<ChatResponseUpdate>
            {
                private int _position;

                public ChatResponseUpdate Current { get; private set; } = default!;

                public ValueTask<bool> MoveNextAsync()
                {
                    if (_position++ == 0)
                    {
                        Current = new ChatResponseUpdate(ChatRole.Assistant, "streamed");
                        return new ValueTask<bool>(true);
                    }

                    return new ValueTask<bool>(false);
                }

                public ValueTask DisposeAsync() => ValueTask.CompletedTask;
            }
        }
    }
}
