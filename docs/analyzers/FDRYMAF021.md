# FDRYMAF021: Graph entry point is not a declared agent

## Cause

A class has `[AgentGraphEntry]` but is not itself decorated with `[FoundryAgent]`.

## Rule Description

The class that carries `[AgentGraphEntry]` is the starting node of the graph workflow and must be registered with Needlr via `[FoundryAgent]`. Without the agent declaration, the entry point class will not be discovered by the source generator and the graph workflow cannot be created.

## How to Fix

Add `[FoundryAgent]` to the class, or remove `[AgentGraphEntry]` if it was added by mistake.

### Before

```csharp
// Missing [FoundryAgent]
[AgentGraphEntry("pipeline")]
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

Suppress if the `[AgentGraphEntry]` is intentionally on a non-agent class for metadata purposes (uncommon).

```csharp
#pragma warning disable FDRYMAF021
[AgentGraphEntry("pipeline")]
public class PlannerAgent { }
#pragma warning restore FDRYMAF021
```

## See Also

- [AI Integrations — Multi-Agent Orchestration](../ai-integrations.md#multi-agent-orchestration)
- [FDRYMAF017](FDRYMAF017.md) — graph has no entry point
- [FDRYMAF020](FDRYMAF020.md) — graph edge source is not a declared agent
