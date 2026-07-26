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
    internal const string AbandonedStreamingErrorMessage =
        "Streaming agent response enumeration ended before completion.";

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
        bool completedEnumeration = false;
        IAsyncEnumerator<AgentResponseUpdate>? enumerator = null;

        try
        {
            try
            {
                enumerator = base
                    .RunCoreStreamingAsync(messages, session, options, cancellationToken)
                    .GetAsyncEnumerator(cancellationToken);
            }
            catch (Exception ex)
            {
                failure = ex;
            }

            while (failure is null && enumerator is not null)
            {
                try
                {
                    if (!await enumerator.MoveNextAsync().ConfigureAwait(false))
                    {
                        completedEnumeration = true;
                        break;
                    }
                }
                catch (Exception ex)
                {
                    failure = ex;
                    break;
                }

                yield return enumerator.Current;
            }
        }
        finally
        {
            if (enumerator is not null)
            {
                try
                {
                    await enumerator.DisposeAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    failure ??= ex;
                }
            }

            stopwatch.Stop();
            try
            {
                if (failure is not null)
                {
                    ReportFailed(state, failure);
                }
                else if (completedEnumeration)
                {
                    ReportCompleted(state, stopwatch.Elapsed);
                }
                else
                {
                    ReportFailed(
                        state,
                        new InvalidOperationException(AbandonedStreamingErrorMessage));
                }
            }
            finally
            {
                runCoordinator.Restore(previous);
            }

            if (failure is not null)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo
                    .Capture(failure)
                    .Throw();
            }
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
