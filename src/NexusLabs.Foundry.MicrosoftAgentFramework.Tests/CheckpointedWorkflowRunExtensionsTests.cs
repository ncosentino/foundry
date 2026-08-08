using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

using NexusLabs.Foundry.MicrosoftAgentFramework.Diagnostics;
using NexusLabs.Foundry.MicrosoftAgentFramework.Workflows;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Tests;

public sealed class CheckpointedWorkflowRunExtensionsTests
{
    [Fact]
    public async Task StartCheckpointedAgentRun_UsesExplicitSessionAndEmitsCheckpoints()
    {
        var chatClient = new CheckpointScriptedChatClient("complete");
        var workflow = AgentWorkflowBuilder.BuildSequential(
            [
                CreateAgent("single-phase", chatClient),
            ]);
        var checkpointManager = CheckpointManager.CreateInMemory();

        await using var run = await workflow.StartCheckpointedAgentRunAsync(
            new ChatMessage(ChatRole.User, "start"),
            checkpointManager,
            "checkpoint-session",
            TestContext.Current.CancellationToken);
        var observation = await CollectCheckpointsAsync(run);

        Assert.Equal("checkpoint-session", run.SessionId);
        Assert.True(
            observation.Checkpoints.Count > 0,
            $"Observed events: {string.Join(", ", observation.EventTypes)}");
        Assert.Equal(1, chatClient.CallCount);
        Assert.NotNull(
            await checkpointManager.GetLatestCheckpointAsync(
                run.SessionId,
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RestoreCheckpoint_AfterCompletion_ReexecutesOnlyDownstream()
    {
        var releaseSecondPhase = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var firstClient = new CheckpointScriptedChatClient("phase-one-result");
        var secondClient = new CheckpointScriptedChatClient(
            [
                "phase-two-result",
                "phase-two-recovered",
            ],
            async cancellationToken =>
                await releaseSecondPhase.Task.WaitAsync(cancellationToken));
        var diagnostics = new DiagnosticsChatClientMiddleware();
        var workflow = BuildCheckpointBoundaryWorkflow(
            new DiagnosticsRecordingChatClient(firstClient, diagnostics),
            new DiagnosticsRecordingChatClient(secondClient, diagnostics));
        var checkpointManager = CheckpointManager.CreateInMemory();

        await using var run = await workflow.StartCheckpointedAgentRunAsync(
            "start",
            checkpointManager,
            sessionId: null,
            TestContext.Current.CancellationToken);
        CheckpointInfo? acceptedPhaseCheckpoint = null;
        int initialOutputs = 0;
        var initialInvocations = new List<string>();
        bool acceptedBoundaryCompleted = false;
        await foreach (var workflowEvent in run.WatchStreamAsync(
            TestContext.Current.CancellationToken))
        {
            if (workflowEvent is ExecutorCompletedEvent
                {
                    ExecutorId: "accepted-artifact-boundary",
                })
            {
                acceptedBoundaryCompleted = true;
            }

            if (acceptedBoundaryCompleted &&
                workflowEvent is SuperStepCompletedEvent superStep &&
                acceptedPhaseCheckpoint is null &&
                superStep.CompletionInfo?.Checkpoint is { } checkpoint)
            {
                acceptedPhaseCheckpoint = checkpoint;
                acceptedBoundaryCompleted = false;
                releaseSecondPhase.TrySetResult();
            }

            if (workflowEvent is WorkflowOutputEvent)
            {
                initialOutputs++;
            }

            if (workflowEvent is ExecutorInvokedEvent { ExecutorId: { } executorId })
            {
                initialInvocations.Add(executorId);
            }
        }

        Assert.NotNull(acceptedPhaseCheckpoint);
        Assert.True(initialOutputs > 0);
        Assert.Equal(1, firstClient.CallCount);
        Assert.Equal(1, secondClient.CallCount);
        Assert.Contains(
            initialInvocations,
            executorId => IsAgentExecutor(executorId, "phase-one"));
        Assert.Contains(
            initialInvocations,
            executorId => IsAgentExecutor(executorId, "phase-two"));
        Assert.Equal(2, diagnostics.DrainCompletions().Count);

        await run.RestoreCheckpointAsync(
            acceptedPhaseCheckpoint,
            TestContext.Current.CancellationToken);
        int restoredOutputs = 0;
        var restoredInvocations = new List<string>();
        var restoredTerminalMessages = new List<string>();
        await foreach (var workflowEvent in run.WatchStreamAsync(
            TestContext.Current.CancellationToken))
        {
            if (workflowEvent is WorkflowOutputEvent output)
            {
                restoredOutputs++;
                restoredTerminalMessages.AddRange(GetTerminalMessageTexts(output.Data));
            }

            if (workflowEvent is ExecutorInvokedEvent { ExecutorId: { } executorId })
            {
                restoredInvocations.Add(executorId);
            }
        }

        Assert.Equal(1, firstClient.CallCount);
        Assert.Equal(2, secondClient.CallCount);
        Assert.True(restoredOutputs > 0);
        Assert.DoesNotContain(
            restoredInvocations,
            executorId => IsAgentExecutor(executorId, "phase-one"));
        Assert.Contains(
            restoredInvocations,
            executorId => IsAgentExecutor(executorId, "phase-two"));
        Assert.Equal(
            1,
            restoredTerminalMessages.Count(
                message => message == "phase-two-recovered"));
        Assert.Single(diagnostics.DrainCompletions());
    }

    [Fact]
    public async Task ResumeCheckpointedAgentRun_AfterFailureUsesFreshWorkflowAndAcceptedCheckpoint()
    {
        var releaseSecondPhase = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var firstClient = new CheckpointScriptedChatClient("phase-one-result");
        var secondClient = new CheckpointScriptedChatClient(
            [
                new InvalidOperationException("phase two failed"),
            ],
            async cancellationToken =>
                await releaseSecondPhase.Task.WaitAsync(cancellationToken));
        var workflow = BuildCheckpointBoundaryWorkflow(firstClient, secondClient);
        var checkpointManager = CheckpointManager.CreateInMemory();
        CheckpointInfo? acceptedPhaseCheckpoint = null;
        bool failureObserved = false;
        bool acceptedBoundaryCompleted = false;

        await using (var initialRun = await workflow.StartCheckpointedAgentRunAsync(
            "start",
            checkpointManager,
            sessionId: null,
            TestContext.Current.CancellationToken))
        {
            await foreach (var workflowEvent in initialRun.WatchStreamAsync(
                TestContext.Current.CancellationToken))
            {
                if (workflowEvent is ExecutorCompletedEvent
                    {
                        ExecutorId: "accepted-artifact-boundary",
                    })
                {
                    acceptedBoundaryCompleted = true;
                }

                if (acceptedBoundaryCompleted &&
                    workflowEvent is SuperStepCompletedEvent superStep &&
                    acceptedPhaseCheckpoint is null &&
                    superStep.CompletionInfo?.Checkpoint is { } checkpoint)
                {
                    acceptedPhaseCheckpoint = checkpoint;
                    acceptedBoundaryCompleted = false;
                    releaseSecondPhase.TrySetResult();
                }

                if (workflowEvent is WorkflowErrorEvent or ExecutorFailedEvent)
                {
                    failureObserved = true;
                }
            }
        }

        Assert.NotNull(acceptedPhaseCheckpoint);
        Assert.True(failureObserved);
        Assert.Equal(1, firstClient.CallCount);
        Assert.Equal(1, secondClient.CallCount);

        var resumedFirstClient = new CheckpointScriptedChatClient(
            new InvalidOperationException("accepted phase replayed"));
        var resumedSecondClient = new CheckpointScriptedChatClient(
            "phase-two-resumed");
        var resumedWorkflow = BuildCheckpointBoundaryWorkflow(
            resumedFirstClient,
            resumedSecondClient);
        await using var resumedRun = await resumedWorkflow.ResumeCheckpointedAgentRunAsync(
            acceptedPhaseCheckpoint,
            checkpointManager,
            TestContext.Current.CancellationToken);
        int resumedOutputs = 0;
        await foreach (var workflowEvent in resumedRun.WatchStreamAsync(
            TestContext.Current.CancellationToken))
        {
            if (workflowEvent is WorkflowOutputEvent)
            {
                resumedOutputs++;
            }
        }

        Assert.Equal(1, firstClient.CallCount);
        Assert.Equal(1, secondClient.CallCount);
        Assert.Equal(0, resumedFirstClient.CallCount);
        Assert.Equal(1, resumedSecondClient.CallCount);
        Assert.True(resumedOutputs > 0);
    }

    [Fact]
    public async Task StartCheckpointedAgentRun_ConcurrentFanOutCreatesCheckpoint()
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

        var firstClient = new CheckpointScriptedChatClient(
            ["first-result"],
            WaitForBothAsync);
        var secondClient = new CheckpointScriptedChatClient(
            ["second-result"],
            WaitForBothAsync);
        var workflow = AgentWorkflowBuilder.BuildConcurrent(
            [
                CreateAgent("parallel-one", firstClient),
                CreateAgent("parallel-two", secondClient),
            ]);
        var checkpointManager = CheckpointManager.CreateInMemory();

        await using var run = await workflow.StartCheckpointedAgentRunAsync(
            "start",
            checkpointManager,
            sessionId: null,
            TestContext.Current.CancellationToken);
        var observation = await CollectCheckpointsAsync(run);

        Assert.True(
            entered == 2,
            $"Observed events: {string.Join(", ", observation.EventTypes)}");
        Assert.Equal(1, firstClient.CallCount);
        Assert.Equal(1, secondClient.CallCount);
        Assert.NotEmpty(observation.Checkpoints);
    }

    [Fact]
    public async Task StartCheckpointedAgentRun_PreCancelledToken_Throws()
    {
        var workflow = AgentWorkflowBuilder.BuildSequential(
            [
                CreateAgent(
                    "cancelled",
                    new CheckpointScriptedChatClient("unused")),
            ]);
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => workflow.StartCheckpointedAgentRunAsync(
                "start",
                CheckpointManager.CreateInMemory(),
                sessionId: null,
                cancellationSource.Token));
    }

    [Fact]
    public async Task CheckpointedRun_InvalidArguments_Throw()
    {
        Workflow workflow = AgentWorkflowBuilder.BuildSequential(
            [
                CreateAgent(
                    "validation",
                    new CheckpointScriptedChatClient("unused")),
            ]);
        CheckpointManager checkpointManager = CheckpointManager.CreateInMemory();
        var checkpoint = new CheckpointInfo("session", "checkpoint");

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => ((Workflow)null!).StartCheckpointedAgentRunAsync(
                "start",
                checkpointManager,
                sessionId: null,
                TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentException>(
            () => workflow.StartCheckpointedAgentRunAsync(
                string.Empty,
                checkpointManager,
                sessionId: null,
                TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => workflow.StartCheckpointedAgentRunAsync(
                (ChatMessage)null!,
                checkpointManager,
                sessionId: null,
                TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => workflow.StartCheckpointedAgentRunAsync(
                "start",
                null!,
                sessionId: null,
                TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => ((Workflow)null!).ResumeCheckpointedAgentRunAsync(
                checkpoint,
                checkpointManager,
                TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => workflow.ResumeCheckpointedAgentRunAsync(
                null!,
                checkpointManager,
                TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => workflow.ResumeCheckpointedAgentRunAsync(
                checkpoint,
                null!,
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ResumeCheckpointedAgentRun_IncompatibleWorkflow_Throws()
    {
        Workflow workflow = AgentWorkflowBuilder.BuildSequential(
            "compatible-workflow",
            [
                CreateAgent(
                    "compatible-agent",
                    new CheckpointScriptedChatClient("complete")),
            ]);
        CheckpointManager checkpointManager = CheckpointManager.CreateInMemory();

        await using var run = await workflow.StartCheckpointedAgentRunAsync(
            "start",
            checkpointManager,
            sessionId: null,
            TestContext.Current.CancellationToken);
        var observation = await CollectCheckpointsAsync(run);
        CheckpointInfo checkpoint = observation.Checkpoints[0];
        Workflow incompatibleWorkflow = AgentWorkflowBuilder.BuildSequential(
            "incompatible-workflow",
            [
                CreateAgent(
                    "different-agent",
                    new CheckpointScriptedChatClient("unused")),
            ]);

        await Assert.ThrowsAsync<InvalidDataException>(
            () => incompatibleWorkflow.ResumeCheckpointedAgentRunAsync(
                checkpoint,
                checkpointManager,
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task WatchCancellation_StopsObservationWithoutCancellingRun()
    {
        var entered = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var client = new CheckpointScriptedChatClient(
            ["complete"],
            async cancellationToken =>
            {
                entered.TrySetResult();
                await release.Task.WaitAsync(cancellationToken);
            });
        Workflow workflow = AgentWorkflowBuilder.BuildSequential(
            [
                CreateAgent("cancellable-observer", client),
            ]);
        CheckpointManager checkpointManager = CheckpointManager.CreateInMemory();

        await using var run = await workflow.StartCheckpointedAgentRunAsync(
            "start",
            checkpointManager,
            sessionId: null,
            TestContext.Current.CancellationToken);
        using var observerCancellation = new CancellationTokenSource();
        Task observer = ConsumeEventsAsync(run, observerCancellation.Token);
        await entered.Task.WaitAsync(TestContext.Current.CancellationToken);

        observerCancellation.Cancel();
        await observer.WaitAsync(TestContext.Current.CancellationToken);

        release.TrySetResult();
        var observation = await CollectCheckpointsAsync(run);

        Assert.Equal(1, client.CallCount);
        Assert.NotEmpty(observation.Checkpoints);
    }

    private static AIAgent CreateAgent(
        string name,
        IChatClient chatClient) =>
        chatClient.AsAIAgent(
            new ChatClientAgentOptions
            {
                Id = name,
                Name = name,
                Description = $"{name} description.",
            });

    private static Workflow BuildCheckpointBoundaryWorkflow(
        IChatClient firstClient,
        IChatClient secondClient)
    {
        AIAgentHostOptions agentOptions = new()
        {
            ForwardIncomingMessages = true,
            ReassignOtherAgentsAsUsers = true,
        };
        ExecutorBinding firstAgent = new AIAgentBinding(
            CreateAgent("phase-one", firstClient),
            agentOptions);
        ExecutorBinding checkpointBoundary =
            new ChatForwardingExecutor("accepted-artifact-boundary").BindExecutor();
        ExecutorBinding secondAgent = new AIAgentBinding(
            CreateAgent("phase-two", secondClient),
            agentOptions);

        return new WorkflowBuilder(firstAgent)
            .AddEdge(firstAgent, checkpointBoundary)
            .AddEdge(checkpointBoundary, secondAgent)
            .WithOutputFrom(secondAgent)
            .Build();
    }

    private static async Task<(
        IReadOnlyList<CheckpointInfo> Checkpoints,
        IReadOnlyList<string> EventTypes)> CollectCheckpointsAsync(
        StreamingRun run)
    {
        var checkpoints = new List<CheckpointInfo>();
        var eventTypes = new List<string>();
        await foreach (var workflowEvent in run.WatchStreamAsync(
            TestContext.Current.CancellationToken))
        {
            eventTypes.Add(
                workflowEvent switch
                {
                    WorkflowErrorEvent error =>
                        $"{workflowEvent.GetType().Name}:{error.Exception}",
                    ExecutorFailedEvent failed =>
                        $"{workflowEvent.GetType().Name}:{failed.Data}",
                    _ => workflowEvent.GetType().Name,
                });
            if (workflowEvent is SuperStepCompletedEvent
                {
                    CompletionInfo.Checkpoint: { } checkpoint,
                })
            {
                checkpoints.Add(checkpoint);
            }
        }

        return (checkpoints, eventTypes);
    }

    private static async Task ConsumeEventsAsync(
        StreamingRun run,
        CancellationToken cancellationToken)
    {
        await foreach (var _ in run.WatchStreamAsync(cancellationToken))
        {
        }
    }

    private static IEnumerable<string> GetTerminalMessageTexts(object? data) =>
        data switch
        {
            AgentResponseUpdate update => [update.ToString()],
            ChatMessage message when message.Text is { } text => [text],
            IEnumerable<ChatMessage> messages =>
                messages
                    .Select(message => message.Text)
                    .OfType<string>(),
            _ => [],
        };

    private static bool IsAgentExecutor(
        string executorId,
        string agentId) =>
        executorId.Contains(
            agentId.Replace('-', '_'),
            StringComparison.Ordinal);
}
