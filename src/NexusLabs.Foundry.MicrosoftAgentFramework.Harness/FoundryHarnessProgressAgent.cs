using System.Diagnostics;
using System.Runtime.CompilerServices;

using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

using NexusLabs.Foundry.MicrosoftAgentFramework.Progress;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Bundle;

internal sealed class FoundryHarnessProgressAgent(
    AIAgent innerAgent,
    IProgressReporterAccessor progressAccessor,
    FoundryHarnessProgressRunCoordinator runCoordinator,
    string agentId,
    string agentName) : DelegatingAIAgent(innerAgent)
{
    protected override async Task<AgentResponse> RunCoreAsync(
        IEnumerable<ChatMessage> messages,
        AgentSession? session = null,
        AgentRunOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var state = BeginRun();
        var previous = runCoordinator.Enter(state);
        using var progressScope = progressAccessor.BeginScope(state.Reporter);
        var stopwatch = Stopwatch.StartNew();
        ReportInvoked(state);

        try
        {
            var response = await base
                .RunCoreAsync(messages, session, options, cancellationToken)
                .ConfigureAwait(false);
            stopwatch.Stop();
            ReportCompleted(state, stopwatch.Elapsed);
            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            ReportFailed(state, ex);
            throw;
        }
        finally
        {
            runCoordinator.Restore(previous);
        }
    }

    protected override async IAsyncEnumerable<AgentResponseUpdate> RunCoreStreamingAsync(
        IEnumerable<ChatMessage> messages,
        AgentSession? session = null,
        AgentRunOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var state = BeginRun();
        var previous = runCoordinator.Enter(state);
        using var progressScope = progressAccessor.BeginScope(state.Reporter);
        var stopwatch = Stopwatch.StartNew();
        ReportInvoked(state);
        Exception? failure = null;

        try
        {
            var enumerator = base
                .RunCoreStreamingAsync(messages, session, options, cancellationToken)
                .GetAsyncEnumerator(cancellationToken);
            try
            {
                while (true)
                {
                    AgentResponseUpdate update;
                    try
                    {
                        if (!await enumerator.MoveNextAsync().ConfigureAwait(false))
                        {
                            break;
                        }

                        update = enumerator.Current;
                    }
                    catch (Exception ex)
                    {
                        failure = ex;
                        break;
                    }

                    yield return update;
                }
            }
            finally
            {
                await enumerator.DisposeAsync().ConfigureAwait(false);
            }

            stopwatch.Stop();
            if (failure is null)
            {
                ReportCompleted(state, stopwatch.Elapsed);
            }
            else
            {
                ReportFailed(state, failure);
            }
        }
        finally
        {
            runCoordinator.Restore(previous);
        }

        if (failure is not null)
        {
            throw failure;
        }
    }

    private FoundryHarnessProgressRunState BeginRun()
    {
        var parentReporter = progressAccessor.Current;
        var reporter = parentReporter.CreateChild(agentId);
        return new FoundryHarnessProgressRunState(
            reporter,
            parentReporter.AgentId,
            agentName);
    }

    private static void ReportInvoked(
        FoundryHarnessProgressRunState state)
    {
        state.Reporter.Report(new AgentInvokedEvent(
            Timestamp: DateTimeOffset.UtcNow,
            WorkflowId: state.Reporter.WorkflowId,
            AgentId: state.Reporter.AgentId,
            ParentAgentId: state.ParentAgentId,
            Depth: state.Reporter.Depth,
            SequenceNumber: state.Reporter.NextSequence(),
            AgentName: state.AgentName));
    }

    private static void ReportCompleted(
        FoundryHarnessProgressRunState state,
        TimeSpan duration)
    {
        state.Reporter.Report(new AgentCompletedEvent(
            Timestamp: DateTimeOffset.UtcNow,
            WorkflowId: state.Reporter.WorkflowId,
            AgentId: state.Reporter.AgentId,
            ParentAgentId: state.ParentAgentId,
            Depth: state.Reporter.Depth,
            SequenceNumber: state.Reporter.NextSequence(),
            AgentName: state.AgentName,
            Duration: duration,
            TotalTokens: state.TotalTokens,
            InputTokens: state.InputTokens,
            OutputTokens: state.OutputTokens,
            ToolCallCount: state.ToolCallCount));
    }

    private static void ReportFailed(
        FoundryHarnessProgressRunState state,
        Exception exception)
    {
        state.Reporter.Report(new AgentFailedEvent(
            Timestamp: DateTimeOffset.UtcNow,
            WorkflowId: state.Reporter.WorkflowId,
            AgentId: state.Reporter.AgentId,
            ParentAgentId: state.ParentAgentId,
            Depth: state.Reporter.Depth,
            SequenceNumber: state.Reporter.NextSequence(),
            AgentName: state.AgentName,
            ErrorMessage: exception.Message));
    }
}
