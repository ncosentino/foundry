namespace NexusLabs.Foundry.MicrosoftAgentFramework.Tests.Collisions;

/// <summary>
/// Deliberately shares a simple class name with
/// <see cref="Tests.LookupWriterAgent"/>, to exercise the ambiguity guard.
/// </summary>
[FoundryAgent(Description = "Also writes.", Instructions = "write", FunctionTypes = new Type[0])]
public sealed class LookupWriterAgent
{
}
