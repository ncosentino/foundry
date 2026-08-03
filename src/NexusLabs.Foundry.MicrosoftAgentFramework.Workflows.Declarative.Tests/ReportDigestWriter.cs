using NexusLabs.Foundry.MicrosoftAgentFramework;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Workflows.Declarative.Tests;

/// <summary>
/// A declared agent addressed by a workflow document as <c>Summarizer</c> rather than by its class
/// name, so the document survives a rename of this class.
/// </summary>
/// <remarks>
/// The class name and the published name are deliberately unalike. A test that used the same word
/// for both could pass whether the document resolved the declared name or fell back to the class
/// name, and so would prove nothing.
/// </remarks>
[FoundryAgent(
    Name = "Summarizer",
    Description = "Summarizes a report.",
    Instructions = "summarized",
    FunctionTypes = new Type[0])]
public sealed class ReportDigestWriter
{
}
