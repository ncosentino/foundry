---
title: "ADR-0013: Phase-local Harness loop evaluation"
status: "Accepted"
date: "2026-08-08"
authors: ["Nick Cosentino"]
tags: ["architecture", "decision", "agent-framework", "harness", "evaluation", "retries"]
supersedes: ""
superseded_by: ""
---

# ADR-0013: Phase-local Harness loop evaluation

## Context and scope

ADR-0008 established the optional complete-bundle Harness package and
deliberately left upstream loop evaluation outside its initial public
candidate. The delivered bundle can autonomously call tools, maintain todos,
use plan/execute modes, persist history, and manage context, but a caller
cannot configure the upstream outer loop through Foundry.

Microsoft Agent Framework 1.17 provides `LoopAgent`, `LoopEvaluator`, and
`LoopAgentOptions`. When configured on `HarnessAgentOptions`, the complete
Harness agent is wrapped in an outer `LoopAgent`. Evaluators inspect the result
after each complete agent run and may request another iteration with feedback
or explicit messages.

This decision governs loop evaluation in the optional
`NexusLabs.Foundry.MicrosoftAgentFramework.Harness` package. It does not define
macro workflow retries, background-agent delegation, a neutral evaluator
abstraction, or a general planner.

## Decision drivers

- Let one autonomous phase validate and revise its own artifact before crossing
  a macro workflow boundary.
- Preserve the upstream Harness pipeline and one loop owner.
- Keep loop configuration explicit rather than inheriting an upstream opt-in.
- Avoid duplicating experimental MAF types before Foundry has evidence for a
  provider-neutral contract.
- Make ignored and incoherent configurations fail before agent construction.
- Preserve accurate progress, telemetry, NativeAOT, and side-effect guidance.

## Decision

Foundry exposes phase-local Harness loop evaluation by mapping upstream types
directly through the optional complete-bundle configuration.

`FoundryHarnessFeatureSelections` requires an explicit
`EnableLoopEvaluation` choice. `FoundryHarnessAgentConfiguration` requires a
non-null evaluator list and an explicit nullable `LoopAgentOptions` value.

The valid states are:

| Selection | Evaluators | Options | Effective behavior |
| --- | --- | --- | --- |
| Disabled | Empty | `null` | No outer loop |
| Enabled | Non-empty | `null` | Upstream `LoopAgentOptions` defaults |
| Enabled | Non-empty | Non-null | Caller-supplied upstream options |

Every other combination fails before construction. Null evaluator elements
and non-positive explicit iteration limits also fail closed.

Foundry copies the ordered evaluator collection and assigns it, together with
the options, to `HarnessAgentOptions`. It does not wrap, reinterpret, discover,
or execute evaluators itself. Upstream `LoopAgent` remains the sole outer-loop
implementation and remains outside the inner upstream OpenTelemetry agent.

The supported use is **phase-local correction**. A loop may validate and
revise one phase artifact, but it does not retry an entire macro workflow.
Previously accepted phase artifacts and irreversible business effects remain
outside its ownership.

Every iteration is a complete Harness run, including tool invocation,
approvals, history behavior, and telemetry. Tools with external side effects
must therefore be idempotent or caller-deduplicated. Reaching the global
iteration cap returns the latest response; it does not prove that evaluators
accepted it.

Foundry progress represents one outer agent lifecycle and observes every model
and tool call across iterations. Upstream OpenTelemetry emits one inner agent
operation for each invocation of the instrumented Harness agent. Evaluator
work is not represented as a Foundry model call; an AI judge client requires
its own instrumentation.

Loop evaluation does not extend upstream compaction into each iteration. A
two-iteration interaction test with an always-firing strategy consults
upstream compaction once for the outer caller turn, while Foundry hybrid
compaction observes both provider requests at its per-call position.

## Alternatives considered

### Leave loop evaluation available only through raw upstream construction

This avoids a source-breaking configuration change. It was rejected because a
consumer choosing Foundry's explicit complete-bundle entry point would have to
bypass it to use a core upstream Harness capability, losing the effective
defaults report and configuration validation.

### Create Foundry evaluator and loop-option abstractions

This could insulate consumers from experimental MAF types. It was rejected
because Foundry has no second implementation or demonstrated neutral contract.
Wrapping the types now would duplicate semantics such as evaluator priority,
fresh-context session cloning, surfaced feedback, and output aggregation.

### Use sequential pipeline retries or critique/revise stages

Those remain valid for developer-authored workflow stages. They were rejected
as the only phase-correction mechanism because they retry a predeclared stage
from outside the agent rather than letting the complete Harness phase revise
its own plan and artifact.

## Consequences

### Positive

- A Harness phase can self-correct before handing off an artifact.
- The upstream loop remains the only implementation and can evolve with MAF.
- Explicit configuration prevents silent activation and ignored options.
- Effective-default reporting now covers loop evaluation.
- Foundry progress and upstream telemetry ownership remain non-duplicated.

### Negative

- Existing configuration initializers gain three required members.
- Consumers directly reference experimental MAF loop types.
- Worst-case work multiplies the outer iteration cap by the inner function-loop
  cap.
- Custom evaluators and judge clients can introduce independent cost,
  concurrency, trimming, trust, and observability risks.
- Fresh-context isolation depends on session persistence semantics. A
  service-managed session that serializes only a conversation identifier may
  restore a reference to the same remote history instead of an independent
  clone.
- Returned non-streaming usage reflects the final iteration rather than total
  loop consumption; Foundry progress remains the aggregate operational view.

### Neutral

- Existing sequential, graph, handoff, declarative, and iterative workflows are
  unchanged.
- Background agents remain a separately gated capability.
- Macro checkpoint and resume remain separate workflow concerns.
- No stable API promise is made while the package family remains prerelease.

## Confirmation

The decision is confirmed by:

- construction tests for every enabled, disabled, missing, and invalid backing
  combination;
- interaction tests for acceptance, reinvocation, iteration limits, evaluator
  failure, and cancellation;
- progress and telemetry tests across multiple iterations;
- effective-default coverage;
- NativeAOT fixture construction and execution with a built-in evaluator; and
- strict documentation, package, and hosted CI validation.

The NativeAOT claim covers the shipped upstream loop types exercised by the
fixture. Caller-authored evaluators remain responsible for their own trimming
and dynamic-code behavior.

## References

- ADR-0008 defines the optional complete-bundle boundary and one-owner
  composition model retained by this decision.
- ADR-0010 retains the existing execution modes rather than selecting one
  universal default.
- `FoundryHarnessAgentFactory` maps the explicit Foundry configuration to the
  official upstream `HarnessAgentOptions`.
- [Microsoft Agent Framework Harness](https://learn.microsoft.com/en-us/agent-framework/agents/harness)
- [MAF 1.17 loop implementation](https://github.com/microsoft/agent-framework/tree/1da571860a60f0da4f060bd60c8e7ee8d092cd32/dotnet/src/Microsoft.Agents.AI/Harness/Loop)
