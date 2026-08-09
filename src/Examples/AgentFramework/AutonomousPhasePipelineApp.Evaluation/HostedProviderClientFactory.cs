using AutonomousPhasePipelineApp.Core;

using Microsoft.Extensions.AI;

using NexusLabs.Foundry.Copilot;

namespace AutonomousPhasePipelineApp.Evaluation;

internal static class HostedProviderClientFactory
{
    internal static IChatClient Create(
        string model,
        HostedEvaluationTelemetry telemetry,
        string agentId,
        bool isChild,
        HostedFaultMode faultMode,
        MagenticPhaseProbe? ledgerProbe,
        ICollection<IDisposable> resources)
    {
        var httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(5),
        };
        var rawClient = new CopilotChatClient(
            new GitHubActionsCopilotTokenProvider(),
            new CopilotChatClientOptions
            {
                DefaultModel = model,
                MaxRetries = 0,
            },
            httpClient);
        resources.Add(rawClient);
        resources.Add(httpClient);

        IChatClient current = rawClient;
        if (faultMode != HostedFaultMode.None)
        {
            current = new HostedFaultInjectingChatClient(
                current,
                faultMode);
        }

        if (ledgerProbe is not null)
        {
            current = new HostedMagenticLedgerChatClient(
                current,
                ledgerProbe);
        }

        return new HostedRecordingChatClient(
            current,
            telemetry,
            agentId,
            isChild);
    }
}
