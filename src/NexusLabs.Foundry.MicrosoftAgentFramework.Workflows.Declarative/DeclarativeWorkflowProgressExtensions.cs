using System.Runtime.CompilerServices;

using Microsoft.Agents.AI.Workflows;
using Microsoft.Agents.AI.Workflows.Declarative;

using NexusLabs.Foundry.MicrosoftAgentFramework.Progress;

using UpstreamWorkflowEvent = Microsoft.Agents.AI.Workflows.WorkflowEvent;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Workflows.Declarative;

/// <summary>
/// Bridges an upstream declarative workflow event stream onto Foundry progress reporting.
/// </summary>
public static class DeclarativeWorkflowProgressExtensions
{
    /// <summary>
    /// Reports each event to <paramref name="progressReporter"/> as it passes through, yielding the
    /// original event unchanged so the caller still sees the complete upstream stream.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is a pass-through rather than a terminal consumer because the upstream stream is
    /// single-consumption: a caller that needs both progress reporting and its own handling would
    /// otherwise have to choose between them.
    /// </para>
    /// <para>
    /// Events with no Foundry equivalent are forwarded without being reported rather than being
    /// mapped onto an approximate one, so a consumer never sees a progress event that claims more
    /// than the upstream stream actually said.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    public static async IAsyncEnumerable<UpstreamWorkflowEvent> ReportProgressTo(
        this IAsyncEnumerable<UpstreamWorkflowEvent> events,
        IProgressReporter progressReporter,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(progressReporter);

        await foreach (var workflowEvent in events
            .WithCancellation(cancellationToken)
            .ConfigureAwait(false))
        {
            var progressEvent = Map(workflowEvent, progressReporter);
            if (progressEvent is not null)
            {
                progressReporter.Report(progressEvent);
            }

            yield return workflowEvent;
        }
    }

    /// <remarks>
    /// Action lifecycle is taken from upstream's declarative events rather than from the generic
    /// executor events that accompany them, because only the declarative ones carry the action id
    /// and kind written in the document; the executor events identify a synthesized node instead.
    /// </remarks>
    private static IProgressEvent? Map(
        UpstreamWorkflowEvent workflowEvent,
        IProgressReporter reporter) => workflowEvent switch
        {
            DeclarativeActionInvokedEvent invoked => new DeclarativeActionStartedProgressEvent(
                DateTimeOffset.UtcNow,
                reporter.WorkflowId,
                reporter.AgentId,
                null,
                reporter.Depth,
                reporter.NextSequence(),
                invoked.ActionId,
                invoked.ActionType),
            DeclarativeActionCompletedEvent completed => new DeclarativeActionCompletedProgressEvent(
                DateTimeOffset.UtcNow,
                reporter.WorkflowId,
                reporter.AgentId,
                null,
                reporter.Depth,
                reporter.NextSequence(),
                completed.ActionId,
                completed.ActionType),
            // Checked before WorkflowOutputEvent, which it derives from, so streamed agent text is
            // reported as a response chunk rather than as a workflow output.
            AgentResponseUpdateEvent update => new AgentResponseChunkEvent(
                DateTimeOffset.UtcNow,
                reporter.WorkflowId,
                reporter.AgentId,
                null,
                reporter.Depth,
                reporter.NextSequence(),
                update.ExecutorId,
                update.Update.Text),
            SuperStepStartedEvent started => new SuperStepStartedProgressEvent(
                DateTimeOffset.UtcNow,
                reporter.WorkflowId,
                reporter.AgentId,
                null,
                reporter.Depth,
                reporter.NextSequence(),
                started.StepNumber),
            SuperStepCompletedEvent stepCompleted => new SuperStepCompletedProgressEvent(
                DateTimeOffset.UtcNow,
                reporter.WorkflowId,
                reporter.AgentId,
                null,
                reporter.Depth,
                reporter.NextSequence(),
                stepCompleted.StepNumber),
            WorkflowErrorEvent error => new DeclarativeWorkflowErrorProgressEvent(
                DateTimeOffset.UtcNow,
                reporter.WorkflowId,
                reporter.AgentId,
                null,
                reporter.Depth,
                reporter.NextSequence(),
                error.Exception?.Message ?? "The declarative workflow reported an error."),
            _ => null,
        };
}
