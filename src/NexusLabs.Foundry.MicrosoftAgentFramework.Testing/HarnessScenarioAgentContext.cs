using Microsoft.Extensions.AI;

using NexusLabs.Foundry.MicrosoftAgentFramework.Workspace;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Testing;

/// <summary>
/// Supplies the trusted inputs an <see cref="IHarnessScenario"/> uses to construct its agent.
/// </summary>
/// <param name="ScenarioName">The scenario name.</param>
/// <param name="UserId">The runner-issued user identity.</param>
/// <param name="OrchestrationId">The runner-issued orchestration identity.</param>
/// <param name="SessionId">The runner-issued session identity.</param>
/// <param name="Services">Services used for generated-function activation and agent construction.</param>
/// <param name="Workspace">The seeded, per-run workspace.</param>
/// <param name="GeneratedTools">Source-generated functions resolved without reflection fallback.</param>
public sealed record HarnessScenarioAgentContext(
    string ScenarioName,
    string UserId,
    string OrchestrationId,
    string SessionId,
    IServiceProvider Services,
    IWorkspace Workspace,
    IReadOnlyList<AIFunction> GeneratedTools);
