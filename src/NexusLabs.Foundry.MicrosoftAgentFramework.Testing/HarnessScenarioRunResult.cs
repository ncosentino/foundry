using Microsoft.Agents.AI;

using NexusLabs.Foundry.MicrosoftAgentFramework.Workspace;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Testing;

/// <summary>
/// Reports the outcome and captured evidence of one <see cref="IHarnessScenario"/> run.
/// </summary>
/// <param name="ScenarioName">The scenario name.</param>
/// <param name="UserId">The runner-issued user identity.</param>
/// <param name="OrchestrationId">The runner-issued orchestration identity.</param>
/// <param name="SessionId">The runner-issued session identity.</param>
/// <param name="Workspace">The workspace after execution.</param>
/// <param name="Session">The created MAF session, or <see langword="null"/> if creation failed.</param>
/// <param name="ResponseText">The final response text, or <see langword="null"/> if execution failed.</param>
/// <param name="ResolvedGeneratedToolNames">Generated tool names resolved before construction.</param>
/// <param name="ExecutedGeneratedToolNames">
/// Generated tool names whose supplied function bodies were invoked.
/// </param>
/// <param name="ExecutionError">The construction, session, or execution error, if any.</param>
/// <param name="VerificationError">The base <see cref="IAgentScenario.Verify"/> error, if any.</param>
/// <param name="HarnessVerificationError">
/// The <see cref="IHarnessScenario.VerifyHarness"/> error, if any.
/// </param>
/// <param name="Succeeded">
/// <see langword="true"/> when execution and both verification passes completed without error.
/// </param>
public sealed record HarnessScenarioRunResult(
    string ScenarioName,
    string UserId,
    string OrchestrationId,
    string SessionId,
    IWorkspace Workspace,
    AgentSession? Session,
    string? ResponseText,
    IReadOnlyList<string> ResolvedGeneratedToolNames,
    IReadOnlyList<string> ExecutedGeneratedToolNames,
    Exception? ExecutionError,
    Exception? VerificationError,
    Exception? HarnessVerificationError,
    bool Succeeded);
