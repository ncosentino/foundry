using NexusLabs.Foundry.MicrosoftAgentFramework;

namespace DeclarativeWorkflowApp;

/// <summary>
/// Drafts a reply to an incoming support report.
/// </summary>
/// <remarks>
/// Declares the name the workflow document addresses it by, so this class can be renamed without
/// editing the document. <c>ClassifierAgent</c> alongside it does not, and is addressed by its class
/// name instead — both forms work, and the document reads the same either way.
/// </remarks>
[FoundryAgent(
    Name = "Responder",
    Description = "Drafts a reply to an incoming support report.",
    Instructions = "responder",
    FunctionTypes = new Type[0])]
public sealed class ResponderAgent
{
}
