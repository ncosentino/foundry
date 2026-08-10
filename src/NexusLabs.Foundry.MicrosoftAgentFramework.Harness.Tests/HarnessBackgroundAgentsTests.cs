using System.Text.Json;

using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

using NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Bundle;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Tests;

public sealed class HarnessBackgroundAgentsTests
{
    private static readonly FoundryHarnessAgentFactory Factory = new();

    [Fact]
    public void Create_NullBackgroundAgents_ThrowsArgumentNullException()
    {
        var configuration = HarnessBundleTestsHelpers.CreateBaseline() with
        {
            BackgroundAgents = null!,
        };

        Assert.Throws<ArgumentNullException>(() => Factory.Create(configuration));
    }

    [Fact]
    public void Create_NullBackgroundAgentElement_ThrowsArgumentException()
    {
        var configuration = HarnessBundleTestsHelpers.CreateBaseline(
            HarnessBundleTestsHelpers.AllFeaturesDisabled() with
            {
                EnableBackgroundAgents = true,
            }) with
        {
            BackgroundAgents = [null!],
        };

        Assert.Throws<ArgumentException>(() => Factory.Create(configuration));
    }

    [Fact]
    public void Create_BackgroundAgentsEnabledWithoutAgents_ThrowsArgumentException()
    {
        var configuration = HarnessBundleTestsHelpers.CreateBaseline(
            HarnessBundleTestsHelpers.AllFeaturesDisabled() with
            {
                EnableBackgroundAgents = true,
            });

        Assert.Throws<ArgumentException>(() => Factory.Create(configuration));
    }

    [Fact]
    public void Create_BackgroundAgentsDisabledWithAgent_ThrowsArgumentException()
    {
        var configuration = HarnessBundleTestsHelpers.CreateBaseline() with
        {
            BackgroundAgents =
            [
                CreateAgent("worker", new HarnessBackgroundChildChatClient("done")),
            ],
        };

        Assert.Throws<ArgumentException>(() => Factory.Create(configuration));
    }

    [Fact]
    public void Create_BackgroundAgentsDisabledWithOptions_ThrowsArgumentException()
    {
        var configuration = HarnessBundleTestsHelpers.CreateBaseline() with
        {
            BackgroundAgentsProviderOptions = new BackgroundAgentsProviderOptions(),
        };

        Assert.Throws<ArgumentException>(() => Factory.Create(configuration));
    }

    [Fact]
    public void Create_BackgroundAgentWithBlankName_ThrowsArgumentException()
    {
        var configuration = CreateBackgroundConfiguration(
            new FakeHarnessChatClient(),
            new HarnessBackgroundChildChatClient("done").AsAIAgent());

        Assert.Throws<ArgumentException>(() => Factory.Create(configuration));
    }

    [Fact]
    public void Create_DuplicateBackgroundAgentNamesIgnoringCase_ThrowsArgumentException()
    {
        var configuration = CreateBackgroundConfiguration(
            new FakeHarnessChatClient(),
            CreateAgent("Worker", new HarnessBackgroundChildChatClient("one")),
            CreateAgent("worker", new HarnessBackgroundChildChatClient("two")));

        var exception = Assert.Throws<ArgumentException>(
            () => Factory.Create(configuration));

        Assert.Contains("case-insensitive", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_AdditionalBackgroundProvider_ThrowsArgumentException()
    {
        var child = CreateAgent(
            "worker",
            new HarnessBackgroundChildChatClient("done"));
        var configuration = HarnessBundleTestsHelpers.CreateBaseline() with
        {
            AdditionalContextProviders =
            [
                new BackgroundAgentsProvider([child]),
            ],
        };

        var exception = Assert.Throws<ArgumentException>(
            () => Factory.Create(configuration));

        Assert.Contains("AdditionalContextProviders", exception.Message, StringComparison.Ordinal);
        Assert.Contains("BackgroundAgentsProvider", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_BackgroundAgentsWithFreshContextLoop_ThrowsArgumentException()
    {
        var configuration = CreateBackgroundConfiguration(
            new FakeHarnessChatClient(),
            CreateAgent(
                "worker",
                new HarnessBackgroundChildChatClient("done"))) with
        {
            Features = HarnessBundleTestsHelpers.AllFeaturesDisabled() with
            {
                EnableBackgroundAgents = true,
                EnableLoopEvaluation = true,
            },
            LoopEvaluators =
            [
                new BackgroundTaskCompletionLoopEvaluator(),
            ],
            LoopAgentOptions = new LoopAgentOptions
            {
                FreshContextPerIteration = true,
            },
        };

        var exception = Assert.Throws<ArgumentException>(
            () => Factory.Create(configuration));

        Assert.Contains("FreshContextPerIteration", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Lost", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_ValidBackgroundConfiguration_ReturnsUpstreamProvider()
    {
        var configuration = CreateBackgroundConfiguration(
            new FakeHarnessChatClient(),
            CreateAgent("worker", new HarnessBackgroundChildChatClient("done")));

        var agent = Factory.Create(configuration);

        Assert.NotNull(agent.GetService<BackgroundAgentsProvider>());
    }

    [Fact]
    public async Task Run_BackgroundTaskCompletionEvaluator_ResolvesConfiguredProvider()
    {
        var parentClient = new HarnessLoopChatClient("DONE");
        var configuration = HarnessBundleTestsHelpers.CreateBaseline(
            HarnessBundleTestsHelpers.AllFeaturesDisabled() with
            {
                EnableBackgroundAgents = true,
                EnableLoopEvaluation = true,
            }) with
        {
            ChatClient = parentClient,
            BackgroundAgents =
            [
                CreateAgent(
                    "worker",
                    new HarnessBackgroundChildChatClient("done")),
            ],
            LoopEvaluators =
            [
                new BackgroundTaskCompletionLoopEvaluator(),
            ],
        };
        var parent = Factory.Create(configuration);

        await parent.RunAsync(
            "no tasks are active",
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(1, parentClient.CallCount);
        Assert.NotNull(parent.GetService<BackgroundAgentsProvider>());
    }

    [Fact]
    public async Task Run_CustomInstructionsWithoutPlaceholder_OmitsRenderedAgentList()
    {
        var parentClient = new FakeHarnessChatClient();
        var configuration = HarnessBundleTestsHelpers.CreateBaseline(
            HarnessBundleTestsHelpers.AllFeaturesDisabled() with
            {
                EnableBackgroundAgents = true,
            }) with
        {
            ChatClient = parentClient,
            BackgroundAgents =
            [
                CreateAgent(
                    "worker",
                    new HarnessBackgroundChildChatClient("done")),
            ],
            BackgroundAgentsProviderOptions = new BackgroundAgentsProviderOptions
            {
                Instructions = "Custom delegation instructions without a placeholder.",
            },
        };
        var parent = Factory.Create(configuration);

        await parent.RunAsync(
            "inspect instructions",
            cancellationToken: TestContext.Current.CancellationToken);

        string requestText = parentClient.LastOptions?.Instructions ?? string.Empty;
        Assert.Contains(
            "Custom delegation instructions without a placeholder.",
            requestText,
            StringComparison.Ordinal);
        Assert.DoesNotContain("Available background agents", requestText, StringComparison.Ordinal);
        Assert.DoesNotContain("worker: worker description", requestText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Run_CustomInstructionsWithPlaceholder_IncludesRenderedAgentList()
    {
        var parentClient = new FakeHarnessChatClient();
        var configuration = HarnessBundleTestsHelpers.CreateBaseline(
            HarnessBundleTestsHelpers.AllFeaturesDisabled() with
            {
                EnableBackgroundAgents = true,
            }) with
        {
            ChatClient = parentClient,
            BackgroundAgents =
            [
                CreateAgent(
                    "worker",
                    new HarnessBackgroundChildChatClient("done")),
            ],
            BackgroundAgentsProviderOptions = new BackgroundAgentsProviderOptions
            {
                Instructions = "Custom delegation instructions.\n{background_agents}",
            },
        };
        var parent = Factory.Create(configuration);

        await parent.RunAsync(
            "inspect instructions",
            cancellationToken: TestContext.Current.CancellationToken);

        string requestText = parentClient.LastOptions?.Instructions ?? string.Empty;
        Assert.Contains("Available background agents", requestText, StringComparison.Ordinal);
        Assert.Contains("worker: worker description", requestText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Tools_TwoStartedTasks_RunConcurrentlyAndReturnIndependentResults()
    {
        int entered = 0;
        var bothEntered = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        async Task WaitForBothAsync(CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref entered) == 2)
            {
                bothEntered.TrySetResult();
            }

            await bothEntered.Task.WaitAsync(cancellationToken);
        }

        var firstClient = new HarnessBackgroundChildChatClient(
            "first-result",
            WaitForBothAsync);
        var secondClient = new HarnessBackgroundChildChatClient(
            "second-result",
            WaitForBothAsync);
        var parentClient = new FakeHarnessChatClient();
        var configuration = CreateBackgroundConfiguration(
            parentClient,
            CreateAgent("worker-a", firstClient),
            CreateAgent("worker-b", secondClient));
        var (parent, session, tools) = await CreateToolHarnessAsync(
            configuration,
            parentClient);

        int firstTaskId = await StartTaskAsync(
            tools,
            "worker-a",
            "first input",
            "first task",
            TestContext.Current.CancellationToken);
        int secondTaskId = await StartTaskAsync(
            tools,
            "worker-b",
            "second input",
            "second task",
            TestContext.Current.CancellationToken);
        await WaitForFirstCompletionAsync(
            tools,
            [firstTaskId, secondTaskId],
            TestContext.Current.CancellationToken);
        await WaitForFirstCompletionAsync(
            tools,
            [firstTaskId, secondTaskId],
            TestContext.Current.CancellationToken);

        string firstResult = await GetTaskResultAsync(
            tools,
            firstTaskId,
            TestContext.Current.CancellationToken);
        string secondResult = await GetTaskResultAsync(
            tools,
            secondTaskId,
            TestContext.Current.CancellationToken);

        Assert.Equal("first-result", firstResult);
        Assert.Equal("second-result", secondResult);
        Assert.Equal(1, firstClient.CallCount);
        Assert.Equal(1, secondClient.CallCount);
        Assert.False(firstClient.LastCancellationCanBeCanceled);
        Assert.False(secondClient.LastCancellationCanBeCanceled);
        Assert.NotNull(parent.GetService<BackgroundAgentsProvider>());
        Assert.NotNull(session);
    }

    [Fact]
    public async Task Tools_CompletedTask_CanContinueAndBeCleared()
    {
        var childClient = new HarnessBackgroundChildChatClient("child-result");
        var parentClient = new FakeHarnessChatClient();
        var configuration = CreateBackgroundConfiguration(
            parentClient,
            CreateAgent("worker", childClient));
        var (_, _, tools) = await CreateToolHarnessAsync(configuration, parentClient);

        int taskId = await StartTaskAsync(
            tools,
            "worker",
            "first input",
            "continuable task",
            TestContext.Current.CancellationToken);
        await WaitForFirstCompletionAsync(
            tools,
            [taskId],
            TestContext.Current.CancellationToken);
        Assert.Equal(
            "child-result",
            await GetTaskResultAsync(
                tools,
                taskId,
                TestContext.Current.CancellationToken));

        string continued = await InvokeTextAsync(
            tools["background_agents_continue_task"],
            new AIFunctionArguments
            {
                ["taskId"] = taskId,
                ["text"] = "follow-up input",
            },
            TestContext.Current.CancellationToken);
        Assert.Contains("continued", continued, StringComparison.OrdinalIgnoreCase);
        await WaitForFirstCompletionAsync(
            tools,
            [taskId],
            TestContext.Current.CancellationToken);
        Assert.Equal(
            "child-result",
            await GetTaskResultAsync(
                tools,
                taskId,
                TestContext.Current.CancellationToken));
        Assert.Equal(2, childClient.CallCount);

        string cleared = await InvokeTextAsync(
            tools["background_agents_clear_completed_task"],
            new AIFunctionArguments
            {
                ["taskId"] = taskId,
            },
            TestContext.Current.CancellationToken);
        Assert.Contains("cleared", cleared, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(
            "No tasks.",
            await InvokeTextAsync(
                tools["background_agents_get_all_tasks"],
                new AIFunctionArguments(),
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Tools_ChildFailure_IsTerminalAndReturnedAsText()
    {
        var childClient = new HarnessBackgroundChildChatClient(
            response: string.Empty,
            exception: new InvalidOperationException("child failed"));
        var parentClient = new FakeHarnessChatClient();
        var configuration = CreateBackgroundConfiguration(
            parentClient,
            CreateAgent("worker", childClient));
        var (_, _, tools) = await CreateToolHarnessAsync(configuration, parentClient);

        int taskId = await StartTaskAsync(
            tools,
            "worker",
            "input",
            "failing task",
            TestContext.Current.CancellationToken);
        await WaitForFirstCompletionAsync(
            tools,
            [taskId],
            TestContext.Current.CancellationToken);

        string result = await GetTaskResultAsync(
            tools,
            taskId,
            TestContext.Current.CancellationToken);

        Assert.Contains("Task failed", result, StringComparison.Ordinal);
        Assert.Contains("child failed", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Tools_ChildApprovalRequest_IsNotPropagatedThroughTextResult()
    {
        var approvalRequest = new ToolApprovalRequestContent(
            "background-approval-request",
            new FunctionCallContent(
                "background-approval-call",
                "approval_tool",
                new Dictionary<string, object?>()));
        var childClient = new HarnessLoopChatClient(
            new ChatResponse(
                new ChatMessage(
                    ChatRole.Assistant,
                    [approvalRequest])));
        var parentClient = new FakeHarnessChatClient();
        var configuration = CreateBackgroundConfiguration(
            parentClient,
            CreateAgent("approval-worker", childClient));
        var (_, _, tools) = await CreateToolHarnessAsync(configuration, parentClient);

        int taskId = await StartTaskAsync(
            tools,
            "approval-worker",
            "input",
            "approval task",
            TestContext.Current.CancellationToken);
        await WaitForFirstCompletionAsync(
            tools,
            [taskId],
            TestContext.Current.CancellationToken);

        string result = await GetTaskResultAsync(
            tools,
            taskId,
            TestContext.Current.CancellationToken);
        string tasks = await InvokeTextAsync(
            tools["background_agents_get_all_tasks"],
            new AIFunctionArguments(),
            TestContext.Current.CancellationToken);

        Assert.Equal(string.Empty, result);
        Assert.Contains("[Completed]", tasks, StringComparison.Ordinal);
        Assert.Contains("approval-worker", tasks, StringComparison.Ordinal);
        Assert.DoesNotContain(
            approvalRequest.RequestId,
            result,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Tools_ParentCancellationDoesNotCancelStartedChild()
    {
        var release = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var childEntered = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var childClient = new HarnessBackgroundChildChatClient(
            "completed after parent cancellation",
            async _ =>
            {
                childEntered.TrySetResult();
                await release.Task;
            });
        var parentClient = new FakeHarnessChatClient();
        var configuration = CreateBackgroundConfiguration(
            parentClient,
            CreateAgent("worker", childClient));
        var (_, _, tools) = await CreateToolHarnessAsync(configuration, parentClient);
        using var cancellationSource = new CancellationTokenSource();

        int taskId = await StartTaskAsync(
            tools,
            "worker",
            "input",
            "non-cancelable child",
            cancellationSource.Token);
        await childEntered.Task.WaitAsync(TestContext.Current.CancellationToken);
        cancellationSource.Cancel();

        Assert.False(childClient.LastCancellationCanBeCanceled);
        release.TrySetResult();
        await WaitForFirstCompletionAsync(
            tools,
            [taskId],
            TestContext.Current.CancellationToken);
        Assert.Equal(
            "completed after parent cancellation",
            await GetTaskResultAsync(
                tools,
                taskId,
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Tools_CancelledWait_RemainsPendingUntilChildCompletes()
    {
        var release = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var childEntered = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var childClient = new HarnessBackgroundChildChatClient(
            "completed",
            async _ =>
            {
                childEntered.TrySetResult();
                await release.Task;
            });
        var parentClient = new FakeHarnessChatClient();
        var configuration = CreateBackgroundConfiguration(
            parentClient,
            CreateAgent("worker", childClient));
        var (_, _, tools) = await CreateToolHarnessAsync(configuration, parentClient);
        int taskId = await StartTaskAsync(
            tools,
            "worker",
            "input",
            "wait cancellation task",
            TestContext.Current.CancellationToken);
        await childEntered.Task.WaitAsync(TestContext.Current.CancellationToken);
        using var cancellationSource = new CancellationTokenSource();

        Task<string> waitTask = WaitForFirstCompletionAsync(
            tools,
            [taskId],
            cancellationSource.Token);
        cancellationSource.Cancel();
        await Task.Delay(25, TestContext.Current.CancellationToken);

        Assert.False(waitTask.IsCompleted);
        release.TrySetResult();
        string result = await waitTask.WaitAsync(TestContext.Current.CancellationToken);
        Assert.Contains("finished", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Tools_UnknownAgent_DoesNotConsumeTaskId()
    {
        var parentClient = new FakeHarnessChatClient();
        var configuration = CreateBackgroundConfiguration(
            parentClient,
            CreateAgent(
                "worker",
                new HarnessBackgroundChildChatClient("done")));
        var (_, _, tools) = await CreateToolHarnessAsync(configuration, parentClient);

        string missing = await InvokeTextAsync(
            tools["background_agents_start_task"],
            new AIFunctionArguments
            {
                ["agentName"] = "missing",
                ["input"] = "input",
                ["description"] = "missing task",
            },
            TestContext.Current.CancellationToken);
        int taskId = await StartTaskAsync(
            tools,
            "worker",
            "input",
            "first valid task",
            TestContext.Current.CancellationToken);

        Assert.Contains("No background agent", missing, StringComparison.Ordinal);
        Assert.Equal(1, taskId);
    }

    [Fact]
    public async Task RestoredSession_MarksPreviouslyRunningTaskLost()
    {
        var release = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var childEntered = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var childClient = new HarnessBackgroundChildChatClient(
            "late result",
            async _ =>
            {
                childEntered.TrySetResult();
                await release.Task;
            });
        var parentClient = new FakeHarnessChatClient();
        var configuration = CreateBackgroundConfiguration(
            parentClient,
            CreateAgent("worker", childClient));
        var (parent, session, tools) = await CreateToolHarnessAsync(
            configuration,
            parentClient);

        await StartTaskAsync(
            tools,
            "worker",
            "input",
            "restored task",
            TestContext.Current.CancellationToken);
        await childEntered.Task.WaitAsync(TestContext.Current.CancellationToken);
        JsonElement serialized = await parent.SerializeSessionAsync(
            session,
            cancellationToken: TestContext.Current.CancellationToken);
        var restoredSession = await parent.DeserializeSessionAsync(
            serialized,
            cancellationToken: TestContext.Current.CancellationToken);

        await parent.RunAsync(
            "refresh restored provider state",
            restoredSession,
            cancellationToken: TestContext.Current.CancellationToken);
        var restoredTools = GetBackgroundTools(parentClient);
        string tasks = await InvokeTextAsync(
            restoredTools["background_agents_get_all_tasks"],
            new AIFunctionArguments(),
            TestContext.Current.CancellationToken);

        Assert.Contains("[Lost]", tasks, StringComparison.Ordinal);
        Assert.Contains("restored task", tasks, StringComparison.Ordinal);
        release.TrySetResult();
    }

    private static FoundryHarnessAgentConfiguration CreateBackgroundConfiguration(
        FakeHarnessChatClient parentClient,
        params AIAgent[] backgroundAgents) =>
        HarnessBundleTestsHelpers.CreateBaseline(
            HarnessBundleTestsHelpers.AllFeaturesDisabled() with
            {
                EnableBackgroundAgents = true,
            }) with
        {
            ChatClient = parentClient,
            BackgroundAgents = backgroundAgents,
        };

    private static AIAgent CreateAgent(
        string name,
        IChatClient chatClient) =>
        chatClient.AsAIAgent(
            name: name,
            description: $"{name} description.");

    private static async Task<(
        AIAgent Parent,
        AgentSession Session,
        IReadOnlyDictionary<string, AIFunction> Tools)> CreateToolHarnessAsync(
            FoundryHarnessAgentConfiguration configuration,
            FakeHarnessChatClient parentClient)
    {
        var parent = Factory.Create(configuration);
        var session = await parent.CreateSessionAsync(
            TestContext.Current.CancellationToken);
        await parent.RunAsync(
            "expose background tools",
            session,
            cancellationToken: TestContext.Current.CancellationToken);
        return (parent, session, GetBackgroundTools(parentClient));
    }

    private static IReadOnlyDictionary<string, AIFunction> GetBackgroundTools(
        FakeHarnessChatClient parentClient) =>
        parentClient.LastOptions?.Tools?
            .OfType<AIFunction>()
            .Where(tool => tool.Name.StartsWith(
                "background_agents_",
                StringComparison.Ordinal))
            .ToDictionary(tool => tool.Name, StringComparer.Ordinal)
        ?? throw new InvalidOperationException("Background tools were not supplied.");

    private static async Task<int> StartTaskAsync(
        IReadOnlyDictionary<string, AIFunction> tools,
        string agentName,
        string input,
        string description,
        CancellationToken cancellationToken = default)
    {
        string result = await InvokeTextAsync(
            tools["background_agents_start_task"],
            new AIFunctionArguments
            {
                ["agentName"] = agentName,
                ["input"] = input,
                ["description"] = description,
            },
            cancellationToken);
        Assert.Contains("started", result, StringComparison.OrdinalIgnoreCase);
        const string Prefix = "Background task ";
        int prefixIndex = result.IndexOf(Prefix, StringComparison.Ordinal);
        Assert.True(prefixIndex >= 0);
        int idStart = prefixIndex + Prefix.Length;
        int idEnd = result.IndexOf(' ', idStart);
        Assert.True(idEnd > idStart);
        return int.Parse(result[idStart..idEnd], System.Globalization.CultureInfo.InvariantCulture);
    }

    private static Task<string> WaitForFirstCompletionAsync(
        IReadOnlyDictionary<string, AIFunction> tools,
        List<int> taskIds,
        CancellationToken cancellationToken) =>
        InvokeTextAsync(
            tools["background_agents_wait_for_first_completion"],
            new AIFunctionArguments
            {
                ["taskIds"] = taskIds,
            },
            cancellationToken);

    private static Task<string> GetTaskResultAsync(
        IReadOnlyDictionary<string, AIFunction> tools,
        int taskId,
        CancellationToken cancellationToken) =>
        InvokeTextAsync(
            tools["background_agents_get_task_results"],
            new AIFunctionArguments
            {
                ["taskId"] = taskId,
            },
            cancellationToken);

    private static async Task<string> InvokeTextAsync(
        AIFunction function,
        AIFunctionArguments arguments,
        CancellationToken cancellationToken)
    {
        object? result = await function.InvokeAsync(arguments, cancellationToken);
        return result?.ToString() ?? string.Empty;
    }
}
