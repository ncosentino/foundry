---
title: "ADR-0014: Phase-local Harness background delegation"
status: "Accepted"
date: "2026-08-08"
authors: ["Nick Cosentino"]
tags: ["architecture", "decision", "agent-framework", "harness", "delegation", "concurrency"]
supersedes: ""
superseded_by: ""
---

# ADR-0014: Phase-local Harness background delegation

## Context and scope

ADR-0008 established the optional complete-bundle Harness package and left
background-agent delegation outside its initial public candidate. ADR-0013
later exposed upstream loop evaluation for phase-local self-correction.

Microsoft Agent Framework 1.17 provides
`BackgroundAgentsProvider`, configured through
`HarnessAgentOptions.BackgroundAgents` and
`BackgroundAgentsProviderOptions`. The provider gives a parent agent six tools
to start concurrent child tasks, wait for completion, retrieve and continue
results, list state, and clear terminal tasks.

This decision governs background delegation in the optional
`NexusLabs.Foundry.MicrosoftAgentFramework.Harness` package. It does not define
the outer macro workflow, dynamic child creation, distributed scheduling, or a
neutral Foundry delegation protocol.

## Decision drivers

- Let one autonomous phase parallelize bounded internal work without changing
  the developer-authored macro topology.
- Reuse the official MAF provider and task lifecycle rather than creating
  another delegation runtime.
- Keep the child catalog explicit and inspectable.
- Reject invalid names, duplicates, ignored options, and tool collisions before
  constructing an agent.
- State cancellation, recovery, cleanup, trust, telemetry, and NativeAOT limits
  accurately.
- Preserve the option to combine delegation with the upstream
  `BackgroundTaskCompletionLoopEvaluator`.

## Decision

Foundry maps upstream background-agent types directly through the optional
complete-bundle configuration.

`FoundryHarnessFeatureSelections` requires an explicit
`EnableBackgroundAgents` choice. `FoundryHarnessAgentConfiguration` requires a
non-null child-agent list and an explicit nullable
`BackgroundAgentsProviderOptions` value.

The valid states are:

| Selection | Child agents | Options | Effective behavior |
| --- | --- | --- | --- |
| Disabled | Empty | `null` | No provider or background tools |
| Enabled | Non-empty | `null` | Upstream provider defaults |
| Enabled | Non-empty | Non-null | Caller-supplied upstream options |

Every other combination fails before construction. Null agents, blank names,
and case-insensitive duplicate names also fail closed.

Foundry reserves the six upstream `background_agents_*` tool names whenever
the feature is enabled, preventing caller tools from silently shadowing the
provider. The ordered child collection and provider options are copied and
assigned directly to `HarnessAgentOptions`; Foundry does not wrap child agents
or interpret task results.

Background delegation is **phase-local**. The macro workflow owns phase
dependencies and artifact boundaries. A phase agent may use the provider to
parallelize internal research or analysis, but production correctness does not
depend on one exact child-call count or ordering.

The provider's runtime behavior establishes explicit constraints:

- every task creates a separate child session and tasks execute concurrently;
- the provider imposes no task-count, concurrency, timeout, retry, or
  cancellation bound and offers only a wait-for-first join tool;
- started child runs receive a non-cancelable token, so canceling the parent
  does not cancel them;
- serializable task metadata survives parent-session serialization, but
  in-flight `Task` and child-session references do not; restored running tasks
  become `Lost`;
- failed, completed, and lost tasks are terminal;
- terminal tasks remain in provider state until explicitly cleared;
- continued tasks reuse their child session while that runtime reference still
  exists; and
- task metadata exposes task and agent identity but not the child session ID;
- the result tool stores only `AgentResponse.Text`, so approval-only and other
  non-text child responses become empty text instead of propagating their
  content to the parent; and
- child output is text that re-enters the parent model's context and must be
  treated as untrusted input.

The MAF 1.17 implementation inserts the rendered child catalog into custom
provider instructions only by replacing the `{background_agents}` placeholder.
Without that placeholder the catalog is omitted, despite upstream XML
documentation saying it is always appended. Foundry pins the implemented
behavior with an interaction test rather than silently rewriting the caller's
instructions.

Callers are responsible for independently bounding child work, avoiding
irreversible duplicate effects, and clearing terminal tasks. The optional
upstream `BackgroundTaskCompletionLoopEvaluator` may keep the parent running
while tasks are still active, but it does not change the cancellation or
restore limitations above. Background work cannot be combined coherently with
`FreshContextPerIteration`, because resetting the parent session discards
runtime task/session references and marks running tasks lost; Foundry rejects
that combination.

## Alternatives considered

### Leave background agents available only through raw upstream construction

This avoids a source-breaking Foundry configuration change. It was rejected
because complete-bundle consumers would have to bypass Foundry's explicit
factory, effective-default reporting, collision checks, and validation to use a
core Harness capability.

### Build a Foundry task scheduler or delegation abstraction

This could eventually unify other providers or distributed workers. It was
rejected because Foundry currently has one concrete upstream implementation
and no evidence for a stable neutral contract. It would also risk hiding
important MAF semantics such as lost tasks after restore.

### Use the macro graph itself for every specialist

That remains appropriate when specialist phases and dependencies are part of
the known outer architecture. It was rejected as the only composition model
because some phases need freedom to choose and parallelize internal work
without adding another brittle workflow edge.

## Consequences

### Positive

- Harness phases can perform bounded concurrent delegation.
- Child identity and built-in tool names are validated before construction.
- The upstream provider remains the only task/session implementation.
- Loop evaluation can be combined with upstream background-task completion.
- A fixed child catalog can be included in the NativeAOT capability profile.

### Negative

- Existing configuration initializers gain three required members.
- Parent cancellation cannot stop already-started child work.
- In-flight work cannot resume after session restoration and becomes lost.
- Terminal task state consumes memory until cleared.
- Child results create an indirect prompt-injection and data-exposure boundary.
- Background tasks cannot surface an interactive child approval through the
  provider's text-result contract.
- Background child model/tool telemetry is not automatically represented as
  Foundry parent progress events.

### Neutral

- The outer macro workflow and artifact contracts remain separate concerns.
- Child agents remain ordinary caller-supplied `AIAgent` instances.
- Distributed execution, durable task queues, and recursive delegation are not
  introduced.
- The feature remains experimental with upstream diagnostic `MAAI001`.

## Confirmation

The decision is confirmed by:

- validation tests for missing, null, blank, duplicate, disabled, and
  colliding configurations;
- direct invocation of all provider lifecycle tools through the actual
  injected `AIFunction` instances;
- concurrency, completion, continuation, failure, cancellation, cleanup, and
  lost-on-restore interaction tests;
- effective-default reporting;
- NativeAOT capability construction with a fixed child catalog; and
- strict documentation, package, and hosted CI validation.

## References

- ADR-0008 defines the optional complete-bundle boundary retained here.
- ADR-0013 defines phase-local outer-loop correction and its interaction with
  background task completion.
- `FoundryHarnessAgentFactory` maps the explicit Foundry configuration to the
  official upstream `HarnessAgentOptions`.
- [Microsoft Agent Framework Harness](https://learn.microsoft.com/en-us/agent-framework/agents/harness)
- [MAF 1.17 BackgroundAgentsProvider](https://github.com/microsoft/agent-framework/blob/1da571860a60f0da4f060bd60c8e7ee8d092cd32/dotnet/src/Microsoft.Agents.AI/Harness/BackgroundAgentsProvider.cs)
