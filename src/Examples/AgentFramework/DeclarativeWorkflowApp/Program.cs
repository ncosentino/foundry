using DeclarativeWorkflowApp;

using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Foundry.MicrosoftAgentFramework;
using NexusLabs.Foundry.MicrosoftAgentFramework.Progress;
using NexusLabs.Foundry.MicrosoftAgentFramework.Workflows.Declarative;

// The workflow names two agents. Neither is deployed anywhere: both are declared with
// [FoundryAgent] and resolved through the same agent factory the rest of Foundry uses, so this
// example never reaches a network.
var services = new ServiceCollection();
services.AddFoundryAgentFramework(builder => builder
    .UsingChatClient(new ScriptedChatClient())
    .AddAgent<ClassifierAgent>()
    .AddAgent<ResponderAgent>());

using var serviceProvider = services.BuildServiceProvider();
var agentFactory = serviceProvider.GetRequiredService<IAgentFactory>();

var workflowPath = Path.Combine(AppContext.BaseDirectory, "triage-workflow.yaml");
var workflowYaml = File.ReadAllText(workflowPath);

// Validation is deliberate rather than implicit: declarative workflows have no published schema, so
// parsing is the only check available and it is worth doing before a run rather than during one.
var validation = agentFactory.ValidateDeclarativeWorkflow(workflowYaml);
if (!validation.IsValid)
{
    Console.WriteLine($"DeclarativeWorkflowApp:invalid:{validation.ErrorMessage}");
    return 1;
}

Workflow workflow = agentFactory.CreateDeclarativeWorkflow(workflowYaml);

var reporter = new ConsoleProgressReporter("declarative-triage");
StreamingRun run = await InProcessExecution.RunStreamingAsync(
    workflow,
    "I cannot log in to my account",
    checkpointManager: CheckpointManager.CreateInMemory());

var agentReplies = new List<string>();
await foreach (var workflowEvent in run
    .WatchStreamAsync()
    .ReportProgressTo(reporter, CancellationToken.None))
{
    if (workflowEvent is AgentResponseEvent response)
    {
        agentReplies.Add(response.Response.Text);
    }
}

foreach (var reply in agentReplies)
{
    Console.WriteLine($"DeclarativeWorkflowApp:agent-reply:{reply}");
}

Console.WriteLine($"DeclarativeWorkflowApp:actions:{reporter.CompletedActionCount}");
Console.WriteLine("DeclarativeWorkflowApp:completed");
return 0;

/// <summary>
/// Prints each declarative action as it starts, so the run is observable through Foundry progress
/// rather than only through the upstream event stream.
/// </summary>
internal sealed class ConsoleProgressReporter(string workflowId) : IProgressReporter
{
    private long _sequence;
    private int _completedActions;

    public string WorkflowId { get; } = workflowId;

    public string? AgentId => null;

    public int Depth => 0;

    public int CompletedActionCount => _completedActions;

    public void Report(IProgressEvent progressEvent)
    {
        switch (progressEvent)
        {
            case DeclarativeActionStartedProgressEvent started:
                Console.WriteLine(
                    $"DeclarativeWorkflowApp:action-started:{started.ActionId}:{started.ActionType}");
                break;
            case DeclarativeActionCompletedProgressEvent:
                Interlocked.Increment(ref _completedActions);
                break;
            case DeclarativeWorkflowErrorProgressEvent error:
                Console.WriteLine($"DeclarativeWorkflowApp:error:{error.ErrorMessage}");
                break;
        }
    }

    public IProgressReporter CreateChild(string agentId) => this;

    public long NextSequence() => Interlocked.Increment(ref _sequence);
}
