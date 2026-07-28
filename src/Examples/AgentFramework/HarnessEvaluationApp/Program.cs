using Microsoft.Extensions.AI;

using HarnessEvaluationApp;
using NexusLabs.Foundry.Copilot;

var options = HostedEvaluationOptions.Load(args);
Func<IChatClient>? realChatClientFactory = null;
if (!options.DryRun)
{
    var token = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
    if (string.IsNullOrWhiteSpace(token))
    {
        Console.Error.WriteLine("GITHUB_TOKEN is required for hosted execution.");
        return 2;
    }

    realChatClientFactory = () =>
        new CopilotChatClient(
            new CopilotChatClientOptions
            {
                DefaultModel = options.ModelId,
                GitHubToken = token,
                IntegrationId = "foundry-harness-evaluation",
                EditorVersion = "NexusLabs.Foundry.HarnessEvaluation/0.1.0",
            });
}

var driver = new HostedEvaluationDriver(options, realChatClientFactory);
var state = await driver.RunAsync(CancellationToken.None);
Console.WriteLine($"HarnessEvaluationApp:{state}:{options.OutputDirectory}");
return state == NexusLabs.Foundry.Evaluation.Harness.HarnessHostedRunState.InvalidInput
    ? 1
    : 0;
