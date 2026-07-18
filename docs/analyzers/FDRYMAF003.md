# FDRYMAF003: [AgentHandoffsTo] source class is not a declared agent

## Cause

A class bearing `[AgentHandoffsTo]` is not itself decorated with `[FoundryAgent]`.

## Rule Description

A handoff relationship is only meaningful when the source of the handoff is itself a Foundry agent. If the class owning `[AgentHandoffsTo]` is not registered with `[FoundryAgent]`, it will not appear in the generated agent registry, and the handoff declaration has no effect at runtime.

## How to Fix

Add `[FoundryAgent]` to the class that declares `[AgentHandoffsTo]`.

### Before

```csharp
[AgentHandoffsTo(typeof(SummaryAgent), When = "Summarization needed")]
public class TriageAgent { }   // ← missing [FoundryAgent]

[FoundryAgent(Instructions = "You summarize content.")]
public class SummaryAgent { }
```

### After

```csharp
[FoundryAgent(Instructions = "Route the request.")]
[AgentHandoffsTo(typeof(SummaryAgent), When = "Summarization needed")]
public class TriageAgent { }   // ← registered

[FoundryAgent(Instructions = "You summarize content.")]
public class SummaryAgent { }
```

## When to Suppress

Only suppress if the class is a temporary scaffold during development.

```csharp
#pragma warning disable FDRYMAF003
[AgentHandoffsTo(typeof(SummaryAgent))]
public class TriageAgent { }
#pragma warning restore FDRYMAF003
```

## See Also

- [AI Integrations — Multi-Agent Orchestration](../ai-integrations.md#multi-agent-orchestration)
- [FDRYMAF001](FDRYMAF001.md) — target type is not a declared agent
