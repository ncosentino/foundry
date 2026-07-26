using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

using NexusLabs.Foundry.MicrosoftAgentFramework.Progress;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Bundle;

internal sealed class FoundryHarnessTelemetryComposition
{
    private readonly IProgressReporterAccessor? _progressAccessor;
    private readonly FoundryHarnessProgressRunCoordinator _runCoordinator = new();

    private FoundryHarnessTelemetryComposition(
        IProgressReporterAccessor? progressAccessor)
    {
        _progressAccessor = progressAccessor;
    }

    internal static FoundryHarnessTelemetryComposition Create(
        FoundryHarnessAgentConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return new FoundryHarnessTelemetryComposition(configuration.ProgressAccessor);
    }

    internal IChatClient ComposeChatClient(IChatClient chatClient)
    {
        ArgumentNullException.ThrowIfNull(chatClient);
        return _progressAccessor is null
            ? chatClient
            : new FoundryHarnessProgressChatClient(chatClient, _runCoordinator);
    }

    internal AIAgent ComposeAgent(AIAgent agent)
    {
        ArgumentNullException.ThrowIfNull(agent);
        if (_progressAccessor is null)
        {
            return agent;
        }

        var functionInvokingChatClient = agent.GetService<FunctionInvokingChatClient>();
        if (functionInvokingChatClient is null)
        {
            throw new InvalidOperationException(
                "The upstream Harness bundle did not expose its FunctionInvokingChatClient. " +
                "Foundry progress composition cannot observe tool calls without taking loop " +
                "ownership, so construction failed closed.");
        }

        var existingFunctionInvoker = functionInvokingChatClient.FunctionInvoker;
        functionInvokingChatClient.FunctionInvoker = async (context, cancellationToken) =>
        {
            var state = _runCoordinator.Current;
            if (state is null)
            {
                return existingFunctionInvoker is null
                    ? await context.Function
                        .InvokeAsync(context.Arguments, cancellationToken)
                        .ConfigureAwait(false)
                    : await existingFunctionInvoker(context, cancellationToken)
                        .ConfigureAwait(false);
            }

            string toolName = context.Function.Name;
            var startedAt = DateTimeOffset.UtcNow;
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            state.IncrementToolCallCount();
            state.Reporter.Report(new ToolCallStartedEvent(
                Timestamp: startedAt,
                WorkflowId: state.Reporter.WorkflowId,
                AgentId: state.Reporter.AgentId,
                ParentAgentId: state.ParentAgentId,
                Depth: state.Reporter.Depth,
                SequenceNumber: state.Reporter.NextSequence(),
                ToolName: toolName));

            try
            {
                var result = existingFunctionInvoker is null
                    ? await context.Function
                        .InvokeAsync(context.Arguments, cancellationToken)
                        .ConfigureAwait(false)
                    : await existingFunctionInvoker(context, cancellationToken)
                        .ConfigureAwait(false);
                stopwatch.Stop();
                state.Reporter.Report(new ToolCallCompletedEvent(
                    Timestamp: DateTimeOffset.UtcNow,
                    WorkflowId: state.Reporter.WorkflowId,
                    AgentId: state.Reporter.AgentId,
                    ParentAgentId: state.ParentAgentId,
                    Depth: state.Reporter.Depth,
                    SequenceNumber: state.Reporter.NextSequence(),
                    ToolName: toolName,
                    Duration: stopwatch.Elapsed,
                    CustomMetrics: null));
                return result;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                state.Reporter.Report(new ToolCallFailedEvent(
                    Timestamp: DateTimeOffset.UtcNow,
                    WorkflowId: state.Reporter.WorkflowId,
                    AgentId: state.Reporter.AgentId,
                    ParentAgentId: state.ParentAgentId,
                    Depth: state.Reporter.Depth,
                    SequenceNumber: state.Reporter.NextSequence(),
                    ToolName: toolName,
                    ErrorMessage: ex.Message,
                    Duration: stopwatch.Elapsed));
                throw;
            }
        };

        string agentId = agent.Id ?? agent.Name ?? "harness-agent";
        string agentName = agent.Name ?? agent.Id ?? "harness-agent";
        return new FoundryHarnessProgressAgent(
            agent,
            _progressAccessor,
            _runCoordinator,
            agentId,
            agentName);
    }
}
