using NexusLabs.Needlr;

namespace GraphWorkflowApp;

[DoNotAutoRegister]
internal sealed class AgentPanel(string shortName)
{
    public string ShortName { get; } = shortName;

    public string Text { get; set; } = "";

    public bool Done { get; set; }

    public bool Failed { get; set; }
}
