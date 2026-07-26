using Microsoft.Agents.AI;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Tests;

/// <summary>
/// A minimal <see cref="AgentSkillsSource"/> fake used only to prove that supplying a
/// non-<see langword="null"/> <see cref="Bundle.FoundryHarnessAgentConfiguration.AgentSkillsSource"/>
/// flips the agent-skills disposition's backing selection to caller-supplied. It returns one
/// in-memory skill and performs no real file I/O.
/// </summary>
internal sealed class FakeAgentSkillsSource : AgentSkillsSource
{
    public override Task<IList<AgentSkill>> GetSkillsAsync(
        AgentSkillsSourceContext context,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IList<AgentSkill>>(
        [
            new AgentInlineSkill(
                "test-skill",
                "An in-memory skill used by Harness bundle tests.",
                "Load this skill for test execution.")
                .AddResource(
                    "test-resource",
                    new Func<string>(() => "resource-content"),
                    "An in-memory test resource.",
                    serializerOptions: null)
                .AddScript(
                    "test-script",
                    new Func<string>(() => "script-result"),
                    "An in-memory test script.",
                    serializerOptions: null),
        ]);
}
