using NexusLabs.Foundry.MicrosoftAgentFramework;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Workflows.Declarative.Tests;

/// <summary>
/// Proves a declarative workflow drives agents declared with <see cref="FoundryAgentAttribute"/> and
/// resolved through <see cref="IAgentFactory"/>, and that the provider supplies enough conversation
/// state for the document's expression language to evaluate.
/// </summary>
public sealed class DeclarativeWorkflowTests
{
    /// <remarks>
    /// The agent input is an expression rather than a literal, so an assertion on what the agent
    /// received proves two things at once: that <c>System.LastMessage</c> resolved from
    /// provider-held conversation state, and that Power Fx evaluated against it.
    /// </remarks>
    private const string ClassifyWorkflow = """
        kind: Workflow
        trigger:

          kind: OnConversationStart
          id: classify_workflow
          actions:

            - kind: InvokeAzureAgent
              id: classify
              agent:
                name: NexusLabs.Foundry.MicrosoftAgentFramework.Workflows.Declarative.Tests.ClassifierAgent
              input:
                messages: =System.LastMessage
              output:
                autoSend: true
                responseObject: Local.Classification
        """;

    private const string TwoAgentWorkflow = """
        kind: Workflow
        trigger:

          kind: OnConversationStart
          id: two_agent_workflow
          actions:

            - kind: SetVariable
              id: build_brief
              variable: Local.brief
              value: =System.LastMessage.Text & " (brief)"

            - kind: InvokeAzureAgent
              id: classify
              agent:
                name: NexusLabs.Foundry.MicrosoftAgentFramework.Workflows.Declarative.Tests.ClassifierAgent
              input:
                messages: =Local.brief
              output:
                autoSend: true
                responseObject: Local.Classification

            - kind: InvokeAzureAgent
              id: respond
              agent:
                name: NexusLabs.Foundry.MicrosoftAgentFramework.Workflows.Declarative.Tests.ResponderAgent
              input:
                messages: =System.LastMessage
              output:
                autoSend: true
                responseObject: Local.Response
        """;

    /// <remarks>
    /// Structural malformation is used rather than an unrecognized action kind, because upstream's
    /// builder accepts unknown kinds without complaint; see the remarks on
    /// <see cref="DeclarativeWorkflowAgentFactoryExtensions.ValidateDeclarativeWorkflow"/>.
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
    public async Task Run_AgentAction_InvokesTheDeclaredAgentByClassName()
    {
        using var host = DeclarativeTestFixture.CreateHost();
        var workflow = host.AgentFactory.CreateDeclarativeWorkflow(ClassifyWorkflow);

        var outcome = await DeclarativeTestFixture.RunAsync(
            workflow, "quantum computing", TestContext.Current.CancellationToken);

        Assert.Empty(outcome.Errors);
        Assert.Equal(["quantum computing"], host.ChatClient.PromptsFor("classified"));
    }

    [Fact]
    public async Task Run_ExpressionDerivedAgentInput_EvaluatesAgainstProviderConversationState()
    {
        using var host = DeclarativeTestFixture.CreateHost();
        var workflow = host.AgentFactory.CreateDeclarativeWorkflow(TwoAgentWorkflow);

        var outcome = await DeclarativeTestFixture.RunAsync(
            workflow, "quantum computing", TestContext.Current.CancellationToken);

        Assert.Empty(outcome.Errors);
        Assert.Equal(["quantum computing (brief)"], host.ChatClient.PromptsFor("classified"));
    }

    [Fact]
    public async Task Run_MultipleAgents_ResolvesEachDeclaredAgentSeparately()
    {
        using var host = DeclarativeTestFixture.CreateHost();
        var workflow = host.AgentFactory.CreateDeclarativeWorkflow(TwoAgentWorkflow);

        var outcome = await DeclarativeTestFixture.RunAsync(
            workflow, "quantum computing", TestContext.Current.CancellationToken);

        Assert.Empty(outcome.Errors);
        Assert.Single(host.ChatClient.PromptsFor("classified"));
        Assert.Single(host.ChatClient.PromptsFor("responded"));
    }

    /// <remarks>
    /// Pins the runtime contract this provider is built against, because it is not documented and
    /// the provider's shape depends on it: the runtime records both the user and the assistant turn
    /// itself, and invokes the agent with exactly the messages the document's input expression
    /// resolved to. If that changes upstream, this fails rather than the provider silently
    /// double-recording turns or feeding agents unrequested history.
    /// </remarks>
    [Fact]
    public async Task Run_AgentAction_RuntimeOwnsConversationRecording()
    {
        using var host = DeclarativeTestFixture.CreateHost();
        var provider = new FoundryAgentProvider(host.AgentFactory);
        var workflow = host.AgentFactory.CreateDeclarativeWorkflow(ClassifyWorkflow);

        await DeclarativeTestFixture.RunAsync(
            workflow, "quantum computing", TestContext.Current.CancellationToken);

        // Exactly one agent turn: the provider must not have replayed stored history into the agent.
        Assert.Equal(["quantum computing"], host.ChatClient.PromptsFor("classified"));
        Assert.Empty(provider.ConversationIds);
    }

    [Fact]
    public void Create_UnregisteredAgent_IsNotDetectedUntilTheActionRuns()
    {
        using var host = DeclarativeTestFixture.CreateHost();

        // Building succeeds because agent names are resolved at invocation time, not at parse time.
        var workflow = host.AgentFactory.CreateDeclarativeWorkflow("""
            kind: Workflow
            trigger:

              kind: OnConversationStart
              id: unknown_agent_workflow
              actions:

                - kind: InvokeAzureAgent
                  id: call_missing
                  agent:
                    name: NotDeclaredAgent
                  input:
                    messages: =System.LastMessage
            """);

        Assert.NotNull(workflow);
    }

    [Fact]
    public async Task Run_UnregisteredAgent_ReportsTheAgentFactoryDiagnostic()
    {
        using var host = DeclarativeTestFixture.CreateHost();
        var workflow = host.AgentFactory.CreateDeclarativeWorkflow("""
            kind: Workflow
            trigger:

              kind: OnConversationStart
              id: unknown_agent_workflow
              actions:

                - kind: InvokeAzureAgent
                  id: call_missing
                  agent:
                    name: NotDeclaredAgent
                  input:
                    messages: =System.LastMessage
            """);

        var outcome = await DeclarativeTestFixture.RunAsync(
            workflow, "input", TestContext.Current.CancellationToken);

        var error = Assert.Single(outcome.Errors);
        Assert.Contains("NotDeclaredAgent", error, StringComparison.Ordinal);
        Assert.Contains("FoundryAgent", error, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_MalformedDocument_ThrowsParseException()
    {
        using var host = DeclarativeTestFixture.CreateHost();

        Assert.Throws<DeclarativeWorkflowParseException>(
            () => host.AgentFactory.CreateDeclarativeWorkflow(MalformedWorkflow));
    }

    [Fact]
    public void Validate_MalformedDocument_ReportsInvalidWithDetail()
    {
        using var host = DeclarativeTestFixture.CreateHost();

        var result = host.AgentFactory.ValidateDeclarativeWorkflow(MalformedWorkflow);

        Assert.False(result.IsValid);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public void Validate_WellFormedDocument_ReportsValid()
    {
        using var host = DeclarativeTestFixture.CreateHost();

        var result = host.AgentFactory.ValidateDeclarativeWorkflow(ClassifyWorkflow);

        Assert.True(result.IsValid);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void Validate_UnrecognizedActionKind_ReportsValidBecauseUpstreamAcceptsIt()
    {
        using var host = DeclarativeTestFixture.CreateHost();

        // Pins the documented limitation rather than the behavior anyone would want: upstream's
        // builder does not reject an action kind it does not recognize, so validation cannot report
        // one. If upstream starts rejecting it, this fails and the docs need updating.
        var result = host.AgentFactory.ValidateDeclarativeWorkflow("""
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
}
