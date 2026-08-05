using System.ComponentModel;

using Microsoft.Extensions.AI;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.GeneratedWrapper.Tests;

#pragma warning disable MEAI001

[AgentFunctionGroup("e2e-published-name")]
public sealed class E2EPublishedNameTool
{
    public sealed class Capture
    {
        public string? Value { get; set; }
    }

    private readonly Capture _capture;

    public E2EPublishedNameTool(Capture capture)
    {
        _capture = capture;
    }

    [AgentFunction]
    [AIFunctionName("record_published_value")]
    [Description("Records a value under a published function contract.")]
    public string Record(
        [AIParameterName("published_value")]
        [Description("The value to record.")] string value)
    {
        _capture.Value = value;
        return "ok";
    }
}

#pragma warning restore MEAI001
