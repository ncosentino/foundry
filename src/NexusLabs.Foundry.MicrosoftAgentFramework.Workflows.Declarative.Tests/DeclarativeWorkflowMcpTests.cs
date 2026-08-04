namespace NexusLabs.Foundry.MicrosoftAgentFramework.Workflows.Declarative.Tests;

/// <summary>
/// Covers the handlers a declarative document calls out through, and pins the <c>InvokeMcpTool</c>
/// action shape.
/// </summary>
/// <remarks>
/// The action's schema is not published anywhere — declarative workflows have no JSON Schema — so it
/// was established by running candidate documents and observing which reached the handler. These
/// tests are the record of that: if upstream renames a property, they fail rather than the shape
/// silently becoming wrong in the documentation.
/// </remarks>
public sealed class DeclarativeWorkflowMcpTests
{
    private const string McpWorkflow = """
        kind: Workflow
        trigger:

          kind: OnConversationStart
          id: mcp_workflow
          actions:

            - kind: InvokeMcpTool
              id: call_tool
              serverUrl: https://example.test/mcp
              serverLabel: example
              toolName: search
              arguments:
                query: =System.LastMessage.Text
              output:
                responseObject: Local.ToolResult
        """;

    [Fact]
    public async Task Run_McpAction_ReachesTheSuppliedHandler()
    {
        using var host = DeclarativeTestFixture.CreateHost();
        var handler = new RecordingMcpToolHandler("tool-output");

        var workflow = host.AgentFactory.CreateDeclarativeWorkflow(
            McpWorkflow,
            new DeclarativeWorkflowHandlers
            {
                McpToolHandler = handler,
                HttpRequestHandler = null,
            });

        var outcome = await DeclarativeTestFixture.RunAsync(
            workflow, "quantum computing", TestContext.Current.CancellationToken);

        Assert.Empty(outcome.Errors);
        var invocation = Assert.Single(handler.Invocations);
        Assert.Equal("https://example.test/mcp", invocation.ServerUrl);
        Assert.Equal("example", invocation.ServerLabel);
        Assert.Equal("search", invocation.ToolName);
    }

    /// <remarks>
    /// Argument values are Power Fx expressions rather than literals, so a handler receives the
    /// resolved value. Asserting on the resolved text rather than merely on the key proves the
    /// expression was evaluated against workflow state before dispatch.
    /// </remarks>
    [Fact]
    public async Task Run_McpAction_DeliversArgumentsAsEvaluatedExpressions()
    {
        using var host = DeclarativeTestFixture.CreateHost();
        var handler = new RecordingMcpToolHandler("tool-output");

        var workflow = host.AgentFactory.CreateDeclarativeWorkflow(
            McpWorkflow,
            new DeclarativeWorkflowHandlers
            {
                McpToolHandler = handler,
                HttpRequestHandler = null,
            });

        await DeclarativeTestFixture.RunAsync(
            workflow, "quantum computing", TestContext.Current.CancellationToken);

        var invocation = Assert.Single(handler.Invocations);
        var query = Assert.Contains("query", invocation.Arguments);
        Assert.Equal("quantum computing", query?.ToString());
    }

    /// <remarks>
    /// The overloads taking no handlers must keep leaving MCP unwired. Were they to acquire a
    /// default, a document could reach a network endpoint the host never agreed to. Upstream
    /// rejects the document while building it, so the failure arrives before anything runs.
    /// </remarks>
    [Fact]
    public void Create_McpActionWithoutHandlers_IsRejectedAtBuild()
    {
        using var host = DeclarativeTestFixture.CreateHost();

        var exception = Assert.Throws<DeclarativeWorkflowParseException>(
            () => host.AgentFactory.CreateDeclarativeWorkflow(McpWorkflow));

        Assert.Contains("McpToolHandler", exception.Message, StringComparison.Ordinal);
    }

    /// <remarks>
    /// An unwired handler being a build-time rejection rather than a run-time one is what makes it
    /// visible to validation, so a host can catch it in a lint step instead of in production.
    /// </remarks>
    [Fact]
    public void Validate_McpActionWithoutHandlers_IsInvalid()
    {
        using var host = DeclarativeTestFixture.CreateHost();

        var validation = host.AgentFactory.ValidateDeclarativeWorkflow(McpWorkflow);

        Assert.False(validation.IsValid);
        Assert.Contains("McpToolHandler", validation.ErrorMessage!, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_NullMcpHandler_IsRejectedTheSameAsNoHandlersAtAll()
    {
        using var host = DeclarativeTestFixture.CreateHost();

        Assert.Throws<DeclarativeWorkflowParseException>(
            () => host.AgentFactory.CreateDeclarativeWorkflow(
                McpWorkflow,
                new DeclarativeWorkflowHandlers
                {
                    McpToolHandler = null,
                    HttpRequestHandler = null,
                }));
    }

    /// <remarks>
    /// A document may mix agent and MCP actions; wiring a handler must not disturb agent
    /// resolution, which is the reason this package exists.
    /// </remarks>
    [Fact]
    public async Task Run_AgentAndMcpActions_BothResolve()
    {
        using var host = DeclarativeTestFixture.CreateHost();
        var handler = new RecordingMcpToolHandler("tool-output");

        var workflow = host.AgentFactory.CreateDeclarativeWorkflow(
            """
            kind: Workflow
            trigger:

              kind: OnConversationStart
              id: mixed_workflow
              actions:

                - kind: InvokeMcpTool
                  id: call_tool
                  serverUrl: https://example.test/mcp
                  toolName: search
                  arguments:
                    query: =System.LastMessage.Text

                - kind: InvokeAzureAgent
                  id: classify
                  agent:
                    name: ClassifierAgent
                  input:
                    messages: =System.LastMessage
                  output:
                    autoSend: true
                    responseObject: Local.Classification
            """,
            new DeclarativeWorkflowHandlers
            {
                McpToolHandler = handler,
                HttpRequestHandler = null,
            });

        var outcome = await DeclarativeTestFixture.RunAsync(
            workflow, "quantum computing", TestContext.Current.CancellationToken);

        Assert.Empty(outcome.Errors);
        Assert.Single(handler.Invocations);
        Assert.Equal(["quantum computing"], host.ChatClient.PromptsFor("classified"));
    }

    /// <remarks>
    /// <c>serverUrl</c> and <c>toolName</c> are the required pair. Upstream rejects a document
    /// missing them at build, so validation catches this one — unlike an unwired handler.
    /// </remarks>
    [Fact]
    public void Validate_McpActionMissingRequiredProperties_IsInvalid()
    {
        using var host = DeclarativeTestFixture.CreateHost();

        var validation = host.AgentFactory.ValidateDeclarativeWorkflow("""
            kind: Workflow
            trigger:

              kind: OnConversationStart
              id: incomplete_workflow
              actions:

                - kind: InvokeMcpTool
                  id: call_tool
                  serverLabel: example
            """);

        Assert.False(validation.IsValid);
    }

    [Fact]
    public void Create_NullHandlers_Throws()
    {
        using var host = DeclarativeTestFixture.CreateHost();

        Assert.Throws<ArgumentNullException>(
            () => host.AgentFactory.CreateDeclarativeWorkflow(McpWorkflow, handlers: null!));
    }
}
