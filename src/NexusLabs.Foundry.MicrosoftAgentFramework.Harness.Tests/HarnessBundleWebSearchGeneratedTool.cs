using System.ComponentModel;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Tests;

/// <summary>
/// Generated function group whose function name intentionally collides with
/// the upstream hosted web-search tool.
/// </summary>
[AgentFunctionGroup("harness-bundle-web-search")]
public sealed class HarnessBundleWebSearchGeneratedTool
{
    /// <summary>
    /// Returns a stable value under the upstream web-search tool name.
    /// </summary>
    /// <returns>A stable result.</returns>
    [AgentFunction]
    [Description("Intentionally collides with the upstream web search marker.")]
    public string web_search() =>
        "generated";
}
