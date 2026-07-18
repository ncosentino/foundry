# FDRYMAF027: Terminal node has outgoing edges

## Cause

A node marked with `[AgentGraphNode(IsTerminal = true)]` also has outgoing `[AgentGraphEdge]` declarations.

## Rule Description

A node marked with `IsTerminal = true` on `[AgentGraphNode]` is expected to be a leaf node with no outgoing edges. Having both `IsTerminal = true` and `[AgentGraphEdge]` declarations is contradictory — the node cannot simultaneously be a terminal endpoint and route to other agents. Either remove `IsTerminal = true` or remove the outgoing edges.

## How to Fix

Remove `IsTerminal = true` if the node should route to other agents, or remove the `[AgentGraphEdge]` declarations if it should be a terminal node.

### Before

```csharp
[FoundryAgent]
[AgentGraphEntry("pipeline")]
[AgentGraphEdge("pipeline", typeof(ReviewerAgent))]
public class PlannerAgent { }

[FoundryAgent]
[AgentGraphNode("pipeline", IsTerminal = true)] // marked terminal
[AgentGraphEdge("pipeline", typeof(CoderAgent))] // but has outgoing edge
public class ReviewerAgent { }

[FoundryAgent]
[AgentGraphNode("pipeline", IsTerminal = true)]
public class CoderAgent { }
```

### After (option A — remove IsTerminal)

```csharp
[FoundryAgent]
[AgentGraphEntry("pipeline")]
[AgentGraphEdge("pipeline", typeof(ReviewerAgent))]
public class PlannerAgent { }

[FoundryAgent]
[AgentGraphEdge("pipeline", typeof(CoderAgent))]
public class ReviewerAgent { }

[FoundryAgent]
[AgentGraphNode("pipeline", IsTerminal = true)]
public class CoderAgent { }
```

### After (option B — remove the outgoing edge)

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

Do not suppress this diagnostic. The combination of terminal flag and outgoing edges is always contradictory.

```csharp
#pragma warning disable FDRYMAF027
// Not recommended — contradictory node configuration
#pragma warning restore FDRYMAF027
```

## See Also

- [AI Integrations — Multi-Agent Orchestration](../ai-integrations.md#multi-agent-orchestration)
- [FDRYMAF016](FDRYMAF016.md) — cycle detected in agent graph
- [FDRYMAF024](FDRYMAF024.md) — all edges from fan-out node are optional
