using GitHub.Copilot;

using Microsoft.Extensions.AI;

using HarnessEvaluationApp;

var options = HostedEvaluationOptions.Load(args);
Func<IChatClient>? realChatClientFactory = null;
CopilotClient? copilotClient = null;
string? copilotRuntimeDirectory = null;
try
{
    if (!options.DryRun)
    {
        var token = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
        if (string.IsNullOrWhiteSpace(token))
        {
            Console.Error.WriteLine("GITHUB_TOKEN is required for hosted execution.");
            return 2;
        }

        copilotRuntimeDirectory = Path.Combine(
            Path.GetTempPath(),
            $"foundry-harness-copilot-{Environment.GetEnvironmentVariable("GITHUB_RUN_ID") ?? Guid.NewGuid().ToString("N")}");
        Directory.CreateDirectory(copilotRuntimeDirectory);
        copilotClient = new CopilotClient(
            new CopilotClientOptions
            {
                Mode = CopilotClientMode.Empty,
                GitHubToken = token,
                UseLoggedInUser = false,
                BaseDirectory = copilotRuntimeDirectory,
                WorkingDirectory = copilotRuntimeDirectory,
                LogLevel = CopilotLogLevel.Error,
            });
        await copilotClient.StartAsync();
        var executor = new CopilotSdkTurnExecutor(
            copilotClient,
            copilotRuntimeDirectory);
        if (args.Contains("--provider-probe", StringComparer.Ordinal))
        {
            using var probeTimeout = new CancellationTokenSource(
                TimeSpan.FromSeconds(options.AttemptTimeoutSeconds));
            using var probeClient = new CopilotSdkChatClient(
                options.ModelId,
                executor.ExecuteAsync);
            await CopilotSdkProviderProbe.RunAsync(
                probeClient,
                probeTimeout.Token);
            Console.WriteLine("HarnessEvaluationApp:ProviderProbeSucceeded");
            return 0;
        }

        realChatClientFactory = () =>
            new CopilotSdkChatClient(options.ModelId, executor.ExecuteAsync);
    }

    var driver = new HostedEvaluationDriver(options, realChatClientFactory);
    var state = await driver.RunAsync(CancellationToken.None);
    Console.WriteLine($"HarnessEvaluationApp:{state}:{options.OutputDirectory}");
    return state == NexusLabs.Foundry.Evaluation.Harness.HarnessHostedRunState.InvalidInput
        ? 1
        : 0;
}
finally
{
    if (copilotClient is not null)
    {
        await copilotClient.DisposeAsync();
    }

    if (copilotRuntimeDirectory is not null &&
        Directory.Exists(copilotRuntimeDirectory))
    {
        Directory.Delete(copilotRuntimeDirectory, recursive: true);
    }
}
