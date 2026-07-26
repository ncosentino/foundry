using Microsoft.Agents.AI;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Testing;

/// <summary>
/// Extends <see cref="IAgentScenario"/> with the construction and verification hooks required
/// to exercise a Microsoft Agent Framework Harness lifecycle.
/// </summary>
/// <remarks>
/// The scenario owns agent construction so the Testing package remains independent of the
/// optional complete-Harness bundle. <see cref="HarnessScenarioRunner"/> owns generated-function
/// resolution, workspace and execution-context scoping, session creation, execution, and evidence
/// capture.
/// </remarks>
public interface IHarnessScenario : IAgentScenario
{
    /// <summary>
    /// Gets the source-generated function-group types required by the scenario.
    /// </summary>
    IReadOnlyList<Type> GeneratedFunctionTypes { get; }

    /// <summary>
    /// Creates the Harness-enabled agent that the runner will execute.
    /// </summary>
    /// <param name="context">
    /// The seeded workspace, trusted execution identity, caller services, and generated functions.
    /// </param>
    /// <remarks>
    /// The supplied generated functions carry the runner's execution tracker. Scenarios must pass
    /// them through directly, or through wrappers that delegate to them, for generated-body
    /// execution evidence to be recorded.
    /// </remarks>
    /// <returns>The constructed Harness-enabled agent.</returns>
    AIAgent CreateAgent(HarnessScenarioAgentContext context);

    /// <summary>
    /// Verifies Harness-specific lifecycle evidence after execution.
    /// </summary>
    /// <param name="context">
    /// The session, response, generated-tool resolution, execution trace, workspace, and error
    /// evidence captured by the runner.
    /// </param>
    void VerifyHarness(HarnessScenarioVerificationContext context);
}
