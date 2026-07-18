# FDRYMAF001: [AgentHandoffsTo] target type is not a declared agent

## Cause

A type referenced in `[AgentHandoffsTo(typeof(X))]` is not decorated with `[FoundryAgent]`.

## Rule Description

Every type listed as a handoff target must be a registered Needlr agent. If the target type does not carry `[FoundryAgent]`, the source generator cannot include it in the generated agent registry, and `IWorkflowFactory` will not be able to resolve it at runtime — causing the workflow to fail.

## How to Fix

Add `[FoundryAgent]` to the target type, or remove it from `[AgentHandoffsTo]` if the handoff was declared by mistake.

### Before

```csharp
[FoundryAgent(Instructions = "Route the request.")]
[AgentHandoffsTo(typeof(SummaryAgent), When = "Summarization needed")]
public class TriageAgent { }

public class SummaryAgent { }   // ← missing [FoundryAgent]
```

### After

```csharp
[FoundryAgent(Instructions = "Route the request.")]
[AgentHandoffsTo(typeof(SummaryAgent), When = "Summarization needed")]
public class TriageAgent { }

[FoundryAgent(Instructions = "You summarize content.")]
public class SummaryAgent { }   // ← registered
```

## When to Suppress

Only suppress if you are intentionally referencing a type that is managed outside of Needlr's agent registry (for example, a programmatically created agent that does not participate in source generation).

```csharp
#pragma warning disable FDRYMAF001
[AgentHandoffsTo(typeof(ExternalAgent))]
public class TriageAgent { }
#pragma warning restore FDRYMAF001
```

## See Also

- [AI Integrations — Multi-Agent Orchestration](../ai-integrations.md#multi-agent-orchestration)
- [FDRYMAF003](FDRYMAF003.md) — source class is not a declared agent
