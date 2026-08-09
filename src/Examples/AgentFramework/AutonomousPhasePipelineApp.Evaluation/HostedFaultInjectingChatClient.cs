using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;

using AutonomousPhasePipelineApp.Core;

using Microsoft.Extensions.AI;

namespace AutonomousPhasePipelineApp.Evaluation;

internal sealed class HostedFaultInjectingChatClient(
    IChatClient innerClient,
    HostedFaultPlan plan,
    HostedFaultState state,
    HostedEvaluationAgentRole role,
    HostedEvaluationTelemetry telemetry) : DelegatingChatClient(innerClient)
{
    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options,
        CancellationToken cancellationToken)
    {
        ChatResponse response = await base.GetResponseAsync(
            messages,
            options,
            cancellationToken);
        if (plan.Mode == HostedFaultMode.None ||
            plan.TargetRole != role)
        {
            return response;
        }

        bool ledger = MagenticScriptedResponses.TryParseLedger(
            response.Text ?? string.Empty,
            out _);
        if (ledger)
        {
            int ledgerIndex = state.IncrementLedger();
            if (plan.Mode == HostedFaultMode.ForceFirstMagenticStall &&
                plan.ActivationPoint ==
                    HostedFaultActivationPoint.AfterMagenticProgressLedger &&
                ledgerIndex == 1)
            {
                telemetry.RecordFaultActivated(role);
                return ReplaceText(
                    response,
                    MagenticScriptedResponses.CreateLedger(
                        isRequestSatisfied: false,
                        isInLoop: true,
                        isProgressBeingMade: false,
                        nextSpeaker:
                            MagenticPhaseFactory.ManifestAnalystName,
                        instruction:
                            "The current approach is ineffective."));
            }

            return response;
        }

        if (!IsTerminalResponse(response, role))
        {
            return response;
        }

        int terminal = state.IncrementTerminal();
        bool injectInvalid =
            plan.ActivationPoint ==
                HostedFaultActivationPoint.AfterTerminalProviderResponse &&
            (plan.Mode == HostedFaultMode.InvalidEveryTerminal ||
             plan.Mode == HostedFaultMode.InvalidMagenticFinal ||
             (plan.Mode == HostedFaultMode.InvalidFirstTerminal &&
              terminal == 1));
        if (injectInvalid)
        {
            telemetry.RecordFaultActivated(role);
            return ReplaceText(
                response,
                RemoveRecommendation(response.Text));
        }

        if (plan.Mode ==
                HostedFaultMode.DelayFirstTerminalUntilCanceled &&
            plan.ActivationPoint ==
                HostedFaultActivationPoint.AfterTerminalProviderResponse &&
            terminal == 1)
        {
            telemetry.RecordFaultActivated(role);
            await Task.Delay(
                Timeout.InfiniteTimeSpan,
                cancellationToken);
        }

        return response;
    }

    public override async IAsyncEnumerable<ChatResponseUpdate>
        GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options,
            [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ChatResponse response = await GetResponseAsync(
            messages,
            options,
            cancellationToken);
        foreach (ChatMessage message in response.Messages)
        {
            yield return new ChatResponseUpdate
            {
                Role = message.Role,
                AuthorName = message.AuthorName,
                Contents = [.. message.Contents],
            };
        }
    }

    private bool IsTerminalResponse(
        ChatResponse response,
        HostedEvaluationAgentRole currentRole)
    {
        if (HasFunctionCall(response) ||
            string.IsNullOrWhiteSpace(response.Text))
        {
            return false;
        }

        return currentRole != HostedEvaluationAgentRole.MagenticManager ||
            state.LedgerCount > 0;
    }

    private static bool HasFunctionCall(ChatResponse response) =>
        response.Messages
            .SelectMany(message => message.Contents)
            .OfType<FunctionCallContent>()
            .Any();

    private static string RemoveRecommendation(
        string? content)
    {
        if (!string.IsNullOrWhiteSpace(content))
        {
            string candidate = ExtractJsonObject(content);
            try
            {
                if (JsonNode.Parse(candidate) is JsonObject artifact)
                {
                    _ = artifact.Remove("recommendation");
                    return artifact.ToJsonString();
                }
            }
            catch (JsonException)
            {
            }
        }

        return
            """
            {
              "summary": "Injected invalid synthesis artifact.",
              "evidence": [],
              "gaps": []
            }
            """;
    }

    private static string ExtractJsonObject(
        string content)
    {
        string candidate = content.Trim();
        int objectStart = candidate.IndexOf('{');
        int objectEnd = candidate.LastIndexOf('}');
        return objectStart >= 0 && objectEnd > objectStart
            ? candidate[objectStart..(objectEnd + 1)]
            : candidate;
    }

    private static ChatResponse ReplaceText(
        ChatResponse response,
        string text) =>
        new(new ChatMessage(ChatRole.Assistant, text))
        {
            ModelId = response.ModelId,
            Usage = response.Usage,
            ResponseId = response.ResponseId,
            RawRepresentation = response.RawRepresentation,
            AdditionalProperties = response.AdditionalProperties,
        };
}
