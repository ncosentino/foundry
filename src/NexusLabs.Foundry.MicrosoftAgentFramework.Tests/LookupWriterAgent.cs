namespace NexusLabs.Foundry.MicrosoftAgentFramework.Tests;

/// <summary>
/// An agent declared solely to exercise name-based lookup.
/// </summary>
[FoundryAgent(Description = "Writes.", Instructions = "write", FunctionTypes = new Type[0])]
public sealed class LookupWriterAgent
{
}
