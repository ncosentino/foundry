using DeclarativeWorkflowApp;

using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Agents.AI.Workflows.Declarative;

using NexusLabs.Foundry.MicrosoftAgentFramework.Progress;
using NexusLabs.Foundry.MicrosoftAgentFramework.Workflows.Declarative;

// The workflow names two agents. Neither is deployed anywhere: they are ordinary in-process agents
// resolved by name, which is the whole point of this example. Nothing here reaches a network.
var agents = new Dictionary<string, AIAgent>(StringComparer.OrdinalIgnoreCase)
{
    ["Classifier"] = new ChatClientAgent(
        new ScriptedChatClient(report =>
            report.Contains("cannot log in", StringComparison.OrdinalIgnoreCase)
                ? "category: access"
                : "category: general"),
        name: "Classifier",
        instructions: "Classify the incoming report."),
    ["Responder"] = new ChatClientAgent(
        new ScriptedChatClient(report => $"Thanks for reporting: {report}"),
        name: "Responder",
        instructions: "Draft a reply to the incoming report."),
};

var provider = new FoundryAgentProvider(new DeclarativeWorkflowAgentRegistry(agents));
var factory = new FoundryDeclarativeWorkflowFactory(provider);

var workflowPath = Path.Combine(AppContext.BaseDirectory, "triage-workflow.yaml");
var workflowYaml = File.ReadAllText(workflowPath);

// Validation is deliberate rather than implicit: declarative workflows have no published schema, so
// parsing is the only check available and it is worth doing before a run rather than during one.
var validation = factory.Validate(workflowYaml);
if (!validation.IsValid)
{
    Console.WriteLine($"DeclarativeWorkflowApp:invalid:{validation.ErrorMessage}");
    return 1;
}

Workflow workflow = factory.Create(workflowYaml);

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
