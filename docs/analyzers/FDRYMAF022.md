# FDRYMAF022: Graph contains unreachable agents

## Cause

An agent that declares `[AgentGraphEdge]` for a named graph is not reachable from that graph's `[AgentGraphEntry]` point via any path.

## Rule Description

Every agent in a named graph must be reachable from the entry point through a chain of `[AgentGraphEdge]` declarations. An unreachable agent will never be executed at runtime — its edges are wasted declarations. This usually means the agent was added to the wrong graph or a connecting edge is missing.

## How to Fix

Either add an edge from a reachable agent to the unreachable one, move the agent to the correct graph name, or remove its graph edges if it should not participate.

### Before

```csharp
[FoundryAgent]
[AgentGraphEntry("pipeline")]
[AgentGraphEdge("pipeline", typeof(ReviewerAgent))]
public class PlannerAgent { }

[FoundryAgent]
[AgentGraphNode("pipeline", IsTerminal = true)]
public class ReviewerAgent { }

[FoundryAgent]
[AgentGraphEdge("pipeline", typeof(ReviewerAgent))] // unreachable — no edge leads here
public class QaAgent { }
```

### After

```csharp
[FoundryAgent]
[AgentGraphEntry("pipeline")]
[AgentGraphEdge("pipeline", typeof(QaAgent))]
public class PlannerAgent { }

[FoundryAgent]
[AgentGraphEdge("pipeline", typeof(ReviewerAgent))]
public class QaAgent { }

[FoundryAgent]
[AgentGraphNode("pipeline", IsTerminal = true)]
public class ReviewerAgent { }
```

## When to Suppress

Suppress if you are intentionally keeping the agent declaration for future use while the graph is under construction.

```csharp
#pragma warning disable FDRYMAF022
[AgentGraphEdge("pipeline", typeof(ReviewerAgent))]
public class QaAgent { }
#pragma warning restore FDRYMAF022
```

## See Also

- [AI Integrations — Multi-Agent Orchestration](../ai-integrations.md#multi-agent-orchestration)
- [FDRYMAF016](FDRYMAF016.md) — cycle detected in agent graph
- [FDRYMAF017](FDRYMAF017.md) — graph has no entry point
