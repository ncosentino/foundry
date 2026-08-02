# Declarative Workflows

Foundry can run Microsoft Agent Framework **declarative workflows** — orchestration
authored in YAML rather than code — against agents your application already owns.

This lives in the optional
`NexusLabs.Foundry.MicrosoftAgentFramework.Workflows.Declarative` package. The core
package never references it, and no other Foundry package acquires it transitively.

## Why this package exists

Upstream ships a declarative runtime, but the only agent provider it ships resolves
agent names against a deployed **Azure AI Foundry** project, and that companion package
is pre-release. Every upstream sample assumes it.

The `InvokeAzureAgent` action is not actually Azure-specific: it resolves through an
abstract `ResponseAgentProvider`. This package supplies one backed by your own agents,
so a declarative workflow can drive in-process agents with no remote project involved.

## Running a workflow

Agents are declared the same way as anywhere else in Foundry and resolved through
`IAgentFactory`, so a declarative workflow needs no registration code of its own:

```csharp
[FoundryAgent(Description = "...", Instructions = "...")]
public sealed class ClassifierAgent { }

// composition root
services.AddFoundryAgentFramework(builder => builder
    .UsingChatClient(chatClient)
    .AddAgent<ClassifierAgent>()
    .AddAgent<ResponderAgent>());

var agentFactory = serviceProvider.GetRequiredService<IAgentFactory>();

Workflow workflow = agentFactory.CreateDeclarativeWorkflow(
    File.ReadAllText("triage-workflow.yaml"));

StreamingRun run = await InProcessExecution.RunStreamingAsync(
    workflow,
    "I cannot log in to my account",
    checkpointManager: CheckpointManager.CreateInMemory());

await foreach (var workflowEvent in run.WatchStreamAsync().ReportProgressTo(reporter, ct))
{
    // The upstream stream is forwarded unchanged; progress is reported as a side effect.
}
```

`CreateDeclarativeWorkflow` and `ValidateDeclarativeWorkflow` are extension methods on
`IAgentFactory` rather than members of `IWorkflowFactory`. Agent resolution is the only
thing declarative composition needs from Foundry, and `IWorkflowFactory` lives in the
core package, which cannot depend on this one without pulling an interpreted expression
engine into every consumer.

The document names agents by type name:

```yaml
kind: Workflow
trigger:

  kind: OnConversationStart
  id: triage_workflow
  actions:

    - kind: SetVariable
      id: capture_report
      variable: Local.report
      value: =System.LastMessage.Text

    - kind: InvokeAzureAgent
      id: classify
      agent:
        name: DeclarativeWorkflowApp.ClassifierAgent
      input:
        messages: =Local.report
      output:
        autoSend: true
        responseObject: Local.Classification
```

Agent names are matched exactly as `IAgentFactory.CreateAgent(string)` matches them, which
today is the fully-qualified type name. That couples a hand-edited document to a namespace
and type name, so renaming or moving an agent class breaks the workflow with no compile-time
error — the document is not compiled, and there is no schema to validate it against.

A complete runnable example is in
`src/Examples/AgentFramework/DeclarativeWorkflowApp`. It runs entirely offline.

## Validation

Declarative workflows have **no published JSON Schema**, so parsing is the only
validation available. `Validate` makes it deliberate:

```csharp
var validation = agentFactory.ValidateDeclarativeWorkflow(workflowYaml);
if (!validation.IsValid)
{
    Console.WriteLine(validation.ErrorMessage);
}
```

It reports what upstream's builder rejects, which is structural malformation. It does
**not** catch every authoring mistake:

- an unrecognized action `kind` is accepted without complaint;
- an expression that will fail to evaluate cannot be detected until it runs;
- an agent name that is not registered cannot be detected until the action runs.

## Progress reporting

`ReportProgressTo` bridges the upstream event stream onto Foundry progress. Action
lifecycle is taken from upstream's declarative events rather than the generic executor
events, because only those carry the **action id written in the document**:

```
action-started:capture_report:SetVariable
action-started:classify:InvokeAzureAgent
```

It is a pass-through: the original stream is yielded unchanged, so a caller can report
progress and still do its own handling.

## Limitations

These are properties of the upstream feature, measured rather than assumed.

### Not NativeAOT compatible

The package is deliberately excluded from Foundry's NativeAOT profile. A probe that
parsed a declarative document and published with `PublishAot=true` produced **44 trim
warnings and 41 AOT warnings**, and the resulting native binary failed at startup:

```
NotSupportedException: 'Microsoft.Agents.ObjectModel.RecurrenceFrequency[]'
is missing native code or metadata.
```

That is a static type initializer, so nothing declarative runs at all under AOT. The
cause is upstream-owned — Power Fx is an interpreter doing reflective function binding,
and the Power Platform object model maps types reflectively — so it is not fixable from
Foundry. Do not reference this package from a project you intend to publish AOT.

### No fan-out / fan-in

Declarative YAML has no parallel execution construct
([upstream #2765](https://github.com/microsoft/agent-framework/issues/2765)). If you need
concurrent branches, joins, or reducers, use Foundry's `GraphWorkflowRunner`, which is
what it exists for. This package sits beside it rather than replacing it.

### No declarative retry, timeout, or error handling

There are no `retry`, `timeout`, or `onError` constructs in the document format. Handle
failures in code around the run, or in a custom `IHttpRequestHandler` for HTTP actions.

### Documentation defects to be aware of

Two things in Microsoft's published guidance do not reproduce:

- The greeting sample calls `Concat` with four arguments. Power Fx's `Concat` is the
  table-aggregation function and accepts two to three, so the sample as published fails
  with `Invalid number of arguments: received 4, expected 2-3`. String concatenation is
  `Concatenate` or the `&` operator.
- `SendActivity` with `activity: =SomeExpression` emits the **raw template** rather than
  the evaluated text, despite the documented sample output showing otherwise.

Expressions do evaluate — `SetVariable` values and agent inputs both work — so prefer
carrying computed values into an agent input or variable rather than relying on
`SendActivity` to render an expression.

## Package boundaries

- Optional: the core `NexusLabs.Foundry.MicrosoftAgentFramework` package never
  references it.
- Excluded from the NativeAOT example and profile.
- Pinned to `Microsoft.Agents.AI.Workflows.Declarative` 1.15.0, matching the rest of the
  Agent Framework pin. No version bump was required to adopt it.
