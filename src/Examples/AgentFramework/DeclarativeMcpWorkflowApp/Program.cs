using DeclarativeMcpWorkflowApp;

using Microsoft.Agents.AI.Workflows;
using Microsoft.Agents.AI.Workflows.Declarative.Mcp;
using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Foundry.MicrosoftAgentFramework;
using NexusLabs.Foundry.MicrosoftAgentFramework.Progress;
using NexusLabs.Foundry.MicrosoftAgentFramework.Workflows.Declarative;

// ============================================================================
// Declarative MCP Workflow Example
//
// Runs a declarative workflow whose document calls a tool on a real MCP server, then
// hands off to a Foundry-declared agent.
//
// Start the server first — this example does not launch one:
//
//   PORT=3111 npx -y @modelcontextprotocol/server-everything streamableHttp
//
// Override the URL with McpServerUrl=... if you run it elsewhere.
// ============================================================================

var serverUrl = Environment.GetEnvironmentVariable("McpServerUrl")
    ?? "http://localhost:3111/mcp";

var services = new ServiceCollection();
services.AddFoundryAgentFramework(builder => builder
    .UsingChatClient(new ScriptedChatClient())
    .AddAgent<EchoSummaryAgent>());

using var serviceProvider = services.BuildServiceProvider();
var agentFactory = serviceProvider.GetRequiredService<IAgentFactory>();

var workflowPath = Path.Combine(AppContext.BaseDirectory, "mcp-workflow.yaml");
var workflowYaml = File.ReadAllText(workflowPath).Replace(
    "http://localhost:3111/mcp", serverUrl, StringComparison.Ordinal);

// The handler is supplied by the host, never defaulted. Which servers may be reached and what
// credentials travel with a request are decisions this example is making on purpose, and the
// package deliberately has no opinion about them.
await using var mcpToolHandler = new DefaultMcpToolHandler();

var handlers = new DeclarativeWorkflowHandlers
{
    McpToolHandler = mcpToolHandler,
    HttpRequestHandler = null,
};

Workflow workflow;
try
{
    workflow = agentFactory.CreateDeclarativeWorkflow(workflowYaml, handlers);
}
catch (DeclarativeWorkflowParseException ex)
{
    Console.WriteLine($"DeclarativeMcpWorkflowApp:invalid:{ex.Message}");
    return 1;
}

var reporter = new ConsoleProgressReporter("declarative-mcp");
StreamingRun run = await InProcessExecution.RunStreamingAsync(
    workflow,
    "hello from a declarative workflow",
    checkpointManager: CheckpointManager.CreateInMemory());

var agentReplies = new List<string>();
var failures = new List<string>();

await foreach (var workflowEvent in run
    .WatchStreamAsync()
    .ReportProgressTo(reporter, CancellationToken.None))
{
    switch (workflowEvent)
    {
        case AgentResponseEvent response:
            agentReplies.Add(response.Response.Text);
            break;
        case WorkflowErrorEvent error:
            failures.Add(error.Exception?.Message ?? "unknown");
            break;
    }
}

foreach (var failure in failures)
{
    Console.WriteLine($"DeclarativeMcpWorkflowApp:error:{failure}");
}

foreach (var reply in agentReplies)
{
    Console.WriteLine($"DeclarativeMcpWorkflowApp:agent-reply:{reply}");
}

Console.WriteLine($"DeclarativeMcpWorkflowApp:actions:{reporter.CompletedActionCount}");

if (failures.Count > 0)
{
    Console.WriteLine("DeclarativeMcpWorkflowApp:failed");
    Console.WriteLine(
        "Is the MCP server running? " +
        "PORT=3111 npx -y @modelcontextprotocol/server-everything streamableHttp");
    return 1;
}

Console.WriteLine("DeclarativeMcpWorkflowApp:completed");
return 0;

/// <summary>
/// Prints each declarative action as it starts, so an MCP call is visible as a workflow action
/// rather than only as whatever the server happens to log.
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
                    $"DeclarativeMcpWorkflowApp:action-started:{started.ActionId}:{started.ActionType}");
                break;
            case DeclarativeActionCompletedProgressEvent:
                Interlocked.Increment(ref _completedActions);
                break;
            case DeclarativeWorkflowErrorProgressEvent error:
                Console.WriteLine($"DeclarativeMcpWorkflowApp:error:{error.ErrorMessage}");
                break;
        }
    }

    public IProgressReporter CreateChild(string agentId) => this;

    public long NextSequence() => Interlocked.Increment(ref _sequence);
}
