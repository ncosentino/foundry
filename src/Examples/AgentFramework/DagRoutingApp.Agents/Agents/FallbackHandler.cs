using NexusLabs.Foundry.MicrosoftAgentFramework;

namespace DagRoutingApp.Agents;

/// <summary>
/// Default handler in the "priority-routing" graph. Executes when no
/// other condition matches (unconditional edge, evaluated last due to
/// <see cref="GraphRoutingMode.FirstMatching"/>).
/// </summary>
[FoundryAgent(
    Description = "Default fallback handler for unclassified requests.",
    Instructions = """
        You are a general-purpose handler. The request did not match any
        specific category. Provide a helpful default response.
        Keep your response under 100 words.
        """,
    FunctionTypes = new Type[0])]
public partial class FallbackHandler { }
