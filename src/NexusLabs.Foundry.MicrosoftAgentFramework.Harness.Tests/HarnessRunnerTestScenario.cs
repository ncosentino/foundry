using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Foundry.MicrosoftAgentFramework.Context;
using NexusLabs.Foundry.MicrosoftAgentFramework.Diagnostics;
using NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Bundle;
using NexusLabs.Foundry.MicrosoftAgentFramework.Testing;
using NexusLabs.Foundry.MicrosoftAgentFramework.Workspace;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Tests;

internal sealed class HarnessRunnerTestScenario(
    IReadOnlyList<Type> generatedFunctionTypes,
    bool failBaseVerification,
    bool failHarnessVerification) : IHarnessScenario
{
    private static readonly FoundryHarnessAgentFactory Factory = new();

    internal int CreateAgentCallCount { get; private set; }

    internal HarnessScenarioAgentContext? AgentContext { get; private set; }

    internal HarnessScenarioVerificationContext? VerificationContext { get; private set; }

    public string Name => "runner-test";

    public string Description => "Exercises the reusable Harness scenario runner.";

    public string SystemPrompt => "Use the generated Record tool.";

    public string UserPrompt => "Record generated-value.";

    public IReadOnlyList<Type> GeneratedFunctionTypes { get; } = generatedFunctionTypes;

    public void SeedWorkspace(IWorkspace workspace)
    {
        workspace.TryWriteFile("seed.txt", "seeded");
    }

    public AIAgent CreateAgent(HarnessScenarioAgentContext context)
    {
        CreateAgentCallCount++;
        AgentContext = context;
        var accessor = context.Services.GetRequiredService<IAgentExecutionContextAccessor>();
        var current = accessor.Current
            ?? throw new InvalidOperationException("The execution context was not active during agent creation.");
        if (!ReferenceEquals(context.Workspace, current.GetWorkspace()))
        {
            throw new InvalidOperationException("The seeded workspace was not active during agent creation.");
        }

        var function = context.GeneratedTools.Single();
        var chatClient = new HarnessBundleToolCallChatClient(
            function.Name,
            new Dictionary<string, object?>
            {
                ["value"] = "generated-value",
            });
        var configuration = HarnessBundleTestsHelpers.CreateBaseline() with
        {
            Id = "runner-test-agent",
            Name = "Runner Test Agent",
            Instructions = SystemPrompt,
            ChatClient = chatClient,
            Tools = [.. context.GeneratedTools],
        };
        return Factory.Create(configuration, context.Services);
    }

    public void Verify(
        IWorkspace workspace,
        IAgentRunDiagnostics? diagnostics)
    {
        if (!workspace.FileExists("seed.txt"))
        {
            throw new ScenarioVerificationException(Name, "The seeded workspace file was missing.");
        }

        if (failBaseVerification)
        {
            throw new ScenarioVerificationException(Name, "Base verification failed.");
        }
    }

    public void VerifyHarness(HarnessScenarioVerificationContext context)
    {
        VerificationContext = context;
        if (failHarnessVerification)
        {
            throw new ScenarioVerificationException(Name, "Harness verification failed.");
        }
    }
}
