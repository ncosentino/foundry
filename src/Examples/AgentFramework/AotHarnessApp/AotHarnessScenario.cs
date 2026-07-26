using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

using NexusLabs.Foundry.MicrosoftAgentFramework.Diagnostics;
using NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Bundle;
using NexusLabs.Foundry.MicrosoftAgentFramework.Testing;
using NexusLabs.Foundry.MicrosoftAgentFramework.Workspace;

namespace AotHarnessApp;

internal sealed class AotHarnessScenario : IHarnessScenario
{
    internal const string OutputPath = "proof/result.txt";
    internal const string ExpectedWorkspaceContent = "aot-proof";
    internal const string ExpectedResponse = "harness-result:written:aot-proof";

    private static readonly FoundryHarnessAgentFactory Factory = new();

    internal FoundryHarnessEffectiveDefaults? EffectiveDefaults { get; private set; }

    public string Name => "minimum-aot-harness";

    public string Description =>
        "Proves generated-tool, workspace, session, and optional Harness execution under NativeAOT.";

    public string SystemPrompt =>
        "Use the generated WriteWorkspace tool exactly once.";

    public string UserPrompt =>
        "Write the deterministic AOT proof value.";

    public IReadOnlyList<Type> GeneratedFunctionTypes { get; } =
        [typeof(AotHarnessTool)];

    public void SeedWorkspace(IWorkspace workspace)
    {
        workspace.TryWriteFile("proof/seed.txt", "seeded");
    }

    public AIAgent CreateAgent(HarnessScenarioAgentContext context)
    {
        var function = context.GeneratedTools.Single();
        var configuration = new FoundryHarnessAgentConfiguration
        {
            Id = "minimum-aot-harness-agent",
            Name = "Minimum AOT Harness Agent",
            Description = Description,
            Instructions = SystemPrompt,
            HarnessInstructionsOverride = string.Empty,
            ChatClient = new AotHarnessScriptedChatClient(function.Name),
            Tools = [.. context.GeneratedTools],
            Features = new FoundryHarnessFeatureSelections
            {
                EnableWebSearch = false,
                EnableFileMemory = false,
                EnableAgentSkills = false,
                EnableToolAutoApproval = false,
                EnableApprovalNotRequiredFunctionBypassing = false,
                EnableApprovalResponseBinding = false,
                EnableOpenTelemetry = false,
                EnableTodoProvider = false,
                EnableAgentModeProvider = false,
                EnableCompaction = false,
            },
            ProgressAccessor = null,
            MaxContextWindowTokens = null,
            MaxOutputTokens = null,
            MaximumIterationsPerRequest = 4,
            FileAccessStore = null,
            FileAccessProviderOptions = null,
            ChatHistoryProvider = null,
            FileMemoryStore = null,
            AgentSkillsSource = null,
            ToolApprovalAgentOptions = null,
            AgentModeProviderOptions = null,
            CompactionStrategy = null,
            OpenTelemetrySourceName = null,
            AdditionalContextProviders = [],
        };
        EffectiveDefaults = Factory.DescribeEffectiveDefaults(configuration);
        return Factory.Create(configuration, context.Services);
    }

    public void Verify(
        IWorkspace workspace,
        IAgentRunDiagnostics? diagnostics)
    {
        if (!workspace.FileExists("proof/seed.txt"))
        {
            throw new ScenarioVerificationException(
                Name,
                "The seeded workspace artifact was missing.");
        }

        var output = workspace.TryReadFile(OutputPath);
        if (!output.Success ||
            !string.Equals(
                output.Value.Content,
                ExpectedWorkspaceContent,
                StringComparison.Ordinal))
        {
            throw new ScenarioVerificationException(
                Name,
                "The generated tool did not write the expected artifact.");
        }
    }

    public void VerifyHarness(HarnessScenarioVerificationContext context)
    {
        if (context.ExecutionError is not null)
        {
            throw new ScenarioVerificationException(
                Name,
                $"Harness execution failed: {context.ExecutionError.Message}");
        }

        if (context.Session is null)
        {
            throw new ScenarioVerificationException(Name, "No MAF session was created.");
        }

        if (!context.ResolvedGeneratedToolNames.SequenceEqual(
            ["WriteWorkspace"],
            StringComparer.Ordinal))
        {
            throw new ScenarioVerificationException(
                Name,
                "The generated tool resolution evidence was incorrect.");
        }

        if (!context.ExecutedGeneratedToolNames.SequenceEqual(
            ["WriteWorkspace"],
            StringComparer.Ordinal))
        {
            throw new ScenarioVerificationException(
                Name,
                "The generated tool did not execute exactly once.");
        }

        if (!string.Equals(
            context.ResponseText,
            ExpectedResponse,
            StringComparison.Ordinal))
        {
            throw new ScenarioVerificationException(
                Name,
                "The final response did not contain the generated tool result.");
        }

        var functionInvocation = EffectiveDefaults?.GetDisposition(
            FoundryHarnessFeature.FunctionInvocation);
        if (functionInvocation?.EffectiveState !=
            FoundryHarnessFeatureEffectiveState.AlwaysOnUnavoidable)
        {
            throw new ScenarioVerificationException(
                Name,
                "The effective-default report did not confirm the upstream function loop.");
        }
    }
}
