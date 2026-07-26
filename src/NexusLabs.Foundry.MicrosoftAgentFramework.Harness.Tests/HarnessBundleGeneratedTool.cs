using System.ComponentModel;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Tests;

/// <summary>
/// Source-generated function group used to prove optional Harness bundle ingress.
/// </summary>
[AgentFunctionGroup("harness-bundle-generated")]
public sealed class HarnessBundleGeneratedTool
{
    private readonly HarnessBundleGeneratedToolCapture _capture;

    /// <summary>
    /// Initializes the generated test tool.
    /// </summary>
    /// <param name="capture">Captures the value received by the generated function.</param>
    public HarnessBundleGeneratedTool(HarnessBundleGeneratedToolCapture capture)
    {
        _capture = capture;
    }

    /// <summary>
    /// Records a value through a source-generated <c>AIFunction</c>.
    /// </summary>
    /// <param name="value">The value to record.</param>
    /// <returns>A stable completion result.</returns>
    [AgentFunction]
    [Description("Records a value through the optional Harness bundle.")]
    public string Record(
        [Description("The value to record.")] string value)
    {
        _capture.Value = value;
        return "recorded";
    }
}
