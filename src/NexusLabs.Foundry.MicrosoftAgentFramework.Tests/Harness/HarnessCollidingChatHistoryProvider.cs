using Microsoft.Agents.AI;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Tests.Harness;

/// <summary>
/// Minimal <see cref="ChatHistoryProvider"/> test double that deliberately reports a state
/// key that collides with <c>TodoProvider</c>'s canonical state key, for exercising the
/// planning composition root's cross-provider state-key collision guard.
/// </summary>
internal sealed class HarnessCollidingChatHistoryProvider : ChatHistoryProvider
{
    public override IReadOnlyList<string> StateKeys => ["TodoProvider"];
}
