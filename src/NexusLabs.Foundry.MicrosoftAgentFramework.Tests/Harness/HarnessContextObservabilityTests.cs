// Tests intentionally exercise explicit CancellationToken parameters (including
// CancellationToken.None) directly. This is the behavior under test, not an oversight of
// TestContext.Current.CancellationToken.
#pragma warning disable xUnit1051

using System.Reflection;

using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

using NexusLabs.Foundry.MicrosoftAgentFramework.Context;
using NexusLabs.Foundry.MicrosoftAgentFramework.Diagnostics;
using NexusLabs.Foundry.MicrosoftAgentFramework.Harness;
using NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Capabilities;
using NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Context;
using NexusLabs.Foundry.MicrosoftAgentFramework.Progress;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Tests.Harness;

/// <summary>
/// Focused tests for the hybrid context compaction progress events
/// (<see cref="HarnessContextCompactionStartedEvent"/>, <see cref="HarnessContextCompactionCompletedEvent"/>,
/// <see cref="HarnessContextCompactionTerminatedEvent"/>, <see cref="HarnessContextComposedEvent"/>) and the
/// <see cref="HarnessContextDiagnostics"/> snapshot they carry: exactly-once emission and correlation per
/// outcome, the completed-vs-composed shared-instance contract, category-contribution/measurement-unit
/// correctness, and the complete absence of raw content from every emitted event.
/// </summary>
public sealed class HarnessContextObservabilityTests
{
    [Fact]
    public async Task WithinLimit_EmitsStartedThenCompletedThenComposed_ExactlyOnceEach_NoTerminated()
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, "be helpful"),
            new(ChatRole.User, "hello"),
        };
        var policy = HarnessCompactionTestFixture.CreatePolicy(
            1000, 500, 5, 3, new HarnessConstantSizeContextEstimator(1));

        var events = await RunAssemblyAsync(messages, policy, HarnessScriptedUpstreamChatReducer.Echo());

        var started = Assert.Single(events.OfType<HarnessContextCompactionStartedEvent>());
        var completed = Assert.Single(events.OfType<HarnessContextCompactionCompletedEvent>());
        var composed = Assert.Single(events.OfType<HarnessContextComposedEvent>());
        Assert.Empty(events.OfType<HarnessContextCompactionTerminatedEvent>());

        Assert.Equal(HarnessContextCompactionOutcome.WithinLimit, completed.Diagnostics.Outcome);
        Assert.Same(completed.Diagnostics, composed.Diagnostics);
        Assert.True(completed.Diagnostics.FinalSequenceValid);
        Assert.True(started.SequenceNumber < completed.SequenceNumber);
        Assert.True(completed.SequenceNumber < composed.SequenceNumber);
        Assert.Equal(HarnessContextMeasurementUnit.HostDefinedUnits, started.MeasurementUnit);
        Assert.Equal(1000, started.HardLimit);
        Assert.Equal(500, started.TriggerThreshold);

        // Per-assembly correlation: all three events for one successful attempt share one
        // non-empty AssemblyId.
        Assert.NotEqual(Guid.Empty, started.AssemblyId);
        Assert.Equal(started.AssemblyId, completed.AssemblyId);
        Assert.Equal(started.AssemblyId, composed.AssemblyId);
    }

    [Fact]
    public async Task Reduced_EmitsStartedThenCompletedThenComposed_DiagnosticsReflectReducedOutcome()
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, "instructions"),
            new(ChatRole.User, "old filler message"),
            new(ChatRole.User, "recent message"),
        };
        var classifier = new HarnessScriptedMessageClassifier();
        var sizes = new Dictionary<string, int>
        {
            [classifier.ResolveEntryId(messages[0], 0, messages)] = 30,
            [classifier.ResolveEntryId(messages[1], 1, messages)] = 90,
            [classifier.ResolveEntryId(messages[2], 2, messages)] = 20,
        };
        var policy = HarnessCompactionTestFixture.CreatePolicy(100, 10, 1, 2, new HarnessFixedSizeContextEstimator(sizes));
        var reducer = new HarnessScriptedUpstreamChatReducer(
            (msgs, _) => Task.FromResult(msgs.Where(m => m.Text != "old filler message")));

        var events = await RunAssemblyAsync(messages, policy, reducer, classifier);

        var started = Assert.Single(events.OfType<HarnessContextCompactionStartedEvent>());
        var completed = Assert.Single(events.OfType<HarnessContextCompactionCompletedEvent>());
        var composed = Assert.Single(events.OfType<HarnessContextComposedEvent>());
        Assert.Empty(events.OfType<HarnessContextCompactionTerminatedEvent>());

        Assert.Equal(HarnessContextCompactionOutcome.Reduced, completed.Diagnostics.Outcome);
        Assert.Same(completed.Diagnostics, composed.Diagnostics);
        Assert.Equal(50, completed.Diagnostics.FinalSize);
        Assert.Equal(140, completed.Diagnostics.OriginalSize);
        Assert.Equal(
            completed.Diagnostics.FinalSize,
            completed.Diagnostics.CategoryContributions.Sum(c => c.Size));
        Assert.Equal(100, started.HardLimit);
        Assert.Equal(90, started.TriggerThreshold);
    }

    [Fact]
    public async Task PreservationFallback_EmitsStartedThenCompletedThenComposed_DiagnosticsReflectFallbackOutcome()
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, "instructions"),
            new(ChatRole.User, "old filler message"),
            new(ChatRole.User, "recent message"),
        };
        var classifier = new HarnessScriptedMessageClassifier();
        var sizes = new Dictionary<string, int>
        {
            [classifier.ResolveEntryId(messages[0], 0, messages)] = 30,
            [classifier.ResolveEntryId(messages[1], 1, messages)] = 90,
            [classifier.ResolveEntryId(messages[2], 2, messages)] = 20,
        };
        var policy = HarnessCompactionTestFixture.CreatePolicy(100, 10, 1, 2, new HarnessFixedSizeContextEstimator(sizes));

        // An echoing reducer never actually shrinks the proposal, so the assembler falls back to its
        // deterministic required-plus-recent-window candidate instead of ever forwarding the unchanged,
        // still-over-budget proposal.
        var events = await RunAssemblyAsync(messages, policy, HarnessScriptedUpstreamChatReducer.Echo(), classifier);

        var completed = Assert.Single(events.OfType<HarnessContextCompactionCompletedEvent>());
        var composed = Assert.Single(events.OfType<HarnessContextComposedEvent>());
        Assert.Empty(events.OfType<HarnessContextCompactionTerminatedEvent>());

        Assert.Equal(HarnessContextCompactionOutcome.PreservationFallback, completed.Diagnostics.Outcome);
        Assert.Same(completed.Diagnostics, composed.Diagnostics);
        Assert.Equal(50, completed.Diagnostics.FinalSize);
        Assert.Contains(HarnessContextAssemblyStageCategory.DeterministicFallback, completed.Diagnostics.Stages);
        Assert.Equal(
            completed.Diagnostics.FinalSize,
            completed.Diagnostics.CategoryContributions.Sum(c => c.Size));
    }

    [Fact]
    public async Task Irreducible_EmitsStartedThenTerminated_NoCompletedNoComposed_ThrowsException()
    {
        var messages = new List<ChatMessage> { new(ChatRole.System, "be helpful") };
        var policy = HarnessCompactionTestFixture.CreatePolicy(
            2, 1, 5, 1, new HarnessConstantSizeContextEstimator(5));

        var events = await RunAssemblyExpectingThrowCaughtAsync(
            messages,
            policy,
            HarnessScriptedUpstreamChatReducer.Echo(),
            new HarnessScriptedMessageClassifier(),
            baselineEntries => new HarnessMutableContextSnapshotProvider(baselineEntries));

        var started = Assert.Single(events.OfType<HarnessContextCompactionStartedEvent>());
        var terminated = Assert.Single(events.OfType<HarnessContextCompactionTerminatedEvent>());
        Assert.Empty(events.OfType<HarnessContextCompactionCompletedEvent>());
        Assert.Empty(events.OfType<HarnessContextComposedEvent>());

        Assert.Equal(HarnessContextCompactionOutcome.Irreducible, terminated.Diagnostics.Outcome);
        Assert.Null(terminated.Diagnostics.FinalSequenceValid);
        Assert.Empty(terminated.Diagnostics.CategoryContributions);
        Assert.True(started.SequenceNumber < terminated.SequenceNumber);

        // Per-assembly correlation: Started and Terminated for one terminated attempt share one
        // non-empty AssemblyId.
        Assert.NotEqual(Guid.Empty, started.AssemblyId);
        Assert.Equal(started.AssemblyId, terminated.AssemblyId);
    }

    [Fact]
    public async Task ConcurrentMutationLimit_EmitsStartedThenTerminated_NoCompletedNoComposed()
    {
        var messages = new List<ChatMessage> { new(ChatRole.System, "instructions") };
        var classifier = new HarnessScriptedMessageClassifier();
        var systemEntryId = classifier.ResolveEntryId(messages[0], 0, messages);
        var sizes = new Dictionary<string, int>
        {
            [systemEntryId] = 101,
            ["churn-1"] = 5,
            ["churn-2"] = 5,
        };
        var policy = HarnessCompactionTestFixture.CreatePolicy(100, 5, 1, 2, new HarnessFixedSizeContextEstimator(sizes));

        HarnessMutableContextSnapshotProvider? capturedProvider = null;
        var injectionCount = 0;
        var reducer = new HarnessScriptedUpstreamChatReducer((msgs, _) =>
        {
            injectionCount++;
            capturedProvider!.Inject(
                HarnessCompactionTestFixture.ConversationalEntry(
                    $"churn-{injectionCount}", ChatRole.User, $"churn message {injectionCount}"));
            return Task.FromResult(msgs);
        });

        var events = await RunAssemblyExpectingThrowCaughtAsync(
            messages,
            policy,
            reducer,
            classifier,
            baselineEntries =>
            {
                var provider = new HarnessMutableContextSnapshotProvider(baselineEntries);
                capturedProvider = provider;
                return provider;
            });

        var started = Assert.Single(events.OfType<HarnessContextCompactionStartedEvent>());
        var terminated = Assert.Single(events.OfType<HarnessContextCompactionTerminatedEvent>());
        Assert.Empty(events.OfType<HarnessContextCompactionCompletedEvent>());
        Assert.Empty(events.OfType<HarnessContextComposedEvent>());

        Assert.Equal(HarnessContextCompactionOutcome.ConcurrentMutationLimit, terminated.Diagnostics.Outcome);
        Assert.Null(terminated.Diagnostics.FinalSequenceValid);
        Assert.True(started.SequenceNumber < terminated.SequenceNumber);
    }

    [Fact]
    public async Task MeasurementUnit_NeverMislabeled_MatchesTheEstimatorThatGovernedThePolicy()
    {
        var messages = new List<ChatMessage> { new(ChatRole.System, "be helpful"), new(ChatRole.User, "hi") };

        var hostDefinedEvents = await RunAssemblyAsync(
            messages,
            HarnessCompactionTestFixture.CreatePolicy(1000, 500, 5, 3, new HarnessConstantSizeContextEstimator(1)),
            HarnessScriptedUpstreamChatReducer.Echo());
        Assert.All(
            hostDefinedEvents.OfType<HarnessContextCompactionStartedEvent>(),
            e => Assert.Equal(HarnessContextMeasurementUnit.HostDefinedUnits, e.MeasurementUnit));
        Assert.All(
            hostDefinedEvents.OfType<HarnessContextCompactionCompletedEvent>(),
            e => Assert.Equal(HarnessContextMeasurementUnit.HostDefinedUnits, e.Diagnostics.MeasurementUnit));

        var utf8Events = await RunAssemblyAsync(
            messages,
            HarnessCompactionTestFixture.CreatePolicy(1000, 500, 5, 3, new HarnessUtf8ContextSizeEstimator()),
            HarnessScriptedUpstreamChatReducer.Echo());
        Assert.All(
            utf8Events.OfType<HarnessContextCompactionStartedEvent>(),
            e => Assert.Equal(HarnessContextMeasurementUnit.Utf8Bytes, e.MeasurementUnit));
        Assert.All(
            utf8Events.OfType<HarnessContextCompactionCompletedEvent>(),
            e => Assert.Equal(HarnessContextMeasurementUnit.Utf8Bytes, e.Diagnostics.MeasurementUnit));
    }

    [Fact]
    public async Task AbsentProfile_NoContextCompactionEventsEmitted()
    {
        var function = AIFunctionFactory.Create(() => "tool-result", "G2Tool");
        using var services = HarnessCompositionTestFixture.CreateServices();
        var accessorCtx = new AgentExecutionContextAccessor();
        var binding = HarnessCompositionTestFixture.CaptureBinding(accessorCtx, out var scope);
        using (scope)
        {
            var (accessor, reporter, events) = CreateProgressHarness();
            var leaf = new HarnessScriptedChatClient(function.Name);
            var profile = HarnessCompositionTestFixture.CreateProfile(
                HarnessToolLoopOwner.Harness, HarnessTelemetryOwner.Harness);

            var request = HarnessCompositionTestFixture.CreateRequest(
                leaf,
                services,
                profile,
                HarnessCompositionTestFixture.CreateToolResolution(function),
                binding,
                accessorCtx,
                metrics: null,
                historyProvider: null,
                planningProviders: null,
                approvalPlugin: null,
                skillsPlugin: null,
                progressAccessor: accessor,
                webSearchPlugin: null,
                offloadPlugin: null,
                hybridProfile: null);

            using (accessor.BeginScope(reporter))
            {
                var result = new HarnessProviderComposition().Compose(request);
                Assert.Equal(HarnessProviderCompositionStatus.Success, result.Status);
                var agent = Assert.IsAssignableFrom<AIAgent>(result.Agent);
                Assert.Null(agent.GetService<HarnessHybridCompactionChatClient>());

                await agent.RunAsync("run", cancellationToken: TestContext.Current.CancellationToken);
            }

            Assert.Empty(events.OfType<HarnessContextCompactionStartedEvent>());
            Assert.Empty(events.OfType<HarnessContextCompactionCompletedEvent>());
            Assert.Empty(events.OfType<HarnessContextCompactionTerminatedEvent>());
            Assert.Empty(events.OfType<HarnessContextComposedEvent>());
        }
    }

    // ================================================================================
    // Parent correlation: child and grandchild reporters (root -> child -> grandchild),
    // exercising IProgressReporterContext.ParentAgentId exactly like the G4 artifact
    // offload/rehydration correlation tests.
    // ================================================================================

    [Fact]
    public async Task WithinLimit_RootChildAndGrandchildReporters_ContextEvents_CarryAgentIdParentAgentIdDepthAndSharedGlobalSequence()
    {
        var events = new List<IProgressEvent>();
        var accessor = new ProgressReporterAccessor();
        var rootReporter = new ProgressReporter(
            "context-observability-nested-wf",
            [new CollectorSink(events)],
            new ProgressSequenceProvider(),
            agentId: "root-agent");
        var childReporter = rootReporter.CreateChild("child-agent");
        var grandchildReporter = childReporter.CreateChild("grandchild-agent");

        var policy = HarnessCompactionTestFixture.CreatePolicy(
            1000, 500, 5, 3, new HarnessConstantSizeContextEstimator(1));
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, "be helpful"),
            new(ChatRole.User, "hello"),
        };

        await RunAssemblyAtReporterScopeAsync(accessor, rootReporter, policy, messages);
        await RunAssemblyAtReporterScopeAsync(accessor, childReporter, policy, messages);
        await RunAssemblyAtReporterScopeAsync(accessor, grandchildReporter, policy, messages);

        var startedEvents = events.OfType<HarnessContextCompactionStartedEvent>().ToList();
        var completedEvents = events.OfType<HarnessContextCompactionCompletedEvent>().ToList();
        var composedEvents = events.OfType<HarnessContextComposedEvent>().ToList();
        Assert.Equal(3, startedEvents.Count);
        Assert.Equal(3, completedEvents.Count);
        Assert.Equal(3, composedEvents.Count);
        Assert.Empty(events.OfType<HarnessContextCompactionTerminatedEvent>());

        AssertRootChildGrandchildCorrelation(startedEvents);
        AssertRootChildGrandchildCorrelation(completedEvents);
        AssertRootChildGrandchildCorrelation(composedEvents);

        // Nested correlation remains: each of the three independent, sequential attempts
        // (root/child/grandchild) keeps its own AssemblyId consistent across its own
        // Started/Completed/Composed, and no two of the three attempts ever share one.
        for (var i = 0; i < 3; i++)
        {
            Assert.Equal(startedEvents[i].AssemblyId, completedEvents[i].AssemblyId);
            Assert.Equal(startedEvents[i].AssemblyId, composedEvents[i].AssemblyId);
        }

        var distinctAssemblyIds = startedEvents.Select(e => e.AssemblyId).Distinct().ToList();
        Assert.Equal(3, distinctAssemblyIds.Count);
        Assert.DoesNotContain(Guid.Empty, distinctAssemblyIds);

        // Global sequence: every event across all three runs and all three event types shares one
        // monotonically increasing sequence, because every reporter (root/child/grandchild) shares
        // the same underlying ProgressSequenceProvider instance rather than each restarting its own
        // counter.
        var sequenceNumbers = events.Select(e => e.SequenceNumber).ToList();
        Assert.Equal(sequenceNumbers.OrderBy(s => s), sequenceNumbers);
        Assert.Equal(sequenceNumbers.Distinct().Count(), sequenceNumbers.Count);
        Assert.All(events, e => Assert.Equal(rootReporter.WorkflowId, e.WorkflowId));
    }

    [Fact]
    public async Task Irreducible_RootChildAndGrandchildReporters_TerminatedEvents_CarryAgentIdParentAgentIdDepthAndSharedGlobalSequence()
    {
        var events = new List<IProgressEvent>();
        var accessor = new ProgressReporterAccessor();
        var rootReporter = new ProgressReporter(
            "context-observability-nested-terminated-wf",
            [new CollectorSink(events)],
            new ProgressSequenceProvider(),
            agentId: "root-agent");
        var childReporter = rootReporter.CreateChild("child-agent");
        var grandchildReporter = childReporter.CreateChild("grandchild-agent");

        var policy = HarnessCompactionTestFixture.CreatePolicy(
            2, 1, 5, 1, new HarnessConstantSizeContextEstimator(5));
        var messages = new List<ChatMessage> { new(ChatRole.System, "be helpful") };

        await RunAssemblyAtReporterScopeExpectingIrreducibleAsync(accessor, rootReporter, policy, messages);
        await RunAssemblyAtReporterScopeExpectingIrreducibleAsync(accessor, childReporter, policy, messages);
        await RunAssemblyAtReporterScopeExpectingIrreducibleAsync(accessor, grandchildReporter, policy, messages);

        var startedEvents = events.OfType<HarnessContextCompactionStartedEvent>().ToList();
        var terminatedEvents = events.OfType<HarnessContextCompactionTerminatedEvent>().ToList();
        Assert.Equal(3, startedEvents.Count);
        Assert.Equal(3, terminatedEvents.Count);
        Assert.Empty(events.OfType<HarnessContextCompactionCompletedEvent>());
        Assert.Empty(events.OfType<HarnessContextComposedEvent>());

        AssertRootChildGrandchildCorrelation(startedEvents);
        AssertRootChildGrandchildCorrelation(terminatedEvents);

        // Nested correlation remains for terminated attempts too: each attempt's Started and
        // Terminated share one AssemblyId, and the three attempts never share one with each other.
        for (var i = 0; i < 3; i++)
        {
            Assert.Equal(startedEvents[i].AssemblyId, terminatedEvents[i].AssemblyId);
        }

        var distinctAssemblyIds = startedEvents.Select(e => e.AssemblyId).Distinct().ToList();
        Assert.Equal(3, distinctAssemblyIds.Count);
        Assert.DoesNotContain(Guid.Empty, distinctAssemblyIds);

        var sequenceNumbers = events.Select(e => e.SequenceNumber).ToList();
        Assert.Equal(sequenceNumbers.OrderBy(s => s), sequenceNumbers);
        Assert.Equal(sequenceNumbers.Distinct().Count(), sequenceNumbers.Count);
        Assert.All(events, e => Assert.Equal(rootReporter.WorkflowId, e.WorkflowId));
    }

    // ================================================================================
    // Concurrency: two overlapping provider calls on the same agent/workflow must remain
    // independently pairable via AssemblyId despite their SequenceNumbers interleaving.
    // ================================================================================

    [Fact]
    public async Task TwoConcurrentSameAgentAssemblies_ProduceTwoDistinctAssemblyIds_EachLifecyclePairsCorrectlyDespiteInterleavedSequence()
    {
        var classifier = new HarnessScriptedMessageClassifier();

        var call1Messages = new List<ChatMessage>
        {
            new(ChatRole.System, "instructions call1"),
            new(ChatRole.User, "old filler message call1"),
            new(ChatRole.User, "recent message call1"),
        };
        var call2Messages = new List<ChatMessage>
        {
            new(ChatRole.System, "instructions call2"),
            new(ChatRole.User, "old filler message call2"),
            new(ChatRole.User, "recent message call2"),
        };

        var sizes = new Dictionary<string, int>();
        foreach (var messages in new[] { call1Messages, call2Messages })
        {
            sizes[classifier.ResolveEntryId(messages[0], 0, messages)] = 30;
            sizes[classifier.ResolveEntryId(messages[1], 1, messages)] = 90;
            sizes[classifier.ResolveEntryId(messages[2], 2, messages)] = 20;
        }

        var policy = HarnessCompactionTestFixture.CreatePolicy(100, 10, 1, 2, new HarnessFixedSizeContextEstimator(sizes));

        // Both concurrent calls' reducer invocations rendezvous here before either is allowed to
        // return, guaranteeing that call 2's Started event is reported (which happens before its
        // reducer ever runs) while call 1 is still mid-attempt — i.e. genuinely interleaved
        // SequenceNumbers, not two assemblies that merely happen to run back-to-back.
        var reducerRendezvous = new Barrier(2);
        var reducer = new HarnessScriptedUpstreamChatReducer((msgs, _) =>
        {
            reducerRendezvous.SignalAndWait(TimeSpan.FromSeconds(10));
            return Task.FromResult(msgs.Where(m => m.Text?.Contains("old filler message") != true));
        });

        var (accessor, reporter, events) = CreateProgressHarness();
        var hybridProfile = HarnessHybridProfile.Create(
            policy,
            reducer,
            classifier,
            baselineEntries => new HarnessMutableContextSnapshotProvider(baselineEntries));

        var leaf = new HarnessCompactionObservingChatClient("unused");
        var accessorCtx = new AgentExecutionContextAccessor();
        var binding = HarnessCompositionTestFixture.CaptureBinding(accessorCtx, out var scope);
        using (scope)
        {
            var client = new HarnessHybridCompactionChatClient(
                leaf, hybridProfile, binding, accessorCtx, HarnessCompositionTestFixture.SessionId,
                runCoordinator: null, accessor);

            using (accessor.BeginScope(reporter))
            {
                var task1 = Task.Run(() => client.GetResponseAsync(
                    call1Messages, cancellationToken: TestContext.Current.CancellationToken));
                var task2 = Task.Run(() => client.GetResponseAsync(
                    call2Messages, cancellationToken: TestContext.Current.CancellationToken));

                await Task.WhenAll(task1, task2);
            }
        }

        var startedEvents = events.OfType<HarnessContextCompactionStartedEvent>().ToList();
        var completedEvents = events.OfType<HarnessContextCompactionCompletedEvent>().ToList();
        var composedEvents = events.OfType<HarnessContextComposedEvent>().ToList();
        Assert.Equal(2, startedEvents.Count);
        Assert.Equal(2, completedEvents.Count);
        Assert.Equal(2, composedEvents.Count);
        Assert.Empty(events.OfType<HarnessContextCompactionTerminatedEvent>());

        // Two distinct, non-empty AssemblyIds — one per concurrent attempt.
        var assemblyIds = startedEvents.Select(e => e.AssemblyId).ToList();
        Assert.Equal(2, assemblyIds.Distinct().Count());
        Assert.DoesNotContain(Guid.Empty, assemblyIds);

        // Each attempt's own Started/Completed/Composed trio shares its own AssemblyId.
        foreach (var assemblyId in assemblyIds)
        {
            var ownStarted = Assert.Single(startedEvents, e => e.AssemblyId == assemblyId);
            var ownCompleted = Assert.Single(completedEvents, e => e.AssemblyId == assemblyId);
            var ownComposed = Assert.Single(composedEvents, e => e.AssemblyId == assemblyId);

            Assert.True(ownStarted.SequenceNumber < ownCompleted.SequenceNumber);
            Assert.True(ownCompleted.SequenceNumber < ownComposed.SequenceNumber);
        }

        // Genuine interleaving, not two back-to-back attempts: the later-starting attempt's
        // Started event has a SequenceNumber that falls strictly between the earlier attempt's own
        // Started and Completed — proving the two lifecycles actually overlapped in time — while
        // each attempt's AssemblyId still correctly pairs its own three events despite that
        // interleaving.
        var orderedByStart = startedEvents.OrderBy(e => e.SequenceNumber).ToList();
        var earlierStarted = orderedByStart[0];
        var laterStarted = orderedByStart[1];
        var earlierCompleted = Assert.Single(completedEvents, e => e.AssemblyId == earlierStarted.AssemblyId);

        Assert.True(earlierStarted.SequenceNumber < laterStarted.SequenceNumber);
        Assert.True(laterStarted.SequenceNumber < earlierCompleted.SequenceNumber);
    }

    private static void AssertRootChildGrandchildCorrelation(IReadOnlyList<IProgressEvent> orderedEvents)
    {
        Assert.Equal(3, orderedEvents.Count);

        var rootEvent = orderedEvents[0];
        Assert.Equal("root-agent", rootEvent.AgentId);
        Assert.Null(rootEvent.ParentAgentId);
        Assert.Equal(0, rootEvent.Depth);

        var childEvent = orderedEvents[1];
        Assert.Equal("child-agent", childEvent.AgentId);
        Assert.Equal("root-agent", childEvent.ParentAgentId);
        Assert.Equal(1, childEvent.Depth);

        var grandchildEvent = orderedEvents[2];
        Assert.Equal("grandchild-agent", grandchildEvent.AgentId);
        Assert.Equal("child-agent", grandchildEvent.ParentAgentId);
        Assert.Equal(2, grandchildEvent.Depth);

        Assert.True(rootEvent.SequenceNumber < childEvent.SequenceNumber);
        Assert.True(childEvent.SequenceNumber < grandchildEvent.SequenceNumber);
    }

    // ================================================================================
    // Binding-revalidation ordering: Completed is reported before the post-assembly
    // EnsureCurrent trust revalidation. A binding invalidated in that window must still leave
    // the already-successful decision observable, while Composed — "ready for dispatch" — is
    // never reached, and this is not itself an Irreducible/ConcurrentMutationLimit termination,
    // so Terminated is never emitted either: InvalidOperationException propagates directly.
    // ================================================================================

    [Fact]
    public async Task BindingInvalidatedAfterSuccessfulAssembly_CompletedEmitted_ComposedNeverEmitted_InvalidOperationExceptionPropagates()
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, "be helpful"),
            new(ChatRole.User, "hello"),
        };
        var policy = HarnessCompactionTestFixture.CreatePolicy(
            1000, 500, 5, 3, new HarnessConstantSizeContextEstimator(1));

        var (accessor, reporter, events) = CreateProgressHarness();
        var hybridProfile = HarnessHybridProfile.Create(
            policy,
            HarnessScriptedUpstreamChatReducer.Echo(),
            new HarnessScriptedMessageClassifier(),
            baselineEntries => new HarnessMutableContextSnapshotProvider(baselineEntries));

        var leaf = new HarnessCompactionObservingChatClient("unused");
        var accessorCtx = new AgentExecutionContextAccessor();
        var binding = HarnessCompositionTestFixture.CaptureBinding(accessorCtx, out var scope);
        using (scope)
        {
            // Observes the trusted execution context as valid for the entry-point EnsureCurrent
            // check (the first read), then as invalidated — context lost — for every subsequent
            // read. This simulates the binding being invalidated by other activity in the window
            // between the successful assembly decision and the post-assembly trust revalidation,
            // without requiring an actual concurrent mutation.
            var invalidatingAccessor = new HarnessInvalidateAfterReadsExecutionContextAccessor(
                accessorCtx, validReads: 1);

            var client = new HarnessHybridCompactionChatClient(
                leaf, hybridProfile, binding, invalidatingAccessor, HarnessCompositionTestFixture.SessionId,
                runCoordinator: null, accessor);

            using (accessor.BeginScope(reporter))
            {
                await Assert.ThrowsAsync<InvalidOperationException>(
                    () => client.GetResponseAsync(
                        messages, cancellationToken: TestContext.Current.CancellationToken));
            }
        }

        var started = Assert.Single(events.OfType<HarnessContextCompactionStartedEvent>());
        var completed = Assert.Single(events.OfType<HarnessContextCompactionCompletedEvent>());
        Assert.Empty(events.OfType<HarnessContextComposedEvent>());
        Assert.Empty(events.OfType<HarnessContextCompactionTerminatedEvent>());

        Assert.Equal(HarnessContextCompactionOutcome.WithinLimit, completed.Diagnostics.Outcome);
        Assert.True(started.SequenceNumber < completed.SequenceNumber);
    }

    // ================================================================================
    // Privacy: no raw message text anywhere in any emitted event's string properties
    // ================================================================================

    [Fact]
    public async Task Events_NeverContainRawMessageText()
    {
        var uniqueMarker = "UNIQUE-CONTEXT-MARKER-" + Guid.NewGuid().ToString("N");
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, "instructions"),
            new(ChatRole.User, uniqueMarker + " an old message that must never leak"),
            new(ChatRole.User, "recent message"),
        };
        var classifier = new HarnessScriptedMessageClassifier();
        var sizes = new Dictionary<string, int>
        {
            [classifier.ResolveEntryId(messages[0], 0, messages)] = 30,
            [classifier.ResolveEntryId(messages[1], 1, messages)] = 90,
            [classifier.ResolveEntryId(messages[2], 2, messages)] = 20,
        };
        var policy = HarnessCompactionTestFixture.CreatePolicy(100, 10, 1, 2, new HarnessFixedSizeContextEstimator(sizes));
        var reducer = new HarnessScriptedUpstreamChatReducer(
            (msgs, _) => Task.FromResult(msgs.Where(m => m.Text?.Contains(uniqueMarker) != true)));

        var events = await RunAssemblyAsync(messages, policy, reducer, classifier);

        Assert.NotEmpty(events);
        foreach (var progressEvent in events)
        {
            var stringValues = new List<string>();
            CollectStringPropertyValues(progressEvent, stringValues, depth: 0);
            Assert.All(stringValues, value => Assert.DoesNotContain(uniqueMarker, value, StringComparison.Ordinal));
            Assert.DoesNotContain(uniqueMarker, progressEvent.ToString(), StringComparison.Ordinal);
        }
    }

    // ================================================================================
    // Started-event gate: classifier/snapshot failure must not emit a dangling Started
    // ================================================================================

    [Fact]
    public async Task ClassifierException_BeforeStarted_EmitsNoEvents()
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, "be helpful"),
            new(ChatRole.User, "hello"),
        };
        var policy = HarnessCompactionTestFixture.CreatePolicy(
            1000, 500, 5, 3, new HarnessConstantSizeContextEstimator(1));

        var (accessor, reporter, events) = CreateProgressHarness();
        var throwingClassifier = new HarnessThrowingMessageClassifier();
        var hybridProfile = HarnessHybridProfile.Create(
            policy,
            HarnessScriptedUpstreamChatReducer.Echo(),
            throwingClassifier,
            baselineEntries => new HarnessMutableContextSnapshotProvider(baselineEntries));

        var leaf = new HarnessCompactionObservingChatClient("unused");
        var accessorCtx = new AgentExecutionContextAccessor();
        var binding = HarnessCompositionTestFixture.CaptureBinding(accessorCtx, out var scope);
        using (scope)
        {
            var client = new HarnessHybridCompactionChatClient(
                leaf, hybridProfile, binding, accessorCtx, HarnessCompositionTestFixture.SessionId,
                runCoordinator: null, accessor);

            using (accessor.BeginScope(reporter))
            {
                await Assert.ThrowsAsync<InvalidOperationException>(
                    () => client.GetResponseAsync(
                        messages, cancellationToken: TestContext.Current.CancellationToken));
            }
        }

        // A classifier exception before the assembler is constructed must never emit
        // a dangling Started event — no assembly was ever started.
        Assert.Empty(events.OfType<HarnessContextCompactionStartedEvent>());
        Assert.Empty(events.OfType<HarnessContextCompactionCompletedEvent>());
        Assert.Empty(events.OfType<HarnessContextCompactionTerminatedEvent>());
        Assert.Empty(events.OfType<HarnessContextComposedEvent>());
    }

    [Fact]
    public async Task SnapshotIntegrationReturnsNull_BeforeStarted_EmitsNoEvents()
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, "be helpful"),
        };
        var policy = HarnessCompactionTestFixture.CreatePolicy(
            1000, 500, 5, 3, new HarnessConstantSizeContextEstimator(1));

        var (accessor, reporter, events) = CreateProgressHarness();
        var hybridProfile = HarnessHybridProfile.Create(
            policy,
            HarnessScriptedUpstreamChatReducer.Echo(),
            new HarnessScriptedMessageClassifier(),
            baselineEntries => null!); // null snapshot integration

        var leaf = new HarnessCompactionObservingChatClient("unused");
        var accessorCtx = new AgentExecutionContextAccessor();
        var binding = HarnessCompositionTestFixture.CaptureBinding(accessorCtx, out var scope);
        using (scope)
        {
            var client = new HarnessHybridCompactionChatClient(
                leaf, hybridProfile, binding, accessorCtx, HarnessCompositionTestFixture.SessionId,
                runCoordinator: null, accessor);

            using (accessor.BeginScope(reporter))
            {
                await Assert.ThrowsAsync<InvalidOperationException>(
                    () => client.GetResponseAsync(
                        messages, cancellationToken: TestContext.Current.CancellationToken));
            }
        }

        // A null snapshot-integration result before assembler construction must not emit
        // any progress event — no assembly was ever started.
        Assert.Empty(events.OfType<HarnessContextCompactionStartedEvent>());
        Assert.Empty(events.OfType<HarnessContextCompactionCompletedEvent>());
        Assert.Empty(events.OfType<HarnessContextCompactionTerminatedEvent>());
        Assert.Empty(events.OfType<HarnessContextComposedEvent>());
    }

    // ================================================================================
    // Helpers
    // ================================================================================

    private static async Task RunAssemblyAtReporterScopeAsync(
        IProgressReporterAccessor accessor,
        IProgressReporter reporter,
        HarnessHybridContextPolicy policy,
        List<ChatMessage> messages)
    {
        var hybridProfile = HarnessHybridProfile.Create(
            policy,
            HarnessScriptedUpstreamChatReducer.Echo(),
            new HarnessScriptedMessageClassifier(),
            baselineEntries => new HarnessMutableContextSnapshotProvider(baselineEntries));

        var leaf = new HarnessCompactionObservingChatClient("unused");
        var accessorCtx = new AgentExecutionContextAccessor();
        var binding = HarnessCompositionTestFixture.CaptureBinding(accessorCtx, out var scope);
        using (scope)
        {
            var client = new HarnessHybridCompactionChatClient(
                leaf, hybridProfile, binding, accessorCtx, HarnessCompositionTestFixture.SessionId,
                runCoordinator: null, accessor);

            using (accessor.BeginScope(reporter))
            {
                await client.GetResponseAsync(messages, cancellationToken: TestContext.Current.CancellationToken);
            }
        }
    }

    private static async Task RunAssemblyAtReporterScopeExpectingIrreducibleAsync(
        IProgressReporterAccessor accessor,
        IProgressReporter reporter,
        HarnessHybridContextPolicy policy,
        List<ChatMessage> messages)
    {
        var hybridProfile = HarnessHybridProfile.Create(
            policy,
            HarnessScriptedUpstreamChatReducer.Echo(),
            new HarnessScriptedMessageClassifier(),
            baselineEntries => new HarnessMutableContextSnapshotProvider(baselineEntries));

        var leaf = new HarnessCompactionObservingChatClient("unused");
        var accessorCtx = new AgentExecutionContextAccessor();
        var binding = HarnessCompositionTestFixture.CaptureBinding(accessorCtx, out var scope);
        using (scope)
        {
            var client = new HarnessHybridCompactionChatClient(
                leaf, hybridProfile, binding, accessorCtx, HarnessCompositionTestFixture.SessionId,
                runCoordinator: null, accessor);

            using (accessor.BeginScope(reporter))
            {
                await Assert.ThrowsAsync<HarnessCompactionIrreducibleException>(
                    () => client.GetResponseAsync(
                        messages, cancellationToken: TestContext.Current.CancellationToken));
            }
        }
    }

    private static async Task<List<IProgressEvent>> RunAssemblyAsync(
        List<ChatMessage> messages,
        HarnessHybridContextPolicy policy,
        IChatReducer reducer,
        HarnessScriptedMessageClassifier? classifier = null)
    {
        var (accessor, reporter, events) = CreateProgressHarness();
        var effectiveClassifier = classifier ?? new HarnessScriptedMessageClassifier();
        var hybridProfile = HarnessHybridProfile.Create(
            policy,
            reducer,
            effectiveClassifier,
            baselineEntries => new HarnessMutableContextSnapshotProvider(baselineEntries));

        var leaf = new HarnessCompactionObservingChatClient("unused");
        var accessorCtx = new AgentExecutionContextAccessor();
        var binding = HarnessCompositionTestFixture.CaptureBinding(accessorCtx, out var scope);
        using (scope)
        {
            var client = new HarnessHybridCompactionChatClient(
                leaf, hybridProfile, binding, accessorCtx, HarnessCompositionTestFixture.SessionId,
                runCoordinator: null, accessor);

            using (accessor.BeginScope(reporter))
            {
                await client.GetResponseAsync(messages, cancellationToken: TestContext.Current.CancellationToken);
            }
        }

        return events;
    }

    private static async Task<List<IProgressEvent>> RunAssemblyExpectingThrowCaughtAsync(
        List<ChatMessage> messages,
        HarnessHybridContextPolicy policy,
        IChatReducer reducer,
        HarnessScriptedMessageClassifier classifier,
        HarnessContextSnapshotIntegration snapshotIntegration)
    {
        var (accessor, reporter, events) = CreateProgressHarness();
        var hybridProfile = HarnessHybridProfile.Create(policy, reducer, classifier, snapshotIntegration);

        var leaf = new HarnessCompactionObservingChatClient("unused");
        var accessorCtx = new AgentExecutionContextAccessor();
        var binding = HarnessCompositionTestFixture.CaptureBinding(accessorCtx, out var scope);
        using (scope)
        {
            var client = new HarnessHybridCompactionChatClient(
                leaf, hybridProfile, binding, accessorCtx, HarnessCompositionTestFixture.SessionId,
                runCoordinator: null, accessor);

            using (accessor.BeginScope(reporter))
            {
                await Assert.ThrowsAsync<HarnessCompactionIrreducibleException>(
                    () => client.GetResponseAsync(
                        messages, cancellationToken: TestContext.Current.CancellationToken));
            }
        }

        return events;
    }

    private static (IProgressReporterAccessor Accessor, IProgressReporter Reporter, List<IProgressEvent> Events) CreateProgressHarness()
    {
        var events = new List<IProgressEvent>();
        var accessor = new ProgressReporterAccessor();
        var reporter = new ProgressReporter(
            "context-observability-wf",
            [new CollectorSink(events)],
            new ProgressSequenceProvider());
        return (accessor, reporter, events);
    }

    private static void CollectStringPropertyValues(object? instance, List<string> into, int depth)
    {
        if (instance is null || depth > 3)
        {
            return;
        }

        var type = instance.GetType();
        if (type.Namespace is null || !type.Namespace.StartsWith("NexusLabs.Foundry", StringComparison.Ordinal))
        {
            return;
        }

        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            object? value;
            try
            {
                value = property.GetValue(instance);
            }
            catch (TargetInvocationException)
            {
                continue;
            }

            if (value is string stringValue)
            {
                into.Add(stringValue);
            }
            else if (value is System.Collections.IEnumerable enumerable and not string)
            {
                foreach (var item in enumerable)
                {
                    if (item?.GetType().Namespace?.StartsWith("NexusLabs.Foundry", StringComparison.Ordinal) == true)
                    {
                        CollectStringPropertyValues(item, into, depth + 1);
                    }
                }
            }
            else if (value is not null && value.GetType().Namespace?.StartsWith("NexusLabs.Foundry", StringComparison.Ordinal) == true)
            {
                CollectStringPropertyValues(value, into, depth + 1);
            }
        }
    }

    private sealed class CollectorSink(List<IProgressEvent> events) : IProgressSink
    {
        // Genuinely concurrent provider calls (see
        // TwoConcurrentSameAgentAssemblies_...) invoke Report from multiple threads at once, and
        // ProgressReporter.Report dispatches to sinks synchronously on whichever thread called it —
        // it does not itself serialize concurrent calls into a single sink. A plain List<T>.Add is
        // not thread-safe, so without this lock, concurrent adds can silently lose events (no
        // exception, just a shorter list), which is a test-harness race, not a product defect. The
        // lock only ever guards this in-memory test collector, never anything under test.
        private readonly object gate = new();

        public ValueTask OnEventAsync(IProgressEvent progressEvent, CancellationToken cancellationToken)
        {
            lock (gate)
            {
                events.Add(progressEvent);
            }

            return ValueTask.CompletedTask;
        }
    }

    /// <summary>
    /// Test-only classifier that throws <see cref="InvalidOperationException"/> from
    /// <see cref="ResolveEntryId"/>, simulating a classifier failure before the assembler is
    /// ever constructed — used to verify no dangling Started event is emitted.
    /// </summary>
    private sealed class HarnessThrowingMessageClassifier : IHarnessContextMessageClassifier
    {
        public string ResolveEntryId(
            ChatMessage message, int index, IReadOnlyList<ChatMessage> allMessages) =>
            throw new InvalidOperationException("Simulated classifier failure.");

        public HarnessContextEntryKind? ClassifyOverride(
            ChatMessage message, int index, IReadOnlyList<ChatMessage> allMessages) => null;
    }

    /// <summary>
    /// Test-only <see cref="IAgentExecutionContextAccessor"/> wrapper that observes the inner
    /// accessor's real <see cref="IAgentExecutionContextAccessor.Current"/> for the first
    /// <paramref name="validReads"/> reads, then observes <see langword="null"/> — as if the
    /// trusted context had been lost/invalidated — for every subsequent read. Used to simulate the
    /// execution binding being invalidated between the entry-point <c>EnsureCurrent</c> check and
    /// the post-assembly trust revalidation inside <c>HarnessHybridCompactionChatClient</c>, without
    /// requiring an actual concurrent mutation.
    /// </summary>
    private sealed class HarnessInvalidateAfterReadsExecutionContextAccessor(
        IAgentExecutionContextAccessor inner, int validReads) : IAgentExecutionContextAccessor
    {
        private int _reads;

        public IAgentExecutionContext? Current
        {
            get
            {
                _reads++;
                return _reads <= validReads ? inner.Current : null;
            }
        }

        public IDisposable BeginScope(IAgentExecutionContext context) => inner.BeginScope(context);
    }
}
