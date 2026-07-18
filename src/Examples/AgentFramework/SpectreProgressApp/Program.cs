using Azure;
using Azure.AI.OpenAI;

using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Foundry.MicrosoftAgentFramework;
using NexusLabs.Foundry.MicrosoftAgentFramework.Context;
using NexusLabs.Foundry.MicrosoftAgentFramework.Diagnostics;
using NexusLabs.Foundry.MicrosoftAgentFramework.Progress;
using NexusLabs.Foundry.MicrosoftAgentFramework.Workflows;
using NexusLabs.Foundry.MicrosoftAgentFramework.Workflows.Diagnostics;
using NexusLabs.Foundry.MicrosoftAgentFramework.Workflows.Middleware;
using NexusLabs.Foundry.Needlr.MicrosoftAgentFramework;
using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.Reflection;
using NexusLabs.Needlr.Injection.SourceGen;

using SimpleAgentFrameworkApp.Agents.Generated;

using Spectre.Console;

using SpectreProgressApp;

// ── Configuration ──────────────────────────────────────────────
var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true)
    .AddJsonFile("appsettings.Development.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

var azureSection = configuration.GetSection("AzureOpenAI");
IChatClient chatClient = new AzureOpenAIClient(
        new Uri(azureSection["Endpoint"]
            ?? throw new InvalidOperationException("No AzureOpenAI:Endpoint set")),
        new AzureKeyCredential(azureSection["ApiKey"]
            ?? throw new InvalidOperationException("No AzureOpenAI:ApiKey set")))
    .GetChatClient(azureSection["DeploymentName"]
        ?? throw new InvalidOperationException("No AzureOpenAI:DeploymentName set"))
    .AsIChatClient();

// ── DI Setup ───────────────────────────────────────────────────
var serviceProvider = new Syringe()
    .UsingReflection()
    .UsingAssemblyProvider(b => b.MatchingAssemblies(path =>
        path.Contains("SimpleAgentFrameworkApp", StringComparison.OrdinalIgnoreCase)).Build())
    .UsingAgentFramework(af => af
        .UsingChatClient(chatClient)
        .UsingToolResultMiddleware()
        .UsingResilience()
        .UsingDiagnostics())
    .BuildServiceProvider(configuration);

var workflowFactory = serviceProvider.GetRequiredService<IWorkflowFactory>();
var contextAccessor = serviceProvider.GetRequiredService<IAgentExecutionContextAccessor>();
var diagnosticsAccessor = serviceProvider.GetRequiredService<IAgentDiagnosticsAccessor>();
var completionCollector = serviceProvider.GetRequiredService<IChatCompletionCollector>();
var progressFactory = serviceProvider.GetRequiredService<IProgressReporterFactory>();
var progressAccessor = serviceProvider.GetRequiredService<IProgressReporterAccessor>();

// ── Header ─────────────────────────────────────────────────────
AnsiConsole.Write(new FigletText("Needlr MAF").Color(Color.Cyan1));
AnsiConsole.MarkupLine("[grey]Real-time agent orchestration dashboard[/]");
AnsiConsole.WriteLine();

// ── Questions ──────────────────────────────────────────────────
var questions = new[]
{
    "Which countries has Nick lived in and what are his favorite cities?",
    "What are Nick's hobbies and does he like ice cream?",
};

var executionContext = new AgentExecutionContext(
    UserId: "spectre-demo",
    OrchestrationId: $"spectre-{Guid.NewGuid():N}");

using (contextAccessor.BeginScope(executionContext))
{
    var handoffWorkflow = workflowFactory.CreateTriageHandoffWorkflow();

    foreach (var question in questions)
    {
        AnsiConsole.MarkupLine($"\n[bold yellow]Q: {Markup.Escape(question)}[/]\n");

        var dashboard = new DashboardSink();
        var reporter = progressFactory.Create(
            $"q-{Guid.NewGuid():N}",
            [dashboard]);

        IPipelineRunResult? result = null;

        await AnsiConsole.Live(dashboard.Render())
            .AutoClear(false)
            .Overflow(VerticalOverflow.Ellipsis)
            .StartAsync(async ctx =>
            {
                dashboard.SetContext(ctx);

                // Background tick — refreshes the dashboard every 500ms so elapsed
                // time updates and spinner frames animate even between LLM calls.
                using var tickCts = new CancellationTokenSource();
                var ticker = Task.Run(async () =>
                {
                    while (!tickCts.Token.IsCancellationRequested)
                    {
                        await Task.Delay(100, tickCts.Token).ConfigureAwait(false);
                        dashboard.Tick();
                    }
                }, tickCts.Token);

                result = await handoffWorkflow.RunWithDiagnosticsAsync(
                    question, diagnosticsAccessor, reporter, completionCollector, progressAccessor);

                tickCts.Cancel();
                try { await ticker; } catch (OperationCanceledException) { }
            });

        // Print final responses below the dashboard
        if (result is not null)
        {
            foreach (var stage in result.Stages)
            {
                AnsiConsole.MarkupLine($"  [green]{Markup.Escape(ShortName(stage.AgentName))}[/]: {Markup.Escape((stage.FinalResponse?.Text ?? string.Empty).Trim())}");
            }

            if (result.AggregateTokenUsage is { } t)
            {
                AnsiConsole.MarkupLine($"  [dim]{t.TotalTokens} tokens, {result.TotalDuration.TotalSeconds:F1}s[/]");
            }
        }
    }
}

AnsiConsole.WriteLine();
AnsiConsole.MarkupLine("[bold green]✓ All orchestrations complete.[/]");

static string ShortName(string id)
{
    var idx = id.IndexOf('_');
    return idx > 0 ? id[..idx] : id;
}
