namespace NexusLabs.Foundry.MicrosoftAgentFramework.Workflows.Declarative.Tests;

/// <summary>
/// Proves a declarative workflow can drive Foundry-registered agents, and that the provider supplies
/// enough conversation state for the document's expression language to evaluate.
/// </summary>
public sealed class FoundryDeclarativeWorkflowTests
{
    /// <remarks>
    /// The agent input is an expression rather than a literal, so a passing assertion on what the
    /// agent received proves two things at once: that <c>System.LastMessage</c> resolved from
    /// provider-held conversation state, and that Power Fx evaluated against it. A provider that only
    /// returned responses would fail this.
    /// </remarks>
    private const string AgentWorkflow = """
        kind: Workflow
        trigger:

          kind: OnConversationStart
          id: agent_workflow
          actions:

            - kind: InvokeAzureAgent
              id: call_writer
              agent:
                name: Writer
              input:
                messages: =System.LastMessage
              output:
                autoSend: true
                responseObject: Local.WriterResult
        """;

    private const string ChainedAgentWorkflow = """
        kind: Workflow
        trigger:

          kind: OnConversationStart
          id: chained_workflow
          actions:

            - kind: SetVariable
              id: build_brief
              variable: Local.brief
              value: =System.LastMessage.Text & " (brief)"

            - kind: InvokeAzureAgent
              id: call_writer
              agent:
                name: Writer
              input:
                messages: =Local.brief
              output:
                autoSend: true
                responseObject: Local.WriterResult
        """;

    /// <remarks>
    /// Structural malformation is used rather than an unrecognized action kind, because upstream's
    /// builder accepts unknown kinds without complaint; see the remarks on
    /// <see cref="FoundryDeclarativeWorkflowFactory.Validate"/>.
    /// </remarks>
    private const string MalformedWorkflow = """
        kind: Workflow
        trigger:

          kind: OnConversationStart
          id: broken_workflow
          actions:

            - kind: SendActivity
              id: nope
              activity:
                text: this-should-be-a-sequence
        """;

    [Fact]
    public async Task Run_AgentAction_InvokesTheRegisteredFoundryAgent()
    {
        var host = DeclarativeTestFixture.CreateHost(("Writer", "written:"));
        var workflow = new FoundryDeclarativeWorkflowFactory(host.Provider).Create(AgentWorkflow);

        var outcome = await DeclarativeTestFixture.RunAsync(
            workflow, "quantum computing", TestContext.Current.CancellationToken);

        Assert.Empty(outcome.Errors);
        Assert.Equal(["quantum computing"], host.Clients["Writer"].ObservedPrompts);
    }

    [Fact]
    public async Task Run_ExpressionDerivedAgentInput_EvaluatesAgainstProviderConversationState()
    {
        var host = DeclarativeTestFixture.CreateHost(("Writer", "written:"));
        var workflow = new FoundryDeclarativeWorkflowFactory(host.Provider)
            .Create(ChainedAgentWorkflow);

        var outcome = await DeclarativeTestFixture.RunAsync(
            workflow, "quantum computing", TestContext.Current.CancellationToken);

        Assert.Empty(outcome.Errors);
        Assert.Equal(["quantum computing (brief)"], host.Clients["Writer"].ObservedPrompts);
    }

    /// <remarks>
    /// Pins the runtime contract this provider is built against, because it is not documented and
    /// the provider's shape depends on it: the runtime records both the user and the assistant turn
    /// itself, and invokes the agent with a null conversation id and exactly the messages the
    /// document's input expression resolved to. If any of that changes upstream, this fails rather
    /// than the provider silently double-recording turns or feeding agents unrequested history.
    /// </remarks>
    [Fact]
    public async Task Run_AgentAction_RuntimeOwnsConversationRecording()
    {
        var host = DeclarativeTestFixture.CreateHost(("Writer", "written:"));
        var workflow = new FoundryDeclarativeWorkflowFactory(host.Provider).Create(AgentWorkflow);

        await DeclarativeTestFixture.RunAsync(
            workflow, "quantum computing", TestContext.Current.CancellationToken);

        // A conversation the provider created and never wrote to would be empty; the runtime having
        // recorded both turns is what makes these present.
        var conversationId = Assert.Single(host.Provider.ConversationIds);
        var recorded = new List<string>();
        await foreach (var message in host.Provider.GetMessagesAsync(
            conversationId,
            limit: null,
            after: null,
            before: null,
            newestFirst: false,
            TestContext.Current.CancellationToken))
        {
            recorded.Add($"{message.Role}:{message.Text}");
        }

        Assert.Equal(
            ["user:quantum computing", "assistant:written:quantum computing"],
            recorded);

        // Exactly one agent turn: the provider must not have replayed history back into the agent.
        Assert.Equal(["quantum computing"], host.Clients["Writer"].ObservedPrompts);
    }

    [Fact]
    public async Task Run_UnregisteredAgent_ReportsTheOffendingName()
    {
        var host = DeclarativeTestFixture.CreateHost(("SomeoneElse", "x:"));
        var workflow = new FoundryDeclarativeWorkflowFactory(host.Provider).Create(AgentWorkflow);

        var outcome = await DeclarativeTestFixture.RunAsync(
            workflow, "input", TestContext.Current.CancellationToken);

        Assert.NotEmpty(outcome.Errors);
        Assert.Contains(outcome.Errors, error => error.Contains("Writer", StringComparison.Ordinal));
    }

    [Fact]
    public void Create_MalformedDocument_ThrowsParseException()
    {
        var factory = new FoundryDeclarativeWorkflowFactory(DeclarativeTestFixture.CreateHost().Provider);

        Assert.Throws<DeclarativeWorkflowParseException>(() => factory.Create(MalformedWorkflow));
    }

    [Fact]
    public void Validate_MalformedDocument_ReportsInvalidWithDetail()
    {
        var factory = new FoundryDeclarativeWorkflowFactory(DeclarativeTestFixture.CreateHost().Provider);

        var result = factory.Validate(MalformedWorkflow);

        Assert.False(result.IsValid);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public void Validate_UnrecognizedActionKind_ReportsValidBecauseUpstreamAcceptsIt()
    {
        var factory = new FoundryDeclarativeWorkflowFactory(DeclarativeTestFixture.CreateHost().Provider);

        // Pins the documented limitation rather than the behavior anyone would want: upstream's
        // builder does not reject an action kind it does not recognize, so validation cannot report
        // one. If upstream starts rejecting it, this test fails and the docs need updating.
        var result = factory.Validate("""
            kind: Workflow
            trigger:

              kind: OnConversationStart
              id: unknown_kind_workflow
              actions:

                - kind: ThisActionKindDoesNotExist
                  id: nope
            """);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_WellFormedDocument_ReportsValid()
    {
        var factory = new FoundryDeclarativeWorkflowFactory(
            DeclarativeTestFixture.CreateHost(("Writer", "written:")).Provider);

        var result = factory.Validate(AgentWorkflow);

        Assert.True(result.IsValid);
        Assert.Null(result.ErrorMessage);
    }
}
