using NexusLabs.Foundry.MicrosoftAgentFramework;

namespace DeclarativeWorkflowApp;

/// <summary>
/// Classifies an incoming support report. Declared rather than constructed, so the workflow
/// document can address it by name through <see cref="IAgentFactory"/>.
/// </summary>
[FoundryAgent(
    Description = "Classifies an incoming support report.",
    Instructions = "classifier",
    FunctionTypes = new Type[0])]
public sealed class ClassifierAgent
{
}
