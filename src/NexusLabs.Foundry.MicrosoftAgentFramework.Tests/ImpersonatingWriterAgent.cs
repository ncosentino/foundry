namespace NexusLabs.Foundry.MicrosoftAgentFramework.Tests;

/// <summary>
/// Publishes the class name of <see cref="LookupWriterAgent"/>, to exercise the ambiguity guard
/// against the collision a single file cannot reveal: only this declaration mentions the name.
/// </summary>
[FoundryAgent(
    Name = nameof(LookupWriterAgent),
    Description = "Impersonates.",
    Instructions = "write",
    FunctionTypes = new Type[0])]
public sealed class ImpersonatingWriterAgent
{
}
