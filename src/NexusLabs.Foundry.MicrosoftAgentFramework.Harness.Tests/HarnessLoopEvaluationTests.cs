using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

using NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Bundle;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Tests;

public sealed class HarnessLoopEvaluationTests
{
    private static readonly FoundryHarnessAgentFactory Factory = new();

    [Fact]
    public void Create_NullLoopEvaluators_ThrowsArgumentNullException()
    {
        var configuration = HarnessBundleTestsHelpers.CreateBaseline() with
        {
            LoopEvaluators = null!,
        };

        Assert.Throws<ArgumentNullException>(() => Factory.Create(configuration));
    }

    [Fact]
    public void Create_NullLoopEvaluatorElement_ThrowsArgumentException()
    {
        var configuration = HarnessBundleTestsHelpers.CreateBaseline(
            HarnessBundleTestsHelpers.AllFeaturesDisabled() with
            {
                EnableLoopEvaluation = true,
            }) with
        {
            LoopEvaluators = [null!],
        };

        Assert.Throws<ArgumentException>(() => Factory.Create(configuration));
    }

    [Fact]
    public void Create_LoopEnabledWithoutEvaluators_ThrowsArgumentException()
    {
        var configuration = HarnessBundleTestsHelpers.CreateBaseline(
            HarnessBundleTestsHelpers.AllFeaturesDisabled() with
            {
                EnableLoopEvaluation = true,
            });

        var exception = Assert.Throws<ArgumentException>(
            () => Factory.Create(configuration));

        Assert.Contains("LoopEvaluators is empty", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_LoopDisabledWithEvaluator_ThrowsArgumentException()
    {
        var configuration = HarnessBundleTestsHelpers.CreateBaseline() with
        {
            LoopEvaluators =
            [
                new CompletionMarkerLoopEvaluator("DONE"),
            ],
        };

        Assert.Throws<ArgumentException>(() => Factory.Create(configuration));
    }

    [Fact]
    public void Create_LoopDisabledWithOptions_ThrowsArgumentException()
    {
        var configuration = HarnessBundleTestsHelpers.CreateBaseline() with
        {
            LoopAgentOptions = new LoopAgentOptions(),
        };

        Assert.Throws<ArgumentException>(() => Factory.Create(configuration));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_LoopMaxIterationsNotPositive_ThrowsArgumentOutOfRangeException(
        int maxIterations)
    {
        var configuration = CreateLoopConfiguration(
            new HarnessLoopChatClient("DONE")) with
        {
            LoopAgentOptions = new LoopAgentOptions
            {
                MaxIterations = maxIterations,
            },
        };

        Assert.Throws<ArgumentOutOfRangeException>(() => Factory.Create(configuration));
    }

    [Fact]
    public void Create_ValidLoopConfiguration_ReturnsUpstreamLoopAgent()
    {
        var configuration = CreateLoopConfiguration(
            new HarnessLoopChatClient("DONE"));

        var agent = Factory.Create(configuration);

        Assert.NotNull(agent.GetService<LoopAgent>());
    }

    [Fact]
    public async Task Run_MarkerAlreadyPresent_StopsAfterOneCompleteRun()
    {
        var chatClient = new HarnessLoopChatClient("accepted artifact DONE");
        var configuration = CreateLoopConfiguration(chatClient);
        var agent = Factory.Create(configuration);

        var response = await agent.RunAsync(
            "produce an artifact",
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(1, chatClient.CallCount);
        Assert.Equal("accepted artifact DONE", response.Text);
    }

    [Fact]
    public async Task Run_MarkerMissingThenPresent_ReinvokesAndReturnsLastResponse()
    {
        var chatClient = new HarnessLoopChatClient(
            "draft artifact",
            "accepted artifact DONE");
        var configuration = CreateLoopConfiguration(chatClient);
        var agent = Factory.Create(configuration);

        var response = await agent.RunAsync(
            "produce an artifact",
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(2, chatClient.CallCount);
        Assert.Equal("accepted artifact DONE", response.Text);
        Assert.Equal(12, response.Usage?.TotalTokenCount);
        Assert.Contains(
            chatClient.Requests[1].Select(message => message.Text),
            text => text?.Contains("DONE", StringComparison.Ordinal) == true);
        Assert.Contains(
            chatClient.Requests[1].Select(message => message.Text),
            text => text?.Contains("draft artifact", StringComparison.Ordinal) == true);
    }

    [Fact]
    public async Task Run_FreshContext_ReplaysOriginalInputAndFeedbackInNewSession()
    {
        int sessionCreatedCount = 0;
        var chatClient = new HarnessLoopChatClient(
            "draft artifact",
            "accepted artifact");
        var configuration = CreateLoopConfiguration(
            chatClient,
            new DelegateLoopEvaluator(
                static (context, _) =>
                    ValueTask.FromResult(
                        context.Iteration == 1
                            ? LoopEvaluation.Continue("missing required evidence")
                            : LoopEvaluation.Stop()))) with
        {
            LoopAgentOptions = new LoopAgentOptions
            {
                MaxIterations = 3,
                FreshContextPerIteration = true,
                NonStreamingReturnsLastResponseOnly = true,
                SessionCreatedCallback = (_, _) =>
                {
                    sessionCreatedCount++;
                    return ValueTask.CompletedTask;
                },
            },
        };
        var agent = Factory.Create(configuration);

        await agent.RunAsync(
            "produce an artifact",
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(2, chatClient.CallCount);
        Assert.Equal(2, sessionCreatedCount);
        var secondRequestText = string.Join(
            "\n",
            chatClient.Requests[1].Select(message => message.Text));
        Assert.Contains("produce an artifact", secondRequestText, StringComparison.Ordinal);
        Assert.Contains("missing required evidence", secondRequestText, StringComparison.Ordinal);
        Assert.DoesNotContain("draft artifact", secondRequestText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Run_EvaluatorAlwaysContinues_StopsAtGlobalIterationLimit()
    {
        var chatClient = new HarnessLoopChatClient("draft one", "draft two");
        var configuration = CreateLoopConfiguration(
            chatClient,
            new DelegateLoopEvaluator(
                static (_, _) =>
                    ValueTask.FromResult(LoopEvaluation.Continue("revise")))) with
        {
            LoopAgentOptions = new LoopAgentOptions
            {
                MaxIterations = 2,
                NonStreamingReturnsLastResponseOnly = true,
            },
        };
        var agent = Factory.Create(configuration);

        var response = await agent.RunAsync(
            "produce an artifact",
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(2, chatClient.CallCount);
        Assert.Equal("draft two", response.Text);
        Assert.Equal(12, response.Usage?.TotalTokenCount);
    }

    [Fact]
    public async Task Run_StopFromEarlierEvaluator_DoesNotVetoLaterContinuation()
    {
        int firstEvaluatorCalls = 0;
        int secondEvaluatorCalls = 0;
        var chatClient = new HarnessLoopChatClient(
            "draft artifact",
            "accepted artifact");
        var configuration = CreateLoopConfiguration(chatClient) with
        {
            LoopEvaluators =
            [
                new DelegateLoopEvaluator(
                    (_, _) =>
                    {
                        firstEvaluatorCalls++;
                        return ValueTask.FromResult(LoopEvaluation.Stop());
                    }),
                new DelegateLoopEvaluator(
                    (context, _) =>
                    {
                        secondEvaluatorCalls++;
                        return ValueTask.FromResult(
                            context.Iteration == 1
                                ? LoopEvaluation.Continue("revise")
                                : LoopEvaluation.Stop());
                    }),
            ],
        };
        var agent = Factory.Create(configuration);

        await agent.RunAsync(
            "produce an artifact",
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(2, chatClient.CallCount);
        Assert.Equal(2, firstEvaluatorCalls);
        Assert.Equal(2, secondEvaluatorCalls);
    }

    [Fact]
    public async Task Run_EvaluatorFailure_Propagates()
    {
        var chatClient = new HarnessLoopChatClient("draft");
        var configuration = CreateLoopConfiguration(
            chatClient,
            new DelegateLoopEvaluator(
                static (_, _) =>
                    ValueTask.FromException<LoopEvaluation>(
                        new InvalidOperationException("evaluation failed"))));
        var agent = Factory.Create(configuration);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => agent.RunAsync(
                "produce an artifact",
                cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal("evaluation failed", exception.Message);
        Assert.Equal(1, chatClient.CallCount);
    }

    [Fact]
    public async Task Run_CancellationDuringEvaluation_Propagates()
    {
        using var cancellationSource = new CancellationTokenSource();
        var chatClient = new HarnessLoopChatClient("draft");
        var configuration = CreateLoopConfiguration(
            chatClient,
            new DelegateLoopEvaluator(
                (_, cancellationToken) =>
                {
                    cancellationSource.Cancel();
                    return ValueTask.FromCanceled<LoopEvaluation>(cancellationToken);
                }));
        var agent = Factory.Create(configuration);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => agent.RunAsync(
                "produce an artifact",
                cancellationToken: cancellationSource.Token));

        Assert.Equal(1, chatClient.CallCount);
    }

    [Fact]
    public async Task Run_PendingApproval_StopsBeforeEvaluatorRuns()
    {
        int evaluatorCalls = 0;
        var approvalRequest = new ToolApprovalRequestContent(
            "loop-approval-request",
            new FunctionCallContent(
                "loop-approval-call",
                "approval_tool",
                new Dictionary<string, object?>()));
        var chatClient = new HarnessLoopChatClient(
            new ChatResponse(
                new ChatMessage(
                    ChatRole.Assistant,
                    [approvalRequest])));
        var configuration = CreateLoopConfiguration(
            chatClient,
            new DelegateLoopEvaluator(
                (_, _) =>
                {
                    evaluatorCalls++;
                    return ValueTask.FromResult(LoopEvaluation.Continue("revise"));
                }));
        var agent = Factory.Create(configuration);

        var response = await agent.RunAsync(
            "request approval",
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(1, chatClient.CallCount);
        Assert.Equal(0, evaluatorCalls);
        Assert.Single(
            response.Messages
                .SelectMany(message => message.Contents)
                .OfType<ToolApprovalRequestContent>());
    }

    private static FoundryHarnessAgentConfiguration CreateLoopConfiguration(
        HarnessLoopChatClient chatClient,
        LoopEvaluator? evaluator = null) =>
        HarnessBundleTestsHelpers.CreateBaseline(
            HarnessBundleTestsHelpers.AllFeaturesDisabled() with
            {
                EnableLoopEvaluation = true,
            }) with
        {
            ChatClient = chatClient,
            LoopEvaluators =
            [
                evaluator ?? new CompletionMarkerLoopEvaluator("DONE"),
            ],
            LoopAgentOptions = new LoopAgentOptions
            {
                MaxIterations = 3,
                NonStreamingReturnsLastResponseOnly = true,
            },
        };
}
