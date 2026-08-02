using NexusLabs.Foundry.MicrosoftAgentFramework;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Workflows.Declarative.Tests;

/// <summary>
/// A declared agent addressed by a workflow document as <c>ResponderAgent</c>.
/// </summary>
[FoundryAgent(
    Description = "Drafts a reply to an incoming report.",
    Instructions = "responded",
    FunctionTypes = new Type[0])]
public sealed class ResponderAgent
{
}
