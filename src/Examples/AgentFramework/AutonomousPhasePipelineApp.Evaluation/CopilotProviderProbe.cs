using AutonomousPhasePipelineApp.Core;
using System.Text.Json;

using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

using NexusLabs.Foundry.Copilot;

namespace AutonomousPhasePipelineApp.Evaluation;

internal static class CopilotProviderProbe
{
    internal static async Task<ProviderProbeResult> RunAsync(
        string model,
        string commitSha,
        CancellationToken cancellationToken)
    {
        string stage = "ordinary-response";
        string? observedModel = null;
        UsageDetails? usage = null;
        int toolCalls = 0;

        try
        {
            using var httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromMinutes(3),
            };
            using var chatClient = new CopilotChatClient(
                new CopilotChatClientOptions
                {
                    DefaultModel = model,
                    TokenSource = CopilotTokenSource.EnvironmentVariable,
                    MaxRetries = 0,
                },
                httpClient);
            ChatResponse ordinary = await chatClient.GetResponseAsync(
                [
                    new ChatMessage(
                        ChatRole.User,
                        "Reply with exactly PROVIDER_PROBE_OK."),
                ],
                options: null,
                cancellationToken);
            observedModel = ordinary.ModelId;
            usage = ordinary.Usage;
            if (ordinary.Text?.Contains(
                "PROVIDER_PROBE_OK",
                StringComparison.Ordinal) != true)
            {
                throw new InvalidOperationException(
                    "The provider ordinary-response probe returned unexpected text.");
            }

            stage = "tool-round-trip";
            AIFunction probeTool = AIFunctionFactory.Create(
                (string value) =>
                {
                    Interlocked.Increment(ref toolCalls);
                    return $"probe-tool-result:{value}";
                },
                new AIFunctionFactoryOptions
                {
                    Name = "evaluation_probe_tool",
                    Description =
                        "Returns a deterministic provider-probe value.",
                });
            AIAgent agent = ReferencePipelineFactory.CreateHarnessAgent(
                "hosted-provider-probe",
                "Call the probe tool exactly once before answering.",
                chatClient,
                tools: [probeTool],
                features: ReferencePipelineFactory.DisabledFeatures(),
                loopEvaluators: [],
                loopAgentOptions: null,
                backgroundAgents: [],
                backgroundOptions: null,
                maximumIterationsPerRequest: 4);
            AgentResponse toolResponse = await agent.RunAsync(
                """
                Call evaluation_probe_tool with value "ok".
                Then reply with exactly TOOL_PROBE_OK.
                """,
                cancellationToken: cancellationToken);
            usage ??= toolResponse.Usage;
            if (toolCalls != 1 ||
                toolResponse.Text?.Contains(
                    "TOOL_PROBE_OK",
                    StringComparison.Ordinal) != true)
            {
                throw new InvalidOperationException(
                    "The provider tool round-trip probe did not complete exactly once.");
            }

            if (string.IsNullOrWhiteSpace(observedModel) ||
                usage?.InputTokenCount is not > 0 ||
                usage.OutputTokenCount is not > 0)
            {
                throw new InvalidOperationException(
                    "The provider probe did not return model and token-usage evidence.");
            }

            return CreateResult(
                succeeded: true,
                stage: "completed",
                model,
                observedModel,
                usage,
                toolCalls,
                commitSha,
                error: null);
        }
        catch (OperationCanceledException exception) when (
            !cancellationToken.IsCancellationRequested)
        {
            return CreateResult(
                succeeded: false,
                stage,
                model,
                observedModel,
                usage,
                toolCalls,
                commitSha,
                exception);
        }
        catch (Exception exception) when (
            exception is CopilotAuthException
                or CopilotRateLimitException
                or HttpRequestException
                or InvalidOperationException
                or JsonException
                or TimeoutException)
        {
            return CreateResult(
                succeeded: false,
                stage,
                model,
                observedModel,
                usage,
                toolCalls,
                commitSha,
                exception);
        }
    }

    private static ProviderProbeResult CreateResult(
        bool succeeded,
        string stage,
        string requestedModel,
        string? observedModel,
        UsageDetails? usage,
        int toolCalls,
        string commitSha,
        Exception? error) =>
        new(
            SchemaVersion: 1,
            Succeeded: succeeded,
            Stage: stage,
            RequestedModel: requestedModel,
            ObservedModel: observedModel,
            InputTokens: usage?.InputTokenCount,
            OutputTokens: usage?.OutputTokenCount,
            CachedInputTokens:
                usage?.CachedInputTokenCount
                ?? usage?.AdditionalCounts?.GetValueOrDefault(
                    "CachedInputTokens"),
            ToolCalls: toolCalls,
            CommitSha: commitSha,
            CompletedAtUtc: DateTimeOffset.UtcNow,
            ErrorType: error?.GetType().Name,
            ErrorMessage: error?.Message);
}
