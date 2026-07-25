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
                new(ChatRole.Tool, HarnessArtifactIdentity.BuildReferenceId(digest1)),
                new(ChatRole.Tool, HarnessArtifactIdentity.BuildReferenceId(digest2)),
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
                leaf, hybridProfile, binding, accessor, HarnessCompositionTestFixture.SessionId, coordinator);

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
                new(ChatRole.Tool, HarnessArtifactIdentity.BuildReferenceId(digest)),
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
                leaf, hybridProfile, binding, accessor, HarnessCompositionTestFixture.SessionId, runCoordinator: null);

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
                new(ChatRole.Tool, HarnessArtifactIdentity.BuildReferenceId(digest)),
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
                leaf, hybridProfile, binding, accessor, HarnessCompositionTestFixture.SessionId, coordinator);

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
        // second run to reach HarnessCompactionRunCoordinator.MarkDelivered would incorrectly find the
        // digest already delivered and the raw body would reach the real provider only once in total,
        // instead of once per run.
        await Task.WhenAll(RunOnceAsync(), RunOnceAsync());

        Assert.Equal(2, leaf.CallCount);
        Assert.Equal(2, leaf.ObservedCalls.Count(call => call.Any(message => message.Text == rawBody)));
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
}
