namespace NexusLabs.Foundry.MicrosoftAgentFramework.Tests;

/// <summary>
/// Publishes the same name as <see cref="NamedEditorAgent"/> from a different class, to exercise
/// the ambiguity guard against two declared names rather than two class names.
/// </summary>
[FoundryAgent(
    Name = "PublishedEditor",
    Description = "Also edits.",
    Instructions = "edit",
    FunctionTypes = new Type[0])]
public sealed class RivalEditorAgent
{
}
