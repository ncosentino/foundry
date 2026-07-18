# FDRYMAF018: Graph has multiple entry points

## Cause

A named agent graph has more than one `[AgentGraphEntry]` declaration.

## Rule Description

Each named agent graph must have exactly one `[AgentGraphEntry]`. Multiple entry points create ambiguity about where execution begins — the source generator cannot determine which agent should receive the initial input. Remove the extra `[AgentGraphEntry]` attributes so only one remains for the graph name.

## How to Fix

Remove `[AgentGraphEntry]` from all but one agent in the graph.

### Before

```csharp
[FoundryAgent]
[AgentGraphEntry("pipeline")] // first entry point
[AgentGraphEdge("pipeline", typeof(CoderAgent))]
public class PlannerAgent { }

[FoundryAgent]
[AgentGraphEntry("pipeline")] // second entry point — error
[AgentGraphEdge("pipeline", typeof(CoderAgent))]
public class DesignerAgent { }

[FoundryAgent]
[AgentGraphNode("pipeline", IsTerminal = true)]
public class CoderAgent { }
```

### After

```csharp
[FoundryAgent]
[AgentGraphEntry("pipeline")]
[AgentGraphEdge("pipeline", typeof(CoderAgent))]
public class PlannerAgent { }

[FoundryAgent]
[AgentGraphEdge("pipeline", typeof(CoderAgent))]
public class DesignerAgent { }

[FoundryAgent]
[AgentGraphNode("pipeline", IsTerminal = true)]
public class CoderAgent { }
```

## When to Suppress

Do not suppress this diagnostic. Multiple entry points in a single graph are always a topology error.

```csharp
#pragma warning disable FDRYMAF018
// Not recommended — ambiguous graph entry
#pragma warning restore FDRYMAF018
```

## See Also

- [AI Integrations — Multi-Agent Orchestration](../ai-integrations.md#multi-agent-orchestration)
- [FDRYMAF017](FDRYMAF017.md) — graph has no entry point
- [FDRYMAF021](FDRYMAF021.md) — graph entry point is not a declared agent
