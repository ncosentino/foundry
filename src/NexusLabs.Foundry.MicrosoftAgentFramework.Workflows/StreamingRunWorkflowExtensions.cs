using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using NexusLabs.Foundry.MicrosoftAgentFramework;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Workflows;

/// <summary>
/// Extension methods on <see cref="StreamingRun"/> and <see cref="Workflow"/> for
/// checkpoint-enabled execution and collecting agent responses.
/// </summary>
public static class StreamingRunWorkflowExtensions
{
    /// <summary>
    /// Starts a checkpoint-enabled agent workflow and sends the initial user message and
    /// <see cref="TurnToken"/> required by MAF agent workflow executors.
    /// </summary>
    /// <param name="workflow">The agent workflow to execute.</param>
    /// <param name="message">The initial user message.</param>
    /// <param name="checkpointManager">The caller-owned upstream checkpoint manager.</param>
    /// <param name="sessionId">
    /// An explicit workflow session identifier, or <see langword="null"/> to let MAF generate one.
    /// The returned <see cref="StreamingRun.SessionId"/> identifies checkpoints created for the run.
    /// </param>
    /// <param name="cancellationToken">Cancellation token for starting the run.</param>
    /// <returns>
    /// The raw upstream <see cref="StreamingRun"/>. The caller owns event consumption, checkpoint
    /// persistence, cancellation via <see cref="StreamingRun.CancelRunAsync"/>, restoration, and
    /// asynchronous disposal.
    /// </returns>
    public static Task<StreamingRun> StartCheckpointedAgentRunAsync(
        this Workflow workflow,
        string message,
        CheckpointManager checkpointManager,
        string? sessionId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        ArgumentException.ThrowIfNullOrEmpty(message);
        return workflow.StartCheckpointedAgentRunAsync(
            new ChatMessage(ChatRole.User, message),
            checkpointManager,
            sessionId,
            cancellationToken);
    }

    /// <summary>
    /// Starts a checkpoint-enabled agent workflow and sends the initial chat message and
    /// <see cref="TurnToken"/> required by MAF agent workflow executors.
    /// </summary>
    /// <param name="workflow">The agent workflow to execute.</param>
    /// <param name="message">The initial chat message.</param>
    /// <param name="checkpointManager">The caller-owned upstream checkpoint manager.</param>
    /// <param name="sessionId">
    /// An explicit workflow session identifier, or <see langword="null"/> to let MAF generate one.
    /// </param>
    /// <param name="cancellationToken">Cancellation token for starting the run.</param>
    /// <returns>
    /// The raw upstream <see cref="StreamingRun"/>. Watching with a canceled token stops only the
    /// event consumer; call <see cref="StreamingRun.CancelRunAsync"/> to cancel workflow execution.
    /// </returns>
    public static async Task<StreamingRun> StartCheckpointedAgentRunAsync(
        this Workflow workflow,
        ChatMessage message,
        CheckpointManager checkpointManager,
        string? sessionId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(checkpointManager);
        cancellationToken.ThrowIfCancellationRequested();

        var run = await InProcessExecution.RunStreamingAsync(
            workflow,
            message,
            checkpointManager,
            sessionId,
            cancellationToken);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            bool sent = await run.TrySendMessageAsync(new TurnToken(emitEvents: true));
            if (!sent)
            {
                throw new InvalidOperationException(
                    "The workflow did not accept the agent TurnToken required to begin execution.");
            }

            return run;
        }
        catch
        {
            await run.DisposeAsync();
            throw;
        }
    }

    /// <summary>
    /// Creates a new streaming run resumed from an upstream workflow checkpoint.
    /// </summary>
    /// <param name="workflow">
    /// A structurally compatible workflow definition with the same stable executor identifiers
    /// as the workflow that produced the checkpoint.
    /// </param>
    /// <param name="checkpoint">The checkpoint to restore.</param>
    /// <param name="checkpointManager">The manager and backing store that own the checkpoint.</param>
    /// <param name="cancellationToken">Cancellation token for resuming the run.</param>
    /// <returns>
    /// A raw upstream <see cref="StreamingRun"/> resumed from <paramref name="checkpoint"/>.
    /// No new initial message or <see cref="TurnToken"/> is sent.
    /// </returns>
    /// <exception cref="InvalidDataException">
    /// The checkpoint does not belong to a structurally compatible workflow.
    /// </exception>
    public static async Task<StreamingRun> ResumeCheckpointedAgentRunAsync(
        this Workflow workflow,
        CheckpointInfo checkpoint,
        CheckpointManager checkpointManager,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        ArgumentNullException.ThrowIfNull(checkpoint);
        ArgumentNullException.ThrowIfNull(checkpointManager);
        cancellationToken.ThrowIfCancellationRequested();

        return await InProcessExecution.ResumeStreamingAsync(
            workflow,
            checkpoint,
            checkpointManager,
            cancellationToken);
    }

    /// <summary>
    /// Creates a streaming execution of the workflow, sends the message, and collects all agent responses.
    /// </summary>
    /// <param name="workflow">The workflow to execute.</param>
    /// <param name="message">The user message to send to the workflow.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>
    /// A dictionary mapping each agent's executor ID to its complete response text.
    /// Agents that emitted no text produce no entry.
    /// </returns>
    public static async Task<IReadOnlyDictionary<string, string>> RunAsync(
        this Workflow workflow,
        string message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        ArgumentException.ThrowIfNullOrEmpty(message);
        return await workflow.RunAsync(new ChatMessage(ChatRole.User, message), cancellationToken);
    }

    /// <summary>
    /// Creates a streaming execution of the workflow, sends the message, collects all agent
    /// responses, and stops early when any termination condition is met.
    /// </summary>
    /// <param name="workflow">The workflow to execute.</param>
    /// <param name="message">The user message to send to the workflow.</param>
    /// <param name="terminationConditions">
    /// Conditions evaluated after each completed agent turn. The first condition that returns
    /// <see langword="true"/> causes the loop to stop and remaining responses to be discarded.
    /// Pass an empty collection (or <see langword="null"/>) to disable Layer 2 termination.
    /// </param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>
    /// A dictionary mapping each agent's executor ID to its complete response text up to the
    /// point of termination. Agents that emitted no text produce no entry.
    /// </returns>
    public static async Task<IReadOnlyDictionary<string, string>> RunAsync(
        this Workflow workflow,
        string message,
        IReadOnlyList<IWorkflowTerminationCondition>? terminationConditions,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        ArgumentException.ThrowIfNullOrEmpty(message);
        return await workflow.RunAsync(
            new ChatMessage(ChatRole.User, message),
            terminationConditions,
            cancellationToken);
    }

    /// <summary>
    /// Creates a streaming execution of the workflow, sends the message, and collects all agent responses.
    /// </summary>
    /// <param name="workflow">The workflow to execute.</param>
    /// <param name="message">The chat message to send to the workflow.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>
    /// A dictionary mapping each agent's executor ID to its complete response text.
    /// Agents that emitted no text produce no entry.
    /// </returns>
    public static async Task<IReadOnlyDictionary<string, string>> RunAsync(
        this Workflow workflow,
        ChatMessage message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        ArgumentNullException.ThrowIfNull(message);
        await using var run = await InProcessExecution.RunStreamingAsync(workflow, message, cancellationToken: cancellationToken);
        await run.TrySendMessageAsync(new TurnToken(emitEvents: true));

        // Register CancelRunAsync on the cancellation token so the workflow
        // actually stops (e.g., when a token budget is exceeded).
        await using var registration = cancellationToken.CanBeCanceled
            ? cancellationToken.Register(() => _ = run.CancelRunAsync())
            : default(CancellationTokenRegistration?);

        var result = await run.CollectAgentResponsesAsync(cancellationToken);

        // If the cancellation token fired during execution (e.g., budget exceeded),
        // throw now. MAF may have swallowed the cancellation internally.
        cancellationToken.ThrowIfCancellationRequested();

        return result;
    }

    /// <summary>
    /// Creates a streaming execution of the workflow, sends the message, collects all agent
    /// responses, and stops early when any termination condition is met.
    /// </summary>
    /// <param name="workflow">The workflow to execute.</param>
    /// <param name="message">The chat message to send to the workflow.</param>
    /// <param name="terminationConditions">
    /// Conditions evaluated after each completed agent turn. The first condition that returns
    /// <see langword="true"/> causes the loop to stop and remaining responses to be discarded.
    /// Pass an empty collection (or <see langword="null"/>) to disable Layer 2 termination.
    /// </param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>
    /// A dictionary mapping each agent's executor ID to its complete response text up to the
    /// point of termination. Agents that emitted no text produce no entry.
    /// </returns>
    public static async Task<IReadOnlyDictionary<string, string>> RunAsync(
        this Workflow workflow,
        ChatMessage message,
        IReadOnlyList<IWorkflowTerminationCondition>? terminationConditions,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        ArgumentNullException.ThrowIfNull(message);
        await using var run = await InProcessExecution.RunStreamingAsync(workflow, message, cancellationToken: cancellationToken);
        await run.TrySendMessageAsync(new TurnToken(emitEvents: true));

        await using var registration = cancellationToken.CanBeCanceled
            ? cancellationToken.Register(() => _ = run.CancelRunAsync())
            : default(CancellationTokenRegistration?);

        if (terminationConditions is null || terminationConditions.Count == 0)
        {
            var result = await run.CollectAgentResponsesAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            return result;
        }

        var terminationResult = await CollectWithTerminationAsync(
            run.WatchStreamAsync(cancellationToken),
            terminationConditions,
            cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return terminationResult;
    }

    /// <summary>
    /// Collects all agent response text from a streaming run, grouped by executor ID.
    /// </summary>
    /// <returns>
    /// A dictionary mapping each agent's executor ID to its complete response text.
    /// Agents that emitted no text produce no entry.
    /// </returns>
    public static Task<IReadOnlyDictionary<string, string>> CollectAgentResponsesAsync(
        this StreamingRun run,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(run);
        return CollectFromEventsAsync(run.WatchStreamAsync(cancellationToken));
    }

    internal static async Task<IReadOnlyDictionary<string, string>> CollectFromEventsAsync(
        IAsyncEnumerable<WorkflowEvent> events)
    {
        var responses = new Dictionary<string, System.Text.StringBuilder>();

        await foreach (var evt in events)
        {
            if (evt is AgentResponseUpdateEvent update
                && update.ExecutorId is not null
                && update.Data is not null)
            {
                var text = update.Data.ToString();
                if (string.IsNullOrEmpty(text))
                    continue;

                if (!responses.TryGetValue(update.ExecutorId, out var sb))
                    responses[update.ExecutorId] = sb = new System.Text.StringBuilder();

                sb.Append(text);
            }
        }

        return responses.ToDictionary(
            kv => kv.Key,
            kv => kv.Value.ToString());
    }

    internal static async Task<IReadOnlyDictionary<string, string>> CollectWithTerminationAsync(
        IAsyncEnumerable<WorkflowEvent> events,
        IReadOnlyList<IWorkflowTerminationCondition> conditions,
        CancellationToken cancellationToken)
    {
        var responses = new Dictionary<string, System.Text.StringBuilder>();
        var history = new List<ChatMessage>();
        var turnCount = 0;

        string? currentExecutorId = null;

        await foreach (var evt in events.WithCancellation(cancellationToken))
        {
            if (evt is not AgentResponseUpdateEvent update
                || update.ExecutorId is null
                || update.Data is null)
            {
                continue;
            }

            var text = update.Data.ToString();
            if (string.IsNullOrEmpty(text))
                continue;

            // Detect executor change — previous agent's turn is complete
            if (currentExecutorId is not null
                && currentExecutorId != update.ExecutorId
                && responses.TryGetValue(currentExecutorId, out var completedSb))
            {
                var responseText = completedSb.ToString();
                turnCount++;
                var completedMessage = new ChatMessage(ChatRole.Assistant, responseText);
                history.Add(completedMessage);

                var ctx = new TerminationContext
                {
                    AgentId = currentExecutorId,
                    LastMessage = completedMessage,
                    TurnCount = turnCount,
                    ConversationHistory = history,
                };

                if (ShouldTerminate(ctx, conditions))
                    return FinalizeResponses(responses);
            }

            currentExecutorId = update.ExecutorId;

            if (!responses.TryGetValue(update.ExecutorId, out var sb))
                responses[update.ExecutorId] = sb = new System.Text.StringBuilder();

            sb.Append(text);
        }

        // Check the last agent's turn
        if (currentExecutorId is not null
            && responses.TryGetValue(currentExecutorId, out var lastSb))
        {
            var responseText = lastSb.ToString();
            turnCount++;
            var lastMessage = new ChatMessage(ChatRole.Assistant, responseText);
            history.Add(lastMessage);

            var ctx = new TerminationContext
            {
                AgentId = currentExecutorId,
                LastMessage = lastMessage,
                TurnCount = turnCount,
                ConversationHistory = history,
            };

            ShouldTerminate(ctx, conditions); // evaluate but don't stop — stream already ended
        }

        return FinalizeResponses(responses);
    }

    private static bool ShouldTerminate(
        TerminationContext ctx,
        IReadOnlyList<IWorkflowTerminationCondition> conditions)
    {
        foreach (var condition in conditions)
        {
            if (condition.ShouldTerminate(ctx))
                return true;
        }
        return false;
    }

    private static IReadOnlyDictionary<string, string> FinalizeResponses(
        Dictionary<string, System.Text.StringBuilder> responses)
        => responses.ToDictionary(kv => kv.Key, kv => kv.Value.ToString());
}
