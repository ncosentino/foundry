using System.Diagnostics;
using System.Runtime.CompilerServices;

using Microsoft.Extensions.AI;

using NexusLabs.Foundry.MicrosoftAgentFramework.Progress;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Bundle;

internal sealed class FoundryHarnessProgressChatClient(
    IChatClient innerClient,
    FoundryHarnessProgressRunCoordinator runCoordinator) : DelegatingChatClient(innerClient)
{
    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var state = runCoordinator.Current;
        if (state is null)
        {
            return await base
                .GetResponseAsync(messages, options, cancellationToken)
                .ConfigureAwait(false);
        }

        int callSequence = state.NextLlmCallSequence();
        var startedAt = DateTimeOffset.UtcNow;
        var stopwatch = Stopwatch.StartNew();
        state.Reporter.Report(new LlmCallStartedEvent(
            Timestamp: startedAt,
            WorkflowId: state.Reporter.WorkflowId,
            AgentId: state.Reporter.AgentId,
            ParentAgentId: state.ParentAgentId,
            Depth: state.Reporter.Depth,
            SequenceNumber: state.Reporter.NextSequence(),
            CallSequence: callSequence));

        try
        {
            var response = await base
                .GetResponseAsync(messages, options, cancellationToken)
                .ConfigureAwait(false);
            stopwatch.Stop();
            ReportCompleted(state, callSequence, response, stopwatch.Elapsed);
            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            ReportFailed(state, callSequence, ex, stopwatch.Elapsed);
            throw;
        }
    }

    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var state = runCoordinator.Current;
        if (state is null)
        {
            await foreach (var update in base
                .GetStreamingResponseAsync(messages, options, cancellationToken)
                .WithCancellation(cancellationToken)
                .ConfigureAwait(false))
            {
                yield return update;
            }

            yield break;
        }

        int callSequence = state.NextLlmCallSequence();
        var startedAt = DateTimeOffset.UtcNow;
        var stopwatch = Stopwatch.StartNew();
        state.Reporter.Report(new LlmCallStartedEvent(
            Timestamp: startedAt,
            WorkflowId: state.Reporter.WorkflowId,
            AgentId: state.Reporter.AgentId,
            ParentAgentId: state.ParentAgentId,
            Depth: state.Reporter.Depth,
            SequenceNumber: state.Reporter.NextSequence(),
            CallSequence: callSequence));

        var buffered = new List<ChatResponseUpdate>();
        Exception? failure = null;
        var enumerator = base
            .GetStreamingResponseAsync(messages, options, cancellationToken)
            .GetAsyncEnumerator(cancellationToken);
        try
        {
            while (true)
            {
                ChatResponseUpdate update;
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

                buffered.Add(update);
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
            ReportCompleted(
                state,
                callSequence,
                buffered.ToChatResponse(),
                stopwatch.Elapsed);
            yield break;
        }

        ReportFailed(state, callSequence, failure, stopwatch.Elapsed);
        throw failure;
    }

    private static void ReportCompleted(
        FoundryHarnessProgressRunState state,
        int callSequence,
        ChatResponse response,
        TimeSpan duration)
    {
        long inputTokens = response.Usage?.InputTokenCount ?? 0;
        long outputTokens = response.Usage?.OutputTokenCount ?? 0;
        long totalTokens = response.Usage?.TotalTokenCount ?? inputTokens + outputTokens;
        state.AddUsage(inputTokens, outputTokens, totalTokens);
        state.Reporter.Report(new LlmCallCompletedEvent(
            Timestamp: DateTimeOffset.UtcNow,
            WorkflowId: state.Reporter.WorkflowId,
            AgentId: state.Reporter.AgentId,
            ParentAgentId: state.ParentAgentId,
            Depth: state.Reporter.Depth,
            SequenceNumber: state.Reporter.NextSequence(),
            CallSequence: callSequence,
            Model: response.ModelId ?? "unknown",
            Duration: duration,
            InputTokens: inputTokens,
            OutputTokens: outputTokens,
            TotalTokens: totalTokens));
    }

    private static void ReportFailed(
        FoundryHarnessProgressRunState state,
        int callSequence,
        Exception exception,
        TimeSpan duration)
    {
        state.Reporter.Report(new LlmCallFailedEvent(
            Timestamp: DateTimeOffset.UtcNow,
            WorkflowId: state.Reporter.WorkflowId,
            AgentId: state.Reporter.AgentId,
            ParentAgentId: state.ParentAgentId,
            Depth: state.Reporter.Depth,
            SequenceNumber: state.Reporter.NextSequence(),
            CallSequence: callSequence,
            ErrorMessage: exception.Message,
            Duration: duration));
    }
}
