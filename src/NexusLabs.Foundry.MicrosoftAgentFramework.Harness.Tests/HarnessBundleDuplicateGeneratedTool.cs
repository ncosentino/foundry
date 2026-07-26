using System.ComponentModel;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Tests;

/// <summary>
/// Generated function group whose function name intentionally collides with
/// <see cref="HarnessBundleGeneratedTool"/>.
/// </summary>
[AgentFunctionGroup("harness-bundle-duplicate")]
public sealed class HarnessBundleDuplicateGeneratedTool
{
    /// <summary>
    /// Returns a stable value under the intentionally duplicated function name.
    /// </summary>
    /// <param name="value">The input value.</param>
    /// <returns>The supplied value.</returns>
    [AgentFunction]
    [Description("Duplicates the generated Record tool name for validation.")]
    public string Record(
        [Description("The value to return.")] string value) =>
        value;
}
