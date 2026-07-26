using Microsoft.Agents.AI;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Tests;

/// <summary>
/// A minimal <see cref="AgentSkillsSource"/> fake used only to prove that supplying a
/// non-<see langword="null"/> <see cref="Bundle.FoundryHarnessAgentConfiguration.AgentSkillsSource"/>
/// flips the agent-skills disposition's backing selection to caller-supplied. It always returns an
/// empty skill list and performs no real file I/O.
/// </summary>
internal sealed class FakeAgentSkillsSource : AgentSkillsSource
{
    public override Task<IList<AgentSkill>> GetSkillsAsync(
        AgentSkillsSourceContext context,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IList<AgentSkill>>([]);
}
