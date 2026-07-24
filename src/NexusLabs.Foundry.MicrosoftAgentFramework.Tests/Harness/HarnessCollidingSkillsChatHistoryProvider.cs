using Microsoft.Agents.AI;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Tests.Harness;

/// <summary>
/// Minimal <see cref="ChatHistoryProvider"/> test double that deliberately reports the same
/// constant state key upstream <c>AgentSkillsProvider</c> uses ("AgentSkillsProvider"), for
/// exercising the composition root's cross-provider state-key collision guard against the
/// Skills selected-provider slice specifically.
/// </summary>
internal sealed class HarnessCollidingSkillsChatHistoryProvider : ChatHistoryProvider
{
    public override IReadOnlyList<string> StateKeys => ["AgentSkillsProvider"];
}
