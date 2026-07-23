using System.ComponentModel;

using NexusLabs.Foundry.MicrosoftAgentFramework;

namespace HarnessCompatibilityProbe;

[AgentFunctionGroup("probe")]
internal static class ProbeFunctions
{
    /// <summary>
    /// Returns the supplied value so the probe can verify generated tool execution.
    /// </summary>
    /// <param name="value">The value to return.</param>
    /// <returns>The unchanged <paramref name="value"/>.</returns>
    [AgentFunction]
    [Description("Returns the provided value.")]
    public static string Echo(string value) => value;
}
