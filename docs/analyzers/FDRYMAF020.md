# FDRYMAF020: Graph edge source is not a declared agent

## Cause

A class has `[AgentGraphEdge]` but is not itself decorated with `[FoundryAgent]`.

## Rule Description

The class that carries `[AgentGraphEdge]` is a node in the agent graph and must be registered with Foundry via `[FoundryAgent]`. Without the agent declaration, the source class will not be part of any generated workflow and the edge declaration is meaningless.

## How to Fix

Add `[FoundryAgent]` to the class, or remove `[AgentGraphEdge]` if it was added by mistake.

### Before

```csharp
// Missing [FoundryAgent]
[AgentGraphEdge("pipeline", typeof(ReviewerAgent))]
public class PlannerAgent { }

[FoundryAgent]
[AgentGraphNode("pipeline", IsTerminal = true)]
public class ReviewerAgent { }
```

### After

```csharp
[FoundryAgent]
[AgentGraphEntry("pipeline")]
[AgentGraphEdge("pipeline", typeof(ReviewerAgent))]
public class PlannerAgent { }

[FoundryAgent]
[AgentGraphNode("pipeline", IsTerminal = true)]
public class ReviewerAgent { }
```

## When to Suppress

Suppress if the `[AgentGraphEdge]` is intentionally on a non-agent class for metadata purposes (uncommon).

```csharp
#pragma warning disable FDRYMAF020
[AgentGraphEdge("pipeline", typeof(ReviewerAgent))]
public class PlannerAgent { }
#pragma warning restore FDRYMAF020
```

## See Also

- [AI Integrations — Multi-Agent Orchestration](../ai-integrations.md#multi-agent-orchestration)
- [FDRYMAF019](FDRYMAF019.md) — graph edge target is not a declared agent
- [FDRYMAF003](FDRYMAF003.md) — handoff source is not a declared agent (analogous rule for handoff topology)
