using Microsoft.Extensions.AI;

using OpenAI;

using System.ClientModel;

using HarnessEvaluationApp;

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
        new OpenAIClient(
                new ApiKeyCredential(token),
                new OpenAIClientOptions
                {
                    Endpoint = new Uri("https://models.github.ai/inference"),
                })
            .GetChatClient(options.ModelId)
            .AsIChatClient();
}

var driver = new HostedEvaluationDriver(options, realChatClientFactory);
var state = await driver.RunAsync(CancellationToken.None);
Console.WriteLine($"HarnessEvaluationApp:{state}:{options.OutputDirectory}");
return state == NexusLabs.Foundry.Evaluation.Harness.HarnessHostedRunState.InvalidInput
    ? 1
    : 0;
