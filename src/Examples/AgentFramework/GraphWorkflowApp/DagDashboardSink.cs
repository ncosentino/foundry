using NexusLabs.Foundry.MicrosoftAgentFramework.Progress;
using NexusLabs.Needlr;

using Spectre.Console;
using Spectre.Console.Rendering;

namespace GraphWorkflowApp;

[DoNotAutoRegister]
internal sealed class DagDashboardSink : IProgressSink
{
    private static readonly string[] _spinnerFrames = ["⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏"];

    private readonly Dictionary<string, AgentPanel> _agents = new();
    private readonly DateTime _start = DateTime.Now;
    private LiveDisplayContext? _context;
    private int _spinnerIndex;
    private bool _complete;

    internal void SetContext(LiveDisplayContext context) => _context = context;

    internal void Refresh()
    {
        _spinnerIndex = (_spinnerIndex + 1) % _spinnerFrames.Length;
        _context?.UpdateTarget(Render());
    }

    internal IRenderable Render()
    {
        var elapsed = (DateTime.Now - _start).TotalSeconds;
        var table = new Table()
            .Border(TableBorder.Heavy)
            .Title("[bold cyan]DAG Workflow — Live[/]")
            .AddColumn(new TableColumn("").NoWrap().Width(90));

        var spinner = _complete ? "[green]✓[/]" : $"[yellow]{_spinnerFrames[_spinnerIndex]}[/]";
        table.AddRow(new Markup($"  {spinner} Elapsed: [bold]{elapsed:F1}s[/]  |  Agents: [bold]{_agents.Count}[/]"));
        table.AddEmptyRow();

        if (_agents.Count == 0)
        {
            table.AddRow(new Markup("  [dim]Waiting for agents...[/]"));
        }
        else
        {
            foreach (var (_, agent) in _agents)
            {
                var statusIcon = agent.Done
                    ? "[green]✓[/]"
                    : agent.Failed
                        ? "[red]✗[/]"
                        : $"[yellow]{_spinnerFrames[_spinnerIndex]}[/]";
                var preview = agent.Text.Length > 120
                    ? agent.Text[^120..].Replace("\n", " ")
                    : agent.Text.Replace("\n", " ");
                table.AddRow(new Markup($"  {statusIcon} [bold]{Markup.Escape(agent.ShortName)}[/]"));
                if (!string.IsNullOrEmpty(preview))
                {
                    table.AddRow(new Markup($"    [dim]{Markup.Escape(preview.Trim())}[/]"));
                }
                else if (!agent.Done && !agent.Failed)
                {
                    table.AddRow(new Markup("    [dim italic]generating...[/]"));
                }
            }
        }

        return table;
    }

    public ValueTask OnEventAsync(IProgressEvent evt, CancellationToken ct)
    {
        switch (evt)
        {
            case AgentInvokedEvent invoked:
                var name = ShortName(invoked.AgentName);
                if (!_agents.ContainsKey(name))
                {
                    foreach (var (_, previousAgent) in _agents)
                    {
                        if (!previousAgent.Done &&
                            !previousAgent.Failed &&
                            previousAgent.Text.Length > 0)
                        {
                            previousAgent.Done = true;
                        }
                    }

                    _agents[name] = new AgentPanel(name);
                    Refresh();
                }
                break;

            case ReducerNodeInvokedEvent reducerInvoked:
                var reducerName = ShortName(reducerInvoked.NodeId);
                if (!_agents.ContainsKey(reducerName))
                {
                    _agents[reducerName] = new AgentPanel(reducerName)
                    {
                        Text = $"[Reducer] Merged {reducerInvoked.InputBranchCount} branches in {reducerInvoked.Duration.TotalMilliseconds:F0}ms",
                        Done = true,
                    };
                    Refresh();
                }
                break;

            case AgentResponseChunkEvent chunk:
                var chunkName = ShortName(chunk.AgentName);
                if (!_agents.TryGetValue(chunkName, out var panel))
                {
                    _agents[chunkName] = panel = new AgentPanel(chunkName);
                }
                panel.Text += chunk.Text;
                Refresh();
                break;

            case AgentCompletedEvent completed:
                var completedName = ShortName(completed.AgentName);
                if (_agents.TryGetValue(completedName, out var completedPanel))
                {
                    completedPanel.Done = true;
                }
                Refresh();
                break;

            case AgentFailedEvent failed:
                var failedName = ShortName(failed.AgentName);
                if (_agents.TryGetValue(failedName, out var failedPanel))
                {
                    failedPanel.Failed = true;
                    failedPanel.Text += $" [ERROR: {failed.ErrorMessage}]";
                }
                Refresh();
                break;

            case WorkflowCompletedEvent:
                _complete = true;
                Refresh();
                break;
        }

        return ValueTask.CompletedTask;
    }

    private static string ShortName(string id)
    {
        var index = id.IndexOf('_');
        return index > 0 ? id[..index] : id;
    }
}
