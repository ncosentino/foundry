using NexusLabs.Needlr;

using Spectre.Console;

namespace SpectreProgressApp;

[DoNotAutoRegister]
internal sealed class AgentState(string name)
{
    internal string Name { get; } = name;

    internal string Status { get; set; } = "[dim]Pending[/]";

    internal bool IsWorking { get; set; }

    internal string LlmStatus { get; set; } = "";

    internal string ToolStatus { get; set; } = "";

    internal int CurrentLlmCall { get; set; }

    internal int LlmCalls { get; set; }

    internal int LlmTokens { get; set; }

    internal int ToolCalls { get; set; }

    internal string Render(string spinnerFrame)
    {
        var icon = IsWorking ? $"[yellow]{spinnerFrame}[/] " : "  ";
        var line = $"  {icon}[bold]{Markup.Escape(Name)}[/]  {Status}";
        if (LlmCalls > 0 || LlmStatus.Length > 0)
        {
            line += $"  |  LLM: {LlmStatus} ({LlmCalls} calls, {LlmTokens} tok)";
        }
        if (ToolCalls > 0 || ToolStatus.Length > 0)
        {
            line += $"  |  Tools: {ToolStatus} ({ToolCalls})";
        }
        return line;
    }
}
