using Microsoft.Agents.AI;

using NexusLabs.Foundry.MicrosoftAgentFramework.Workspace;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Testing;

/// <summary>
/// Contains the Harness lifecycle evidence supplied to
/// <see cref="IHarnessScenario.VerifyHarness"/>.
/// </summary>
/// <param name="ScenarioName">The scenario name.</param>
/// <param name="UserId">The runner-issued user identity.</param>
/// <param name="OrchestrationId">The runner-issued orchestration identity.</param>
/// <param name="SessionId">The runner-issued session identity.</param>
/// <param name="Workspace">The workspace after execution.</param>
/// <param name="Session">The created MAF session, or <see langword="null"/> if creation failed.</param>
/// <param name="ResponseText">The final response text, or <see langword="null"/> if execution failed.</param>
/// <param name="ResolvedGeneratedToolNames">Generated tool names resolved before construction.</param>
/// <param name="ExecutedToolNames">Tool names observed at the upstream function-invoker seam.</param>
/// <param name="ExecutionError">The construction, session, or execution error, if any.</param>
public sealed record HarnessScenarioVerificationContext(
    string ScenarioName,
    string UserId,
    string OrchestrationId,
    string SessionId,
    IWorkspace Workspace,
    AgentSession? Session,
    string? ResponseText,
    IReadOnlyList<string> ResolvedGeneratedToolNames,
    IReadOnlyList<string> ExecutedToolNames,
    Exception? ExecutionError);
