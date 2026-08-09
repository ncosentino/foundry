using System.Runtime.CompilerServices;

using AutonomousPhasePipelineApp.Core;

using Microsoft.Extensions.AI;

namespace AutonomousPhasePipelineApp.Evaluation;

internal sealed class HostedRecordingChatClient(
    IChatClient innerClient,
    HostedEvaluationTelemetry telemetry,
    string agentId,
    bool isChild,
    ReferenceSynthesisBudget budget) : DelegatingChatClient(innerClient)
{
    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options,
        CancellationToken cancellationToken)
    {
        budget.ConsumeProviderCall(agentId);
        telemetry.RecordCallStarted(agentId, isChild);
        try
        {
            ChatResponse response = await base.GetResponseAsync(
                messages,
                options,
                cancellationToken);
            telemetry.RecordResponse(
                response,
                agentId,
                isChild);
            return response;
        }
        catch
        {
            telemetry.RecordFailure(isChild);
            throw;
        }
    }

    public override async IAsyncEnumerable<ChatResponseUpdate>
        GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options,
            [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        budget.ConsumeProviderCall(agentId);
        telemetry.RecordCallStarted(agentId, isChild);
        var updates = new List<ChatResponseUpdate>();
        bool completed = false;
        try
        {
            await foreach (ChatResponseUpdate update in base
                .GetStreamingResponseAsync(
                    messages,
                    options,
                    cancellationToken))
            {
                updates.Add(update);
                yield return update;
            }

            telemetry.RecordResponse(
                updates.ToChatResponse(),
                agentId,
                isChild);
            completed = true;
        }
        finally
        {
            if (!completed)
            {
                telemetry.RecordFailure(isChild);
            }
        }
    }
}
