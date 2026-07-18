using NexusLabs.Foundry.MicrosoftAgentFramework.Progress;
using NexusLabs.Needlr;

using Spectre.Console;
using Spectre.Console.Rendering;

namespace SpectreProgressApp;

[DoNotAutoRegister]
internal sealed class DashboardSink : IProgressSink
{
    private static readonly string[] _spinnerFrames = ["⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏"];

    private readonly List<AgentState> _agents = [];
    private readonly DateTime _start = DateTime.Now;
    private long _budgetCurrent;
    private long _budgetMax;
    private string? _budgetStatus;
    private LiveDisplayContext? _context;
    private bool _isRunning;
    private int _llmCallCount;
    private int _spinnerIndex;
    private int _superStepNumber;
    private double _totalLlmMs;
    private int _totalTokens;
    private string _workflowStatus = "[dim]Initializing...[/]";

    internal void SetContext(LiveDisplayContext context) => _context = context;

    internal void Tick()
    {
        _spinnerIndex = (_spinnerIndex + 1) % _spinnerFrames.Length;
        Refresh();
    }

    internal IRenderable Render()
    {
        var elapsed = (DateTime.Now - _start).TotalSeconds;

        var panel = new Table()
            .Border(TableBorder.Heavy)
            .Title("[bold cyan]Orchestration Dashboard[/]")
            .AddColumn(new TableColumn("").Width(80));

        var spinner = _isRunning ? $"[yellow]{_spinnerFrames[_spinnerIndex]}[/] " : "";
        var stepText = _superStepNumber > 0 ? $"  |  Step: [bold]{_superStepNumber}[/]" : "";
        panel.AddRow(new Markup($"  {spinner}{_workflowStatus}  |  Elapsed: [bold]{elapsed:F1}s[/]  |  LLM calls: [bold]{_llmCallCount}[/]  |  Tokens: [bold]{_totalTokens}[/]{stepText}"));
        panel.AddEmptyRow();

        if (_agents.Count == 0)
        {
            panel.AddRow(new Markup("  [dim]Waiting for agents...[/]"));
        }
        else
        {
            var frame = _spinnerFrames[_spinnerIndex];
            foreach (var agent in _agents)
            {
                panel.AddRow(new Markup(agent.Render(frame)));
            }
        }

        panel.AddEmptyRow();

        if (_budgetMax > 0 || _budgetStatus is not null)
        {
            var budgetText = _budgetStatus ?? $"[dim]{_budgetCurrent}/{_budgetMax} tokens[/]";
            panel.AddRow(new Markup($"  Budget: {budgetText}"));
            panel.AddEmptyRow();
        }

        var averageMilliseconds = _llmCallCount > 0 ? _totalLlmMs / _llmCallCount : 0;
        var secondsPerCall = _llmCallCount > 0 ? elapsed / _llmCallCount : 0;
        panel.AddRow(new Markup($"  [dim]Avg LLM latency: {averageMilliseconds:F0}ms  |  Throughput: {secondsPerCall:F1}s/call[/]"));

        return panel;
    }

    public ValueTask OnEventAsync(IProgressEvent evt, CancellationToken ct)
    {
        switch (evt)
        {
            case WorkflowStartedEvent:
                _workflowStatus = "[yellow]Running[/]";
                _isRunning = true;
                break;

            case WorkflowCompletedEvent completed:
                _isRunning = false;
                _workflowStatus = completed.Succeeded
                    ? $"[green]Complete ✓[/] ({completed.TotalDuration.TotalSeconds:F1}s)"
                    : $"[red]Failed ✗[/]: {Markup.Escape(completed.ErrorMessage ?? "?")}";
                break;

            case AgentInvokedEvent invoked:
                if (invoked.AgentName.Contains("Handoff"))
                {
                    break;
                }
                var existing = _agents.FirstOrDefault(agent => agent.Name == ShortName(invoked.AgentName));
                if (existing is null)
                {
                    _agents.Add(new AgentState(ShortName(invoked.AgentName))
                    {
                        Status = "[yellow]Working...[/]",
                        IsWorking = true,
                    });
                }
                else
                {
                    existing.Status = "[yellow]Working...[/]";
                    existing.IsWorking = true;
                }
                break;

            case AgentCompletedEvent agentCompleted:
                var completedAgent = _agents.FirstOrDefault(agent => agent.Name == ShortName(agentCompleted.AgentName));
                if (completedAgent is not null)
                {
                    completedAgent.Status = $"[green]✓ Done[/] ({agentCompleted.TotalTokens} tok, {agentCompleted.Duration.TotalMilliseconds:F0}ms)";
                    completedAgent.IsWorking = false;
                    _totalTokens += (int)agentCompleted.TotalTokens;
                }
                break;

            case AgentHandoffEvent handoff:
                var sourceAgent = _agents.FirstOrDefault(agent => agent.Name == ShortName(handoff.FromAgentId));
                if (sourceAgent is not null)
                {
                    sourceAgent.Status = $"[dim]→ handed off to {ShortName(handoff.ToAgentId)}[/]";
                }
                break;

            case LlmCallStartedEvent llmStarted:
                var callingAgent = _agents.LastOrDefault();
                if (callingAgent is not null)
                {
                    callingAgent.CurrentLlmCall = llmStarted.CallSequence;
                    callingAgent.LlmStatus = "[blue]⏳ Calling LLM...[/]";
                }
                break;

            case LlmCallCompletedEvent llmCompleted:
                _llmCallCount++;
                _totalLlmMs += llmCompleted.Duration.TotalMilliseconds;
                var respondingAgent = _agents.LastOrDefault();
                if (respondingAgent is not null)
                {
                    respondingAgent.LlmCalls++;
                    respondingAgent.LlmTokens += (int)llmCompleted.TotalTokens;
                    respondingAgent.LlmStatus = $"[blue]✓ #{llmCompleted.CallSequence}[/] {llmCompleted.Duration.TotalMilliseconds:F0}ms";
                }
                break;

            case LlmCallFailedEvent llmFailed:
                _llmCallCount++;
                _totalLlmMs += llmFailed.Duration.TotalMilliseconds;
                var failedAgent = _agents.LastOrDefault();
                if (failedAgent is not null)
                {
                    failedAgent.LlmStatus = $"[red]✗ #{llmFailed.CallSequence} FAILED[/]";
                }
                break;

            case ToolCallStartedEvent toolStarted:
                var toolAgent = _agents.LastOrDefault();
                if (toolAgent is not null)
                {
                    toolAgent.ToolStatus = $"[magenta]🔧 {Markup.Escape(toolStarted.ToolName)}...[/]";
                }
                break;

            case ToolCallCompletedEvent toolCompleted:
                var toolCompletedAgent = _agents.LastOrDefault();
                if (toolCompletedAgent is not null)
                {
                    toolCompletedAgent.ToolCalls++;
                    toolCompletedAgent.ToolStatus = $"[magenta]✓ {Markup.Escape(toolCompleted.ToolName)}[/] {toolCompleted.Duration.TotalMilliseconds:F0}ms";
                }
                break;

            case BudgetUpdatedEvent budgetUpdated:
                _budgetCurrent = budgetUpdated.CurrentTotalTokens;
                _budgetMax = budgetUpdated.MaxTotalTokens ?? 0;
                break;

            case BudgetExceededEvent budgetExceeded:
                _budgetStatus = $"[red]EXCEEDED: {budgetExceeded.LimitType} {budgetExceeded.CurrentValue}/{budgetExceeded.MaxValue}[/]";
                break;

            case SuperStepStartedProgressEvent superStepStarted:
                _superStepNumber = superStepStarted.StepNumber;
                break;

            case AgentFailedEvent agentFailed:
                var failed = _agents.FirstOrDefault(agent => agent.Name == ShortName(agentFailed.AgentName));
                if (failed is null)
                {
                    _agents.Add(new AgentState(ShortName(agentFailed.AgentName))
                    {
                        Status = $"[red]✗ FAILED[/] {Markup.Escape(agentFailed.ErrorMessage)}",
                    });
                }
                else
                {
                    failed.Status = $"[red]✗ FAILED[/] {Markup.Escape(agentFailed.ErrorMessage)}";
                    failed.IsWorking = false;
                }
                break;
        }

        Refresh();
        return ValueTask.CompletedTask;
    }

    private void Refresh()
    {
        if (_context is null)
        {
            return;
        }

        _context.UpdateTarget(Render());
        _context.Refresh();
    }

    private static string ShortName(string id)
    {
        var index = id.IndexOf('_');
        return index > 0 ? id[..index] : id;
    }
}
