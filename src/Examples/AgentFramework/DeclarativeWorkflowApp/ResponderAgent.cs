using NexusLabs.Foundry.MicrosoftAgentFramework;

namespace DeclarativeWorkflowApp;

/// <summary>
/// Drafts a reply to an incoming support report.
/// </summary>
[FoundryAgent(
    Description = "Drafts a reply to an incoming support report.",
    Instructions = "responder",
    FunctionTypes = new Type[0])]
public sealed class ResponderAgent
{
}
