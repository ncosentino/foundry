using AutonomousPhasePipelineApp.Core;

using Microsoft.Extensions.AI;

namespace AutonomousPhasePipelineApp.Evaluation;

internal sealed class HostedMagenticLedgerChatClient(
    IChatClient innerClient,
    MagenticPhaseProbe probe) : DelegatingChatClient(innerClient)
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
        if (MagenticScriptedResponses.TryParseLedger(
            response.Text ?? string.Empty,
            out MagenticLedgerSnapshot? snapshot) &&
            snapshot is not null)
        {
            probe.RecordLedgerSnapshot(snapshot);
        }

        return response;
    }
}
