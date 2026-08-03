namespace NexusLabs.Foundry.MicrosoftAgentFramework.Tests;

/// <summary>
/// An agent that publishes a name distinct from its class name, so tests can tell the two apart.
/// </summary>
[FoundryAgent(
    Name = "PublishedEditor",
    Description = "Edits.",
    Instructions = "edit",
    FunctionTypes = new Type[0])]
public sealed class NamedEditorAgent
{
}
