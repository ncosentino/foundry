using Microsoft.Extensions.AI;

using MafDeclarative = Microsoft.Agents.AI.Workflows.Declarative;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Workflows.Declarative.Tests;

/// <summary>
/// Records every MCP tool invocation a workflow makes, so a test can assert what the document
/// actually delivered rather than only that the handler was reached.
/// </summary>
internal sealed class RecordingMcpToolHandler : MafDeclarative.IMcpToolHandler
{
    private readonly List<McpInvocation> _invocations = [];
    private readonly string _output;

    internal RecordingMcpToolHandler(string output)
    {
        _output = output;
    }

    internal IReadOnlyList<McpInvocation> Invocations
    {
        get
        {
            lock (_invocations)
            {
                return [.. _invocations];
            }
        }
    }

    public Task<McpServerToolResultContent> InvokeToolAsync(
        string serverUrl,
        string? serverLabel,
        string toolName,
        IDictionary<string, object?>? arguments,
        IDictionary<string, string>? headers,
        string? connectionName,
        CancellationToken cancellationToken)
    {
        lock (_invocations)
        {
            _invocations.Add(new McpInvocation(
                serverUrl,
                serverLabel,
                toolName,
                arguments is null
                    ? new Dictionary<string, object?>(StringComparer.Ordinal)
                    : new Dictionary<string, object?>(arguments, StringComparer.Ordinal),
                headers is null
                    ? new Dictionary<string, string>(StringComparer.Ordinal)
                    : new Dictionary<string, string>(headers, StringComparer.Ordinal),
                connectionName));
        }

        var result = new McpServerToolResultContent($"call-{_invocations.Count}")
        {
            Outputs = [new TextContent(_output)],
        };

        return Task.FromResult(result);
    }

    internal sealed record McpInvocation(
        string ServerUrl,
        string? ServerLabel,
        string ToolName,
        IReadOnlyDictionary<string, object?> Arguments,
        IReadOnlyDictionary<string, string> Headers,
        string? ConnectionName);
}
