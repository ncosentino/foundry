using Microsoft.Extensions.AI;

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
                name: ClassifierAgent
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
                name: ClassifierAgent
              input:
                messages: =Local.brief
              output:
                autoSend: true
                responseObject: Local.Classification

            - kind: InvokeAzureAgent
              id: respond
              agent:
                name: ResponderAgent
              input:
                messages: =System.LastMessage
              output:
                autoSend: true
                responseObject: Local.Response
        """;

    private const string DeclaredNameWorkflow = """
        kind: Workflow
        trigger:

          kind: OnConversationStart
          id: declared_name_workflow
          actions:

            - kind: InvokeAzureAgent
              id: summarize
              agent:
                name: Summarizer
              input:
                messages: =System.LastMessage
              output:
                autoSend: true
                responseObject: Local.Summary
        """;

    private const string ClassNameWorkflow = """
        kind: Workflow
        trigger:

          kind: OnConversationStart
          id: class_name_workflow
          actions:

            - kind: InvokeAzureAgent
              id: summarize
              agent:
                name: ReportDigestWriter
              input:
                messages: =System.LastMessage
              output:
                autoSend: true
                responseObject: Local.Summary
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

    /// <remarks>
    /// The reason a document should be able to name an agent independently of its class: a workflow
    /// document is hand-edited and never compiled, so binding it to a class name makes a rename a
    /// runtime break with no compile-time signal.
    /// </remarks>
    [Fact]
    public async Task Run_AgentAction_InvokesTheAgentByItsDeclaredNameNotItsClassName()
    {
        using var host = DeclarativeTestFixture.CreateHost();
        var workflow = host.AgentFactory.CreateDeclarativeWorkflow(DeclaredNameWorkflow);

        var outcome = await DeclarativeTestFixture.RunAsync(
            workflow, "quantum computing", TestContext.Current.CancellationToken);

        Assert.Empty(outcome.Errors);
        Assert.Equal(["quantum computing"], host.ChatClient.PromptsFor("summarized"));
    }

    /// <remarks>
    /// Declaring a name replaces the class name rather than adding a second way to say the same
    /// thing, so a document still naming the class must fail. Were the class name kept addressable,
    /// the rename the declaration exists to survive could still break the document.
    /// </remarks>
    [Fact]
    public async Task Run_AgentAction_ClassNameOfAnAgentThatDeclaresAName_FailsNamingTheAction()
    {
        using var host = DeclarativeTestFixture.CreateHost();
        var workflow = host.AgentFactory.CreateDeclarativeWorkflow(ClassNameWorkflow);

        var outcome = await DeclarativeTestFixture.RunAsync(
            workflow, "quantum computing", TestContext.Current.CancellationToken);

        var error = Assert.Single(outcome.Errors);
        Assert.Contains(nameof(ReportDigestWriter), error, StringComparison.Ordinal);
        Assert.Contains("summarize", error, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Summarizer", error, StringComparison.Ordinal);
        Assert.Empty(host.ChatClient.PromptsFor("summarized"));
    }

    /// <remarks>
    /// MAF 1.17 changed declarative invocation so a top-level <see cref="ErrorContent"/> aborts the
    /// action. This differs from a provider exception: the agent returned a valid response object,
    /// but explicitly reported that the run failed. The downstream activity proves the workflow
    /// does not silently continue after that response.
    /// </remarks>
    [Fact]
    public async Task Run_AgentErrorContent_FailsWorkflowBeforeDownstreamAction()
    {
        using var host = DeclarativeTestFixture.CreateHost();
        var workflow = host.AgentFactory.CreateDeclarativeWorkflow("""
            kind: Workflow
            trigger:

              kind: OnConversationStart
              id: failed_agent_workflow
              actions:

                - kind: InvokeAzureAgent
                  id: invoke_failing_agent
                  agent:
                    name: FailingAgent
                  input:
                    messages: =System.LastMessage
                  output:
                    autoSend: true
                    responseObject: Local.Failure

                - kind: SendActivity
                  id: should_not_run
                  activity:
                    text:
                      - Workflow continued after an agent failure.
            """);

        var outcome = await DeclarativeTestFixture.RunAsync(
            workflow,
            "fail now",
            TestContext.Current.CancellationToken);

        var error = Assert.Single(outcome.Errors);
        Assert.Contains("FailingAgent", error, StringComparison.Ordinal);
        Assert.Contains("server_error", error, StringComparison.Ordinal);
        Assert.Contains("The scripted agent failed.", error, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "Workflow continued after an agent failure.",
            outcome.Activities);
        Assert.DoesNotContain("AgentResponseEvent", outcome.ObservedEvents);
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
