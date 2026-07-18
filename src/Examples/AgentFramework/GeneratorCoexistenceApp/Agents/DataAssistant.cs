using NexusLabs.Foundry.MicrosoftAgentFramework;

namespace GeneratorCoexistenceApp;

/// <summary>
/// Needlr-side: <see cref="FoundryAgentAttribute"/> declares this agent.
/// Needlr's source generator emits it into the <c>AgentRegistry</c>, generates
/// a partial companion with topology metadata, and wires function groups.
/// </summary>
[FoundryAgent(
    Description = "A data lookup assistant that answers questions using the data store.",
    Instructions = "You are a helpful assistant. Use the data-tools to look up information.",
    FunctionGroups = new[] { "data-tools" })]
public partial class DataAssistant { }
