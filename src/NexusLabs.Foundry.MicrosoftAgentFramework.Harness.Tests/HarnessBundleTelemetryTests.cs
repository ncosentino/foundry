using System.Collections.Concurrent;
using System.Diagnostics;

using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Bundle;
using NexusLabs.Foundry.MicrosoftAgentFramework.Progress;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Tests;

/// <summary>
/// Proves that the optional bundle keeps one upstream function loop and OpenTelemetry owner while
/// adding ordered Foundry progress without duplicate agent, model, or tool records.
/// </summary>
public sealed class HarnessBundleTelemetryTests
{
    private static readonly FoundryHarnessAgentFactory Factory = new();

    [Fact]
    public async Task Run_OneToolRound_EmitsSingleUpstreamActivitySet()
    {
        int invocationCount = 0;
        var function = AIFunctionFactory.Create(
            () =>
            {
                Interlocked.Increment(ref invocationCount);
                return "tool-result";
            },
            name: "bundle_tool");
        var chatClient = new HarnessBundleToolCallChatClient(
            function.Name,
            new Dictionary<string, object?>());
        string sourceName = $"Foundry.Harness.Tests.{Guid.NewGuid():N}";
        var activities = new ConcurrentQueue<Activity>();
        using var listener = CreateActivityListener(sourceName, activities);
        var configuration = CreateTelemetryConfiguration(
            chatClient,
            function,
            sourceName,
            progressAccessor: null);
        var agent = Factory.Create(configuration);

        await agent.RunAsync(
            "run one tool",
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(1, invocationCount);
        Assert.Equal(2, chatClient.CallCount);
        Assert.NotNull(agent.GetService<FunctionInvokingChatClient>());
        AssertActivityCounts(activities);
    }

    [Fact]
    public async Task Run_FoundryProgressEnabled_EmitsOrderedProgressWithoutDuplicatingActivities()
    {
        int invocationCount = 0;
        var function = AIFunctionFactory.Create(
            () =>
            {
                Interlocked.Increment(ref invocationCount);
                return "tool-result";
            },
            name: "bundle_tool");
        var chatClient = new HarnessBundleToolCallChatClient(
            function.Name,
            new Dictionary<string, object?>());
        string sourceName = $"Foundry.Harness.Tests.{Guid.NewGuid():N}";
        var activities = new ConcurrentQueue<Activity>();
        using var listener = CreateActivityListener(sourceName, activities);
        var sink = new HarnessBundleProgressSink();
        using var services = new ServiceCollection()
            .AddFoundryAgentFramework()
            .BuildServiceProvider();
        var progressFactory = services.GetRequiredService<IProgressReporterFactory>();
        var progressAccessor = services.GetRequiredService<IProgressReporterAccessor>();
        var reporter = progressFactory.Create("bundle-workflow", [sink]);
        var configuration = CreateTelemetryConfiguration(
            chatClient,
            function,
            sourceName,
            progressAccessor);
        var agent = Factory.Create(configuration);

        using (progressAccessor.BeginScope(reporter))
        {
            await agent.RunAsync(
                "run one tool",
                cancellationToken: TestContext.Current.CancellationToken);
        }

        Assert.Equal(1, invocationCount);
        Assert.Equal(2, chatClient.CallCount);
        AssertActivityCounts(activities);
        Assert.NotNull(agent.GetService<FunctionInvokingChatClient>());
        Assert.NotNull(agent.GetService<OpenTelemetryChatClient>());
        Assert.NotNull(agent.GetService<OpenTelemetryAgent>());
        Assert.Equal(
            [
                typeof(AgentInvokedEvent),
                typeof(LlmCallStartedEvent),
                typeof(LlmCallCompletedEvent),
                typeof(ToolCallStartedEvent),
                typeof(ToolCallCompletedEvent),
                typeof(LlmCallStartedEvent),
                typeof(LlmCallCompletedEvent),
                typeof(AgentCompletedEvent),
            ],
            sink.Events.Select(progressEvent => progressEvent.GetType()));
        Assert.All(
            sink.Events,
            progressEvent =>
            {
                Assert.Equal("bundle-workflow", progressEvent.WorkflowId);
                Assert.Equal("bundle-agent-id", progressEvent.AgentId);
                Assert.Null(progressEvent.ParentAgentId);
                Assert.Equal(1, progressEvent.Depth);
            });
        var sequenceNumbers = sink.Events
            .Select(progressEvent => progressEvent.SequenceNumber)
            .ToArray();
        for (int index = 1; index < sequenceNumbers.Length; index++)
        {
            Assert.True(sequenceNumbers[index] > sequenceNumbers[index - 1]);
        }
        var completed = Assert.IsType<AgentCompletedEvent>(sink.Events[^1]);
        Assert.Equal(33, completed.TotalTokens);
        Assert.Equal(30, completed.InputTokens);
        Assert.Equal(3, completed.OutputTokens);
        Assert.Equal(1, completed.ToolCallCount);
    }

    [Fact]
    public async Task Run_FoundryProgressEnabledWithoutActiveScope_EmitsNoProgress()
    {
        var function = AIFunctionFactory.Create(
            () => "tool-result",
            name: "bundle_tool");
        var chatClient = new HarnessBundleToolCallChatClient(
            function.Name,
            new Dictionary<string, object?>());
        var sink = new HarnessBundleProgressSink();
        using var services = new ServiceCollection()
            .AddFoundryAgentFramework()
            .AddSingleton<IProgressSink>(sink)
            .BuildServiceProvider();
        var progressAccessor = services.GetRequiredService<IProgressReporterAccessor>();
        var configuration = CreateTelemetryConfiguration(
            chatClient,
            function,
            sourceName: null,
            progressAccessor) with
        {
            Features = HarnessBundleTestsHelpers.AllFeaturesDisabled(),
        };
        var agent = Factory.Create(configuration);

        await agent.RunAsync(
            "run one tool",
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(2, chatClient.CallCount);
        Assert.Empty(sink.Events);
    }

    [Fact]
    public async Task Run_FoundryProgressDisabledWithActiveScope_EmitsNoProgress()
    {
        var function = AIFunctionFactory.Create(
            () => "tool-result",
            name: "bundle_tool");
        var chatClient = new HarnessBundleToolCallChatClient(
            function.Name,
            new Dictionary<string, object?>());
        var sink = new HarnessBundleProgressSink();
        using var services = new ServiceCollection()
            .AddFoundryAgentFramework()
            .BuildServiceProvider();
        var progressFactory = services.GetRequiredService<IProgressReporterFactory>();
        var progressAccessor = services.GetRequiredService<IProgressReporterAccessor>();
        var reporter = progressFactory.Create("bundle-workflow", [sink]);
        var configuration = CreateTelemetryConfiguration(
            chatClient,
            function,
            sourceName: null,
            progressAccessor: null) with
        {
            Features = HarnessBundleTestsHelpers.AllFeaturesDisabled(),
        };
        var agent = Factory.Create(configuration);

        using (progressAccessor.BeginScope(reporter))
        {
            await agent.RunAsync(
                "run one tool",
                cancellationToken: TestContext.Current.CancellationToken);
        }

        Assert.Equal(2, chatClient.CallCount);
        Assert.Empty(sink.Events);
    }

    [Fact]
    public void DescribeEffectiveDefaults_ProgressAccessorSupplied_ReturnsReport()
    {
        using var services = new ServiceCollection()
            .AddFoundryAgentFramework()
            .BuildServiceProvider();
        var progressAccessor = services.GetRequiredService<IProgressReporterAccessor>();
        var configuration = HarnessBundleTestsHelpers.CreateBaseline() with
        {
            ProgressAccessor = progressAccessor,
        };

        var defaults = Factory.DescribeEffectiveDefaults(configuration);

        Assert.NotEmpty(defaults.Dispositions);
    }

    [Fact]
    public async Task RunStreaming_FoundryProgressEnabled_EmitsLifecycleAndTokenProgress()
    {
        var chatClient = new HarnessBundleStreamingChatClient();
        var sink = new HarnessBundleProgressSink();
        using var services = new ServiceCollection()
            .AddFoundryAgentFramework()
            .BuildServiceProvider();
        var progressFactory = services.GetRequiredService<IProgressReporterFactory>();
        var progressAccessor = services.GetRequiredService<IProgressReporterAccessor>();
        var reporter = progressFactory.Create("bundle-stream-workflow", [sink]);
        var configuration = HarnessBundleTestsHelpers.CreateBaseline() with
        {
            Id = "bundle-stream-agent",
            Name = "Bundle Stream Agent",
            ChatClient = chatClient,
            ProgressAccessor = progressAccessor,
        };
        var agent = Factory.Create(configuration);
        var updates = new List<AgentResponseUpdate>();

        using (progressAccessor.BeginScope(reporter))
        {
            await foreach (var update in agent
                .RunStreamingAsync(
                    "stream",
                    cancellationToken: TestContext.Current.CancellationToken))
            {
                updates.Add(update);
            }
        }

        Assert.Contains(
            updates.SelectMany(update => update.Contents).OfType<TextContent>(),
            content => content.Text == "streamed");
        Assert.Equal(
            [
                typeof(AgentInvokedEvent),
                typeof(LlmCallStartedEvent),
                typeof(LlmCallCompletedEvent),
                typeof(AgentCompletedEvent),
            ],
            sink.Events.Select(progressEvent => progressEvent.GetType()));
        var llmCompleted = Assert.IsType<LlmCallCompletedEvent>(sink.Events[2]);
        Assert.Equal(10, llmCompleted.InputTokens);
        Assert.Equal(5, llmCompleted.OutputTokens);
        Assert.Equal(15, llmCompleted.TotalTokens);
        var agentCompleted = Assert.IsType<AgentCompletedEvent>(sink.Events[3]);
        Assert.Equal(15, agentCompleted.TotalTokens);
        Assert.Equal(0, agentCompleted.ToolCallCount);
    }

    [Fact]
    public async Task RunStreaming_EarlyDisposal_EmitsLlmAndAgentFailureProgress()
    {
        var chatClient = new HarnessBundleStreamingChatClient();
        var sink = new HarnessBundleProgressSink();
        using var services = new ServiceCollection()
            .AddFoundryAgentFramework()
            .BuildServiceProvider();
        var progressFactory = services.GetRequiredService<IProgressReporterFactory>();
        var progressAccessor = services.GetRequiredService<IProgressReporterAccessor>();
        var reporter = progressFactory.Create("bundle-abandoned-stream-workflow", [sink]);
        var configuration = HarnessBundleTestsHelpers.CreateBaseline() with
        {
            Id = "bundle-abandoned-stream-agent",
            Name = "Bundle Abandoned Stream Agent",
            ChatClient = chatClient,
            ProgressAccessor = progressAccessor,
        };
        var agent = Factory.Create(configuration);

        using (progressAccessor.BeginScope(reporter))
        {
            var enumerator = agent
                .RunStreamingAsync(
                    "stream",
                    cancellationToken: TestContext.Current.CancellationToken)
                .GetAsyncEnumerator(TestContext.Current.CancellationToken);
            Assert.True(await enumerator.MoveNextAsync());
            await enumerator.DisposeAsync();
        }

        Assert.Equal(
            [
                typeof(AgentInvokedEvent),
                typeof(LlmCallStartedEvent),
                typeof(LlmCallFailedEvent),
                typeof(AgentFailedEvent),
            ],
            sink.Events.Select(progressEvent => progressEvent.GetType()));
        var llmFailed = Assert.IsType<LlmCallFailedEvent>(sink.Events[2]);
        Assert.Equal(
            FoundryHarnessProgressChatClient.AbandonedStreamingErrorMessage,
            llmFailed.ErrorMessage);
        var agentFailed = Assert.IsType<AgentFailedEvent>(sink.Events[3]);
        Assert.Equal(
            FoundryHarnessProgressAgent.AbandonedStreamingErrorMessage,
            agentFailed.ErrorMessage);
    }

    [Fact]
    public async Task Run_ModelFailure_EmitsLlmAndAgentFailureProgress()
    {
        var chatClient = new HarnessBundleFailingChatClient(
            new InvalidOperationException("model failed"));
        var sink = new HarnessBundleProgressSink();
        using var services = new ServiceCollection()
            .AddFoundryAgentFramework()
            .BuildServiceProvider();
        var progressFactory = services.GetRequiredService<IProgressReporterFactory>();
        var progressAccessor = services.GetRequiredService<IProgressReporterAccessor>();
        var reporter = progressFactory.Create("bundle-failure-workflow", [sink]);
        var configuration = HarnessBundleTestsHelpers.CreateBaseline() with
        {
            Id = "bundle-failure-agent",
            Name = "Bundle Failure Agent",
            ChatClient = chatClient,
            ProgressAccessor = progressAccessor,
        };
        var agent = Factory.Create(configuration);

        using (progressAccessor.BeginScope(reporter))
        {
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => agent.RunAsync(
                    "fail",
                    cancellationToken: TestContext.Current.CancellationToken));
            Assert.Equal("model failed", exception.Message);
        }

        Assert.Equal(
            [
                typeof(AgentInvokedEvent),
                typeof(LlmCallStartedEvent),
                typeof(LlmCallFailedEvent),
                typeof(AgentFailedEvent),
            ],
            sink.Events.Select(progressEvent => progressEvent.GetType()));
    }

    [Fact]
    public async Task Run_ToolFailure_EmitsToolFailureProgressOnce()
    {
        var function = AIFunctionFactory.Create(
            new Func<string>(() => throw new InvalidOperationException("tool failed")),
            name: "failing_bundle_tool");
        var chatClient = new HarnessBundleToolCallChatClient(
            function.Name,
            new Dictionary<string, object?>());
        var sink = new HarnessBundleProgressSink();
        using var services = new ServiceCollection()
            .AddFoundryAgentFramework()
            .BuildServiceProvider();
        var progressFactory = services.GetRequiredService<IProgressReporterFactory>();
        var progressAccessor = services.GetRequiredService<IProgressReporterAccessor>();
        var reporter = progressFactory.Create("bundle-tool-failure-workflow", [sink]);
        var configuration = HarnessBundleTestsHelpers.CreateBaseline() with
        {
            Id = "bundle-tool-failure-agent",
            Name = "Bundle Tool Failure Agent",
            ChatClient = chatClient,
            Tools = [function],
            ProgressAccessor = progressAccessor,
        };
        var agent = Factory.Create(configuration);

        using (progressAccessor.BeginScope(reporter))
        {
            await agent.RunAsync(
                "run the failing tool",
                cancellationToken: TestContext.Current.CancellationToken);
        }

        Assert.Single(sink.Events.OfType<ToolCallStartedEvent>());
        var failed = Assert.Single(sink.Events.OfType<ToolCallFailedEvent>());
        Assert.Equal("failing_bundle_tool", failed.ToolName);
        Assert.Contains("tool failed", failed.ErrorMessage, StringComparison.Ordinal);
        Assert.Empty(sink.Events.OfType<ToolCallCompletedEvent>());
        Assert.Single(sink.Events.OfType<AgentCompletedEvent>());
    }

    [Fact]
    public async Task Run_ConcurrentScopes_OnOneAgent_DoNotCrossProgressEvents()
    {
        var chatClient = new HarnessBundleConcurrentChatClient();
        var firstSink = new HarnessBundleProgressSink();
        var secondSink = new HarnessBundleProgressSink();
        using var services = new ServiceCollection()
            .AddFoundryAgentFramework()
            .BuildServiceProvider();
        var progressFactory = services.GetRequiredService<IProgressReporterFactory>();
        var progressAccessor = services.GetRequiredService<IProgressReporterAccessor>();
        var firstReporter = progressFactory.Create("bundle-concurrent-a", [firstSink]);
        var secondReporter = progressFactory.Create("bundle-concurrent-b", [secondSink]);
        var configuration = HarnessBundleTestsHelpers.CreateBaseline() with
        {
            Id = "bundle-concurrent-agent",
            Name = "Bundle Concurrent Agent",
            ChatClient = chatClient,
            ProgressAccessor = progressAccessor,
        };
        var agent = Factory.Create(configuration);

        await Task.WhenAll(
            RunWithProgressScopeAsync(firstReporter, "first"),
            RunWithProgressScopeAsync(secondReporter, "second"));

        AssertConcurrentRunEvents(firstSink.Events, "bundle-concurrent-a");
        AssertConcurrentRunEvents(secondSink.Events, "bundle-concurrent-b");

        async Task RunWithProgressScopeAsync(
            IProgressReporter reporter,
            string message)
        {
            using (progressAccessor.BeginScope(reporter))
            {
                await agent.RunAsync(
                    message,
                    cancellationToken: TestContext.Current.CancellationToken);
            }
        }
    }

    private static FoundryHarnessAgentConfiguration CreateTelemetryConfiguration(
        IChatClient chatClient,
        AIFunction function,
        string? sourceName,
        IProgressReporterAccessor? progressAccessor) =>
        HarnessBundleTestsHelpers.CreateBaseline(
            HarnessBundleTestsHelpers.AllFeaturesDisabled() with
            {
                EnableOpenTelemetry = sourceName is not null,
            }) with
        {
            Id = "bundle-agent-id",
            Name = "Bundle Agent",
            ChatClient = chatClient,
            Tools = [function],
            ProgressAccessor = progressAccessor,
            OpenTelemetrySourceName = sourceName,
        };

    private static ActivityListener CreateActivityListener(
        string sourceName,
        ConcurrentQueue<Activity> activities)
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = source => string.Equals(
                source.Name,
                sourceName,
                StringComparison.Ordinal) ||
                source.Name.StartsWith(
                    $"{sourceName}.",
                    StringComparison.Ordinal),
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllDataAndRecorded,
            SampleUsingParentId = static (ref ActivityCreationOptions<string> _) =>
                ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activities.Enqueue,
        };
        ActivitySource.AddActivityListener(listener);
        return listener;
    }

    private static void AssertActivityCounts(
        IEnumerable<Activity> activities)
    {
        var activityList = activities.ToList();
        var operationNames = activityList
            .Select(activity =>
                activity.GetTagItem("gen_ai.operation.name")?.ToString()
                ?? activity.OperationName)
            .ToList();
        string evidence = string.Join(
            ", ",
            activityList.Select(activity =>
                $"{activity.Source.Name}:{activity.OperationName}:" +
                $"{activity.GetTagItem("gen_ai.operation.name")}"));
        Assert.True(
            operationNames.Count(name => name.StartsWith(
                "invoke_agent",
                StringComparison.Ordinal)) == 1,
            evidence);
        Assert.True(
            operationNames.Count(name => name.StartsWith(
                "chat",
                StringComparison.Ordinal)) == 2,
            evidence);
        Assert.True(
            operationNames.Count(name => name.StartsWith(
                "execute_tool",
                StringComparison.Ordinal)) == 1,
            evidence);
        Assert.Equal(4, operationNames.Count);
    }

    private static void AssertConcurrentRunEvents(
        IReadOnlyList<IProgressEvent> events,
        string workflowId)
    {
        Assert.Equal(
            [
                typeof(AgentInvokedEvent),
                typeof(LlmCallStartedEvent),
                typeof(LlmCallCompletedEvent),
                typeof(AgentCompletedEvent),
            ],
            events.Select(progressEvent => progressEvent.GetType()));
        Assert.All(
            events,
            progressEvent =>
            {
                Assert.Equal(workflowId, progressEvent.WorkflowId);
                Assert.Equal("bundle-concurrent-agent", progressEvent.AgentId);
            });
        var completed = Assert.IsType<AgentCompletedEvent>(events[^1]);
        Assert.Equal(5, completed.TotalTokens);
        Assert.Equal(0, completed.ToolCallCount);
    }
}
