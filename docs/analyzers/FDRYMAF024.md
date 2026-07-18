# FDRYMAF024: All edges from fan-out node are optional

## Cause

A fan-out node (a node with multiple outgoing edges) has all edges marked as `IsRequired = false`.

## Rule Description

When all outgoing edges from a fan-out node have `IsRequired = false`, every downstream branch is optional. If all optional branches fail at runtime, the downstream nodes receive no input and the graph may produce empty or unexpected results. Consider making at least one edge required to guarantee that the graph produces meaningful output.

## How to Fix

Set `IsRequired = true` on at least one outgoing edge to ensure the graph always has a guaranteed execution path.

### Before

```csharp
[FoundryAgent]
[AgentGraphEntry("pipeline")]
[AgentGraphEdge("pipeline", typeof(ReviewerAgent), IsRequired = false)]
[AgentGraphEdge("pipeline", typeof(QaAgent), IsRequired = false)]
public class PlannerAgent { } // all edges optional

[FoundryAgent]
[AgentGraphNode("pipeline", IsTerminal = true)]
public class ReviewerAgent { }

[FoundryAgent]
[AgentGraphNode("pipeline", IsTerminal = true)]
public class QaAgent { }
```

### After

```csharp
[FoundryAgent]
[AgentGraphEntry("pipeline")]
[AgentGraphEdge("pipeline", typeof(ReviewerAgent), IsRequired = true)]
[AgentGraphEdge("pipeline", typeof(QaAgent), IsRequired = false)]
public class PlannerAgent { } // at least one required edge

[FoundryAgent]
[AgentGraphNode("pipeline", IsTerminal = true)]
public class ReviewerAgent { }

[FoundryAgent]
[AgentGraphNode("pipeline", IsTerminal = true)]
public class QaAgent { }
```

## When to Suppress

Suppress if empty results from the fan-out are acceptable for your workflow and you handle the case downstream (e.g. via a fallback or default response).

```csharp
#pragma warning disable FDRYMAF024
[AgentGraphEdge("pipeline", typeof(ReviewerAgent), IsRequired = false)]
[AgentGraphEdge("pipeline", typeof(QaAgent), IsRequired = false)]
public class PlannerAgent { }
#pragma warning restore FDRYMAF024
```

## See Also

- [AI Integrations — Multi-Agent Orchestration](../ai-integrations.md#multi-agent-orchestration)
- [FDRYMAF016](FDRYMAF016.md) — cycle detected in agent graph
- [FDRYMAF027](FDRYMAF027.md) — terminal node has outgoing edges
