using Microsoft.Agents.AI.Workflows;

using NexusLabs.Foundry.MicrosoftAgentFramework.Progress;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Workflows.Declarative.Tests;

/// <summary>
/// Covers the bridge from the upstream declarative event stream onto Foundry progress reporting.
/// </summary>
public sealed class DeclarativeWorkflowProgressTests
{
    private const string AgentWorkflow = """
        kind: Workflow
        trigger:

          kind: OnConversationStart
          id: progress_workflow
          actions:

            - kind: SetVariable
              id: capture_topic
              variable: Local.topic
              value: =System.LastMessage.Text

            - kind: InvokeAzureAgent
              id: classify
              agent:
                name: NexusLabs.Foundry.MicrosoftAgentFramework.Workflows.Declarative.Tests.ClassifierAgent
              input:
                messages: =Local.topic
              output:
                autoSend: true
                responseObject: Local.Classification
        """;

    [Fact]
    public async Task ReportProgressTo_DeclarativeRun_ReportsActionsByTheirAuthoredIds()
    {
        using var host = DeclarativeTestFixture.CreateHost();
        var reporter = new RecordingProgressReporter("declarative-progress");
        var workflow = host.AgentFactory.CreateDeclarativeWorkflow(AgentWorkflow);

        var forwarded = 0;
        StreamingRun run = await InProcessExecution.RunStreamingAsync(
            workflow,
            "topic",
            checkpointManager: CheckpointManager.CreateInMemory(),
            cancellationToken: TestContext.Current.CancellationToken);

        await foreach (var _ in run
            .WatchStreamAsync(TestContext.Current.CancellationToken)
            .ReportProgressTo(reporter, TestContext.Current.CancellationToken))
        {
            forwarded++;
        }

        var startedActionIds = reporter.Events
            .OfType<DeclarativeActionStartedProgressEvent>()
            .Select(started => started.ActionId)
            .ToList();

        Assert.Contains("capture_topic", startedActionIds);
        Assert.Contains("classify", startedActionIds);
        Assert.Contains(reporter.Events, e => e is DeclarativeActionCompletedProgressEvent);
        Assert.Contains(reporter.Events, e => e is SuperStepStartedProgressEvent);
        Assert.True(forwarded > 0, "The bridge must forward the upstream stream, not consume it.");
    }

    [Fact]
    public async Task ReportProgressTo_AgentResponse_ReportsResponseChunks()
    {
        using var host = DeclarativeTestFixture.CreateHost();
        var reporter = new RecordingProgressReporter("declarative-progress");
        var workflow = host.AgentFactory.CreateDeclarativeWorkflow(AgentWorkflow);

        StreamingRun run = await InProcessExecution.RunStreamingAsync(
            workflow,
            "topic",
            checkpointManager: CheckpointManager.CreateInMemory(),
            cancellationToken: TestContext.Current.CancellationToken);

        await foreach (var _ in run
            .WatchStreamAsync(TestContext.Current.CancellationToken)
            .ReportProgressTo(reporter, TestContext.Current.CancellationToken))
        {
        }

        Assert.Contains(reporter.Events, e => e is AgentResponseChunkEvent);
    }

    [Fact]
    public async Task ReportProgressTo_SequenceNumbers_AreStrictlyIncreasing()
    {
        using var host = DeclarativeTestFixture.CreateHost();
        var reporter = new RecordingProgressReporter("declarative-progress");
        var workflow = host.AgentFactory.CreateDeclarativeWorkflow(AgentWorkflow);

        StreamingRun run = await InProcessExecution.RunStreamingAsync(
            workflow,
            "topic",
            checkpointManager: CheckpointManager.CreateInMemory(),
            cancellationToken: TestContext.Current.CancellationToken);

        await foreach (var _ in run
            .WatchStreamAsync(TestContext.Current.CancellationToken)
            .ReportProgressTo(reporter, TestContext.Current.CancellationToken))
        {
        }

        var sequences = reporter.Events.Select(e => e.SequenceNumber).ToList();
        Assert.Equal(sequences.OrderBy(s => s), sequences);
        Assert.Equal(sequences.Distinct().Count(), sequences.Count);
    }
}
