using NexusLabs.Foundry.MicrosoftAgentFramework;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Workflows.Declarative.Tests;

/// <summary>
/// A declared agent addressed by a workflow document as <c>ClassifierAgent</c>.
/// </summary>
/// <remarks>
/// Declared with <see cref="FoundryAgentAttribute"/> rather than constructed inline, because a
/// declarative document resolves agents through <see cref="IAgentFactory"/> by class name. Using the
/// real declaration model is what makes these tests exercise the same path a consumer would.
/// </remarks>
[FoundryAgent(
    Description = "Classifies an incoming report.",
    Instructions = "classified",
    FunctionTypes = new Type[0])]
public sealed class ClassifierAgent
{
}
