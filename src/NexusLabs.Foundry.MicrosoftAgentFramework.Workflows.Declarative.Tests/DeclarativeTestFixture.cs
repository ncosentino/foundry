using Microsoft.Agents.AI.Workflows;
using Microsoft.Agents.AI.Workflows.Declarative;
using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Foundry.MicrosoftAgentFramework;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Workflows.Declarative.Tests;

/// <summary>
/// Builds a Foundry runtime containing the declared test agents and runs declarative workflows
/// against it.
/// </summary>
internal static class DeclarativeTestFixture
{
    /// <remarks>
    /// Agents are registered explicitly rather than through the source generator's module
    /// initializer, because this test project does not reference the generator. The resolution path
    /// under test — <see cref="IAgentFactory.CreateAgent(string)"/> by published name — is identical
    /// either way.
    /// </remarks>
    internal static DeclarativeTestHost CreateHost()
    {
        var chatClient = new ScriptedDeclarativeChatClient();
        var services = new ServiceCollection();
        services.AddFoundryAgentFramework(builder => builder
            .UsingChatClient(chatClient)
            .AddAgent<ClassifierAgent>()
            .AddAgent<ResponderAgent>()
            .AddAgent<FailingAgent>()
            .AddAgent<ReportDigestWriter>());

        var provider = services.BuildServiceProvider();
        return new DeclarativeTestHost(
            provider.GetRequiredService<IAgentFactory>(),
            chatClient,
            provider);
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
        IAgentFactory AgentFactory,
        ScriptedDeclarativeChatClient ChatClient,
        ServiceProvider Services) : IDisposable
    {
        public void Dispose() => Services.Dispose();
    }

    internal sealed record DeclarativeRunOutcome(
        IReadOnlyList<string> Activities,
        IReadOnlyList<string> Errors,
        IReadOnlyList<string> ObservedEvents);
}
