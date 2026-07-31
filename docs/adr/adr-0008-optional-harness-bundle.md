---
title: "ADR-0008: Optional Microsoft Agent Framework Harness bundle"
status: "Accepted"
date: "2026-07-26"
authors: ["Nick Cosentino"]
tags: ["architecture", "decision", "agent-framework", "harness", "bundle", "telemetry", "progress"]
supersedes: ""
superseded_by: ""
---

## Context and scope

ADR-0005 established two independent Microsoft Agent Framework integration
lanes: a neutral selected-provider lane in
`NexusLabs.Foundry.MicrosoftAgentFramework`, and a separately gated,
complete-bundle lane that may depend on `Microsoft.Agents.AI.Harness`.
The complete bundle is useful because it supplies an opinionated, integrated
agent pipeline, but adopting it inside the neutral core would impose its
dependencies and defaults on consumers who did not select it.

The upstream bundle also owns behavior that cannot be safely composed twice.
MAF 1.15 constructs its own function-invocation loop, message injection,
history persistence, context providers, agent-level OpenTelemetry
instrumentation, and chat-client OpenTelemetry instrumentation. Foundry needs
to expose those defaults honestly, preserve one owner for each loop and
telemetry layer, and add Foundry progress without creating another loop,
span writer, metric writer, or diagnostics writer.

Source-generated Foundry functions are already available through the public
`IAIFunctionProvider` registered by `AgentFrameworkGeneratedBootstrap`.
The optional bundle must accept those generated `AIFunction` instances without
creating a second discovery mechanism or using reflection.

This decision governs the optional package boundary, public construction
candidate, dependency direction, generated-tool ingress, effective-default
reporting, loop and telemetry ownership, and Foundry progress composition. It
does not approve background-agent delegation, loop evaluators, direct
NativeAOT support, hosted provider quality, analyzer guidance, or stable
release status.

## Decision drivers

- Consumers that do not reference the optional package must not acquire the
  Harness bundle, its defaults, or its transitive dependencies.
- The neutral core must never depend on the complete Harness bundle.
- Bundle users must be able to inspect what they requested, what will
  actually run, and whether an upstream default or caller-supplied object
  backs each configurable dimension.
- The composed agent must have exactly one function-invocation loop and one
  upstream OpenTelemetry owner.
- Foundry progress must observe every outer run, model round, and tool
  invocation without emitting duplicate spans, metrics, or diagnostics.
- Generated tools must remain AOT-oriented and reflection-free.
- Configuration inputs that upstream would ignore or that conflict with
  another selected backing must fail before construction.
- The public candidate must remain explicit enough to evolve before a stable
  release without accumulating compatibility shims for unshipped alpha shapes.

## Decision

Foundry will provide the complete MAF Harness bundle only through the optional
`NexusLabs.Foundry.MicrosoftAgentFramework.Harness` package. The neutral core
will not reference this package. The optional package may reference:

- `Microsoft.Agents.AI.Harness`, which owns the complete upstream pipeline; and
- `NexusLabs.Foundry.MicrosoftAgentFramework`, solely for its public progress
  contracts.

The optional package will never depend on Needlr. The core dependency is
one-way: referencing the optional package acquires the neutral core, but
referencing the neutral core does not acquire the bundle.

`FoundryHarnessAgentFactory` is the sole public construction entry point for
this lane. It always calls the official upstream `AsHarnessAgent` extension.
`FoundryHarnessAgentConfiguration` requires an explicit value for every
exposed dimension, including explicit `null` for upstream-default or disabled
backings. It maps those values directly to `HarnessAgentOptions` and
`ChatOptions`, rejects incoherent combinations, reserves enabled built-in
tool names so caller tools cannot silently shadow them, and rejects known
pre-existing loop, message-injection, or OpenTelemetry wrappers that are
discoverable through the cooperative `IChatClient.GetService` convention.
Opaque wrappers remain a caller responsibility because `IChatClient` cannot
require service-discovery forwarding.

The bundle's requested-versus-effective report is a separate public,
immutable model. It records:

- the requested state;
- the effective state;
- a limitation when the upstream bundle is unavoidable or the candidate does
  not expose a dimension; and
- whether a configurable backing is caller-supplied or an upstream default,
  together with the meaning of that default.

Generated Foundry functions enter this lane only as caller-resolved
`AIFunction` instances in `FoundryHarnessAgentConfiguration.Tools`. Callers
obtain them from the source-generated `IAIFunctionProvider` registered by
`AgentFrameworkGeneratedBootstrap`. The optional production package performs
no assembly scan, reflection fallback, generated-type activation, or parallel
duplicate discovery.

The upstream bundle is the sole function-loop and OpenTelemetry owner.
Foundry does not wrap the provider with diagnostics or telemetry middleware
and does not construct another `FunctionInvokingChatClient`. When the caller
supplies an explicit `IProgressReporterAccessor`, Foundry adds progress at
three observation seams:

1. an innermost progress-only `DelegatingChatClient` around the raw provider
   reports every real model round;
2. a chained delegate on the bundle's existing
   `FunctionInvokingChatClient.FunctionInvoker` reports tool execution without
   replacing the loop; and
3. an outer `DelegatingAIAgent` reports agent lifecycle.

One `AsyncLocal` run state correlates those seams for concurrent and nested
runs. It carries the child reporter, model-call sequence, token totals, and
tool-call count. Normal completion, model/tool failure, streaming failure,
stream abandonment, enumerator acquisition failure, and enumerator disposal
failure each produce exactly one terminal progress event at the affected
agent/model level. Cancellation is represented by the existing failure event
types because the progress contract has no cancellation-specific event.

The progress wrappers emit no `Activity`, `Meter`, Foundry diagnostics record,
or token histogram. Upstream therefore remains the only source of agent,
model, and tool OpenTelemetry records. Replacing the constructed
`FunctionInvokingChatClient.FunctionInvoker` after construction also replaces
Foundry's tool-progress hook and is outside the factory's control.

This is an accepted architecture for the current public API candidate, not a stable
release promise. Release status and compatibility commitments remain subject
to the later API and release review.

## Alternatives considered

### Put the complete bundle in the neutral core

This would give every core consumer one construction path and avoid a separate
package. It was rejected because all core consumers would acquire the bundle,
its experimental surface, and its opinionated defaults. It would also reverse
the dependency direction established by ADR-0005.

### Keep the optional package independent of the core

This preserves the smallest dependency closure. It was rejected because the
bundle lane would either expose no Foundry progress or would need to duplicate
the public progress contracts. The accepted core reference has a narrow code
purpose and does not introduce a reverse dependency or Needlr.

### Let Foundry own another loop or telemetry layer

Foundry could wrap the provider with its diagnostics function loop and
telemetry middleware. This was rejected because the upstream bundle already
owns those layers. Two owners would duplicate model/tool execution or
OpenTelemetry records and would make ordering ambiguous.

### Auto-discover generated tools inside the optional package

The package could reference internal resolver logic, scan assemblies, or add a
second generated registry. This was rejected because the public generated
provider already exists, caller-resolved functions flow through ordinary
`ChatOptions.Tools`, and a second discovery path would create divergence and
weaken AOT evidence.

### Resolve progress implicitly from the factory service provider

An enable flag could instruct the factory to look up the progress accessor in
`IServiceProvider`. This was rejected because it creates two configuration
sources and makes the effective progress seam depend on hidden container state.
An explicit nullable accessor is inspectable and matches the existing Foundry
composition pattern.

### Create a new progress-abstractions package now

This would reduce the optional bundle's transitive package weight. It was
rejected for this decision because no measured deployment or trimming blocker
requires another package boundary. A later package split remains possible if
NativeAOT or release evidence demonstrates concrete value.

## Consequences

### Positive

- Neutral core consumers remain free of the complete Harness bundle.
- Bundle consumers receive one official upstream construction path with
  explicit configuration and inspectable defaults.
- Generated functions enter without reflection or a duplicate discovery
  system.
- Known built-in tool collisions fail before construction rather than silently
  shadowing upstream implementations.
- One function loop and one OpenTelemetry owner are proven by real pipeline
  execution.
- Foundry progress covers agent, model, and tool activity without duplicate
  spans, metrics, or diagnostics.
- Streaming and concurrent runs have deterministic correlation and terminal
  behavior.
- Needlr remains outside both the optional package and the neutral core
  boundary.

### Negative

- The optional package now carries the neutral core and its transitive
  dependencies solely to reuse progress contracts.
- Built-in tool-name reservations and default descriptions are version-specific
  evidence that must be rechecked on every Harness upgrade.
- Opaque chat-client wrappers can hide pre-existing middleware from
  service-discovery validation.
- Caller-provided context providers can inject dynamic tool names that the
  factory cannot enumerate before a run.
- A caller can replace the mutable upstream `FunctionInvoker` after
  construction and thereby remove Foundry tool progress.
- Cancellation appears as failure progress because no cancellation-specific
  event exists.
- The public candidate is intentionally stricter than raw upstream options and
  can reject ignored or ambiguous combinations that upstream would accept.

### Neutral

- Background agents and loop evaluators remain reported limitations rather
  than silently omitted features.
- File access and compaction remain explicit opt-ins.
- The selected-provider lane and its trust/workspace/hybrid-context decisions
  are unchanged.
- Stable release guidance, direct NativeAOT proof, hosted comparisons, and
  analyzer guidance remain later decisions.

## Confirmation

The decision is confirmed by:

- dependency-closure tests proving core-to-bundle isolation, the optional
  package's exact core-plus-Harness direct dependencies, and the absence of
  Needlr;
- real source-generator tests that resolve, execute, and collision-check
  generated functions through the bundle;
- real `HarnessAgent` tests that inspect upstream services and effective
  defaults;
- deterministic telemetry tests proving one agent operation, two model
  operations, and one tool operation for a one-tool round;
- progress tests covering exact event order, token and tool counts, disabled
  and no-scope behavior, model and tool failure, normal streaming, abandoned
  streaming, acquisition/disposal failure, and overlapping concurrent runs;
- package validation that asserts both required direct dependencies;
- solution build and core regression tests; and
- isolated MAF-specialist, code, and API-contract reviews with every blocking
  finding resolved.

An upgrade from MAF 1.15 or MEAI 10.6 must re-prove middleware order, service
discovery, option defaults, built-in tool names, activity classification, and
stream lifecycle behavior. Repository evidence cannot confirm hosted provider
quality, direct NativeAOT execution of this package, or stable compatibility;
those remain separate gates.

## References

- ADR-0005 defines the two-lane MAF Harness architecture and the one-owner
  loop/telemetry rule that this optional package preserves.
- ADR-0006 and ADR-0007 govern workspace authority and selected-provider hybrid
  context; the optional complete bundle does not replace those decisions.
- `FoundryHarnessAgentFactory` and `FoundryHarnessAgentConfiguration` define the
  public construction candidate and explicit option mapping.
- `FoundryHarnessTelemetryComposition` defines the progress-only composition
  that preserves upstream loop and telemetry ownership.
- the Gate G6 record (git history; see issue #22) records the dependency,
  defaults, tests, review dispositions, and delivery evidence for this decision.
