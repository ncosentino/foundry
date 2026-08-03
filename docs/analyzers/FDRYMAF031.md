# FDRYMAF031: Two declared agents publish the same name

## Cause

Two or more classes decorated with `[FoundryAgent]` resolve to the same published name.

An agent's published name is `[FoundryAgent(Name = "...")]` when declared, and the simple class name otherwise. The collision can therefore be between two class names, two declared names, or a declared name and a class name.

## Rule Description

The published name is what identifies an agent everywhere a type cannot:

- the key `IAgentFactory.CreateAgent(string)` resolves, including from a declarative workflow document
- the value of `AIAgent.Name`, and therefore the author recorded on the messages the agent produces
- the `gen_ai.agent.name` telemetry dimension
- the key a hosted agent is registered under for DevUI

Two agents sharing one makes the name ambiguous. `BuildAgentFactory()` throws rather than resolving to whichever registered first, because silently picking one would let a workflow document or a configuration string keep working while addressing an agent nobody intended.

That runtime check only fires once the composition root runs, and only for the agents that particular host registered. The declarations themselves are visible at compile time, so the collision is reported here as well.

## How to Fix

Rename one of the classes, or give one of them a distinct published name.

### Before

```csharp
namespace Support
{
    [FoundryAgent(Instructions = "Triage support requests.")]
    public class TriageAgent { }   // ← publishes "TriageAgent"
}

namespace Billing
{
    [FoundryAgent(Instructions = "Triage billing requests.")]
    public class TriageAgent { }   // ← also publishes "TriageAgent"
}
```

### After

```csharp
namespace Support
{
    [FoundryAgent(
        Name = "SupportTriage",
        Instructions = "Triage support requests.")]
    public class TriageAgent { }   // ← publishes "SupportTriage"
}

namespace Billing
{
    [FoundryAgent(
        Name = "BillingTriage",
        Instructions = "Triage billing requests.")]
    public class TriageAgent { }   // ← publishes "BillingTriage"
}
```

Renaming one of the classes works equally well. Declaring a name is the better choice when the two agents genuinely share the clearest class name for their namespace, or when the published name is referenced from a workflow document that should not have to change again.

## When to Suppress

Do not suppress. The condition is not stylistic: building the agent factory throws on it, so suppressing the diagnostic only defers the same failure to startup.

Both colliding declarations are reported, so suppressing at one of them still leaves the other.

## See Also

- [Declarative workflows](../declarative-workflows.md) — where a document names an agent by its published name
- [FDRYMAF001](FDRYMAF001.md) — handoff target is not a declared agent
