using System.Text.Json;

using Microsoft.Extensions.AI;

namespace AutonomousPhasePipelineApp.Core;

internal sealed class ResearchCoordinatorChatClient : IChatClient
{
    private const string StartEvidenceCallId = "research-start-evidence";
    private const string StartFeasibilityCallId = "research-start-feasibility";
    private const string WaitEvidenceCallId = "research-wait-evidence";
    private const string WaitFeasibilityCallId = "research-wait-feasibility";
    private const string GetEvidenceCallId = "research-get-evidence";
    private const string GetFeasibilityCallId = "research-get-feasibility";
    private const string ClearEvidenceCallId = "research-clear-evidence";
    private const string ClearFeasibilityCallId = "research-clear-feasibility";

    private readonly List<string> _invokedToolNames = [];
    private int _callCount;

    internal int CallCount => Volatile.Read(ref _callCount);

    internal IReadOnlyList<string> InvokedToolNames => _invokedToolNames;

    Task<ChatResponse> IChatClient.GetResponseAsync(
        IEnumerable<ChatMessage> chatMessages,
        ChatOptions? options,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Interlocked.Increment(ref _callCount);
        ChatMessage[] messages = [.. chatMessages];

        if (ScriptedFunctionCall.GetResultText(
            messages,
            StartEvidenceCallId) is null)
        {
            return Task.FromResult(
                Call(
                    StartEvidenceCallId,
                    "background_agents_start_task",
                    new Dictionary<string, object?>
                    {
                        ["agentName"] = "evidence-worker",
                        ["input"] = "Collect bounded evidence for the synthetic topic.",
                        ["description"] = "Evidence collection",
                    },
                    options));
        }

        if (ScriptedFunctionCall.GetResultText(
            messages,
            StartFeasibilityCallId) is null)
        {
            return Task.FromResult(
                Call(
                    StartFeasibilityCallId,
                    "background_agents_start_task",
                    new Dictionary<string, object?>
                    {
                        ["agentName"] = "feasibility-worker",
                        ["input"] = "Assess feasibility for the synthetic topic.",
                        ["description"] = "Feasibility analysis",
                    },
                    options));
        }

        if (ScriptedFunctionCall.GetResultText(
            messages,
            WaitEvidenceCallId) is null)
        {
            return Task.FromResult(
                Call(
                    WaitEvidenceCallId,
                    "background_agents_wait_for_first_completion",
                    new Dictionary<string, object?>
                    {
                        ["taskIds"] = new object?[] { 1 },
                    },
                    options));
        }

        if (ScriptedFunctionCall.GetResultText(
            messages,
            WaitFeasibilityCallId) is null)
        {
            return Task.FromResult(
                Call(
                    WaitFeasibilityCallId,
                    "background_agents_wait_for_first_completion",
                    new Dictionary<string, object?>
                    {
                        ["taskIds"] = new object?[] { 2 },
                    },
                    options));
        }

        if (ScriptedFunctionCall.GetResultText(
            messages,
            GetEvidenceCallId) is null)
        {
            return Task.FromResult(
                Call(
                    GetEvidenceCallId,
                    "background_agents_get_task_results",
                    new Dictionary<string, object?>
                    {
                        ["taskId"] = 1,
                    },
                    options));
        }

        if (ScriptedFunctionCall.GetResultText(
            messages,
            GetFeasibilityCallId) is null)
        {
            return Task.FromResult(
                Call(
                    GetFeasibilityCallId,
                    "background_agents_get_task_results",
                    new Dictionary<string, object?>
                    {
                        ["taskId"] = 2,
                    },
                    options));
        }

        if (ScriptedFunctionCall.GetResultText(
            messages,
            ClearEvidenceCallId) is null)
        {
            return Task.FromResult(
                Call(
                    ClearEvidenceCallId,
                    "background_agents_clear_completed_task",
                    new Dictionary<string, object?>
                    {
                        ["taskId"] = 1,
                    },
                    options));
        }

        if (ScriptedFunctionCall.GetResultText(
            messages,
            ClearFeasibilityCallId) is null)
        {
            return Task.FromResult(
                Call(
                    ClearFeasibilityCallId,
                    "background_agents_clear_completed_task",
                    new Dictionary<string, object?>
                    {
                        ["taskId"] = 2,
                    },
                    options));
        }

        string evidence = ScriptedFunctionCall.GetResultText(
            messages,
            GetEvidenceCallId) ?? string.Empty;
        string feasibility = ScriptedFunctionCall.GetResultText(
            messages,
            GetFeasibilityCallId) ?? string.Empty;
        if (!evidence.Contains(
                "evidence-complete",
                StringComparison.Ordinal) ||
            !feasibility.Contains(
                "feasibility-complete",
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Background research results were incomplete.");
        }

        string topic = messages
            .FirstOrDefault(message => message.Role == ChatRole.User)?
            .Text?
            .Split("topic=", 2, StringSplitOptions.None)
            .LastOrDefault()?
            .Trim() ?? "synthetic topic";
        string artifact = JsonSerializer.Serialize(
            new
            {
                topic,
                evidence = new[]
                {
                    "evidence-complete",
                    "feasibility-complete",
                },
            });
        return Task.FromResult(
            new ChatResponse(
                new ChatMessage(ChatRole.Assistant, artifact)));
    }

    IAsyncEnumerable<ChatResponseUpdate> IChatClient.GetStreamingResponseAsync(
        IEnumerable<ChatMessage> chatMessages,
        ChatOptions? options,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException(
            "Streaming is not required by the offline research coordinator.");

    object? IChatClient.GetService(Type serviceType, object? key) => null;

    void IDisposable.Dispose()
    {
    }

    private ChatResponse Call(
        string callId,
        string functionName,
        IDictionary<string, object?> arguments,
        ChatOptions? options)
    {
        _invokedToolNames.Add(functionName);
        return ScriptedFunctionCall.Create(
            callId,
            functionName,
            arguments,
            options);
    }
}
