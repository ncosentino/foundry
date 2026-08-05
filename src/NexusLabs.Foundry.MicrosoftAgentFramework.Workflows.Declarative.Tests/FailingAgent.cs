using NexusLabs.Foundry.MicrosoftAgentFramework;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Workflows.Declarative.Tests;

/// <summary>
/// Emits an <see cref="Microsoft.Extensions.AI.ErrorContent"/> response so tests can pin how the
/// declarative runtime handles an agent-reported failure rather than an exception thrown by the
/// provider.
/// </summary>
[FoundryAgent(
    Description = "Reports an agent failure.",
    Instructions = "failed",
    FunctionTypes = new Type[0])]
public sealed class FailingAgent
{
}
