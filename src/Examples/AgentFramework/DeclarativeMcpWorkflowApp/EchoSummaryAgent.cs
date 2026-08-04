using NexusLabs.Foundry.MicrosoftAgentFramework;

namespace DeclarativeMcpWorkflowApp;

/// <summary>
/// Reports on what the MCP tool returned. Declared rather than constructed, so the workflow document
/// can name it the same way it names any other Foundry agent.
/// </summary>
[FoundryAgent(
    Name = "EchoSummarizer",
    Description = "Reports on the result of the MCP echo tool.",
    Instructions = "summarizer",
    FunctionTypes = new Type[0])]
public sealed class EchoSummaryAgent
{
}
