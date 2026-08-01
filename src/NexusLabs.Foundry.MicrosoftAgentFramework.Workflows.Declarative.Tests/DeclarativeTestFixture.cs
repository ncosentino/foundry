using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Agents.AI.Workflows.Declarative;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Workflows.Declarative.Tests;

/// <summary>
/// Shared construction and execution helpers for declarative workflow tests.
/// </summary>
internal static class DeclarativeTestFixture
{
    internal static DeclarativeTestHost CreateHost(
        params (string Name, string ResponsePrefix)[] agents)
    {
        var clients = new Dictionary<string, ScriptedDeclarativeChatClient>(StringComparer.OrdinalIgnoreCase);
        var registered = new Dictionary<string, AIAgent>(StringComparer.OrdinalIgnoreCase);

        foreach (var (name, responsePrefix) in agents)
        {
            var client = new ScriptedDeclarativeChatClient(responsePrefix);
            clients[name] = client;
            registered[name] = new ChatClientAgent(
                client,
                name: name,
                instructions: "Answer using the supplied text.");
        }

        var provider = new FoundryAgentProvider(new DeclarativeWorkflowAgentRegistry(registered));
        return new DeclarativeTestHost(provider, clients);
    }

    /// <summary>
    /// Runs a workflow to completion, collecting the activities it emitted, any error it reported,
    /// and every event type observed so a failing test can show what the workflow actually did.
    /// </summary>
    internal static async Task<DeclarativeRunOutcome> RunAsync(
        Workflow workflow,
        string input,
        CancellationToken cancellationToken)
    {
        var activities = new List<string>();
        var errors = new List<string>();
        var observed = new List<string>();

        StreamingRun run = await InProcessExecution
            .RunStreamingAsync(workflow, input, CheckpointManager.CreateInMemory())
            .ConfigureAwait(false);

        await foreach (var workflowEvent in run
            .WatchStreamAsync()
            .WithCancellation(cancellationToken)
            .ConfigureAwait(false))
        {
            observed.Add(workflowEvent.GetType().Name);
            switch (workflowEvent)
            {
                case WorkflowErrorEvent error:
                    errors.Add(error.Exception?.ToString() ?? "unknown");
                    break;
                case MessageActivityEvent activity:
                    activities.Add(activity.Message);
                    break;
            }
        }

        return new DeclarativeRunOutcome(activities, errors, observed);
    }

    internal sealed record DeclarativeTestHost(
        FoundryAgentProvider Provider,
        IReadOnlyDictionary<string, ScriptedDeclarativeChatClient> Clients);

    internal sealed record DeclarativeRunOutcome(
        IReadOnlyList<string> Activities,
        IReadOnlyList<string> Errors,
        IReadOnlyList<string> ObservedEvents);
}
