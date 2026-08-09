---
title: "ADR-0016: Fixed-macro autonomous-phase composition"
status: "Accepted"
date: "2026-08-09"
authors: ["Nick Cosentino"]
tags: ["architecture", "decision", "agent-framework", "harness", "workflows", "artifacts", "magentic"]
supersedes: ""
superseded_by: ""
---

# ADR-0016: Fixed-macro autonomous-phase composition

## Context and scope

Foundry supports Microsoft Agent Framework workflows and an optional complete
Harness bundle. ADR-0013 exposed upstream loop evaluation for phase-local
correction, ADR-0014 exposed upstream background delegation, and ADR-0015
added an upstream-first checkpoint and resume path.

Those primitives answer how one agent phase can correct itself, delegate
internal work, and be resumed. They do not by themselves define the recommended
composition for a developer who already knows the outer business process but
does not want production correctness coupled to one exact LLM call, tool, or
agent-handoff sequence.

The reference pipeline tested a fixed developer-authored graph with
content-addressed artifact boundaries, all-settled specialist outcomes,
phase-local correction, a checkpoint before synthesis, and deterministic
delivery. A second arm replaced only the synthesis executor with raw upstream
Magentic orchestration. A hosted diagnostic run executed nine scenarios across
plain Harness, delegated Harness, and Magentic arms.

This decision governs the supported composition guidance and whether any
example-local phase, artifact, all-settled, checkpoint, or Magentic glue becomes
a Foundry public API. It does not define dynamic outer workflow generation, a
durable workflow service, a generic planner, a workflow DSL, or a code-mode
runtime.

## Verified facts and evidence limits

- The fixed macro graph can isolate nondeterministic Harness phases behind
  deterministic artifact validation.
- Explicit branch envelopes preserve successful siblings and record optional
  gaps without relying on exact internal agent topology.
- The existing upstream-first checkpoint surface can restore work after an
  accepted artifact boundary without rerunning earlier accepted phases.
- Background delegation has non-propagating cancellation, lost-on-restore
  in-flight state, text-only results, and no built-in task or concurrency
  bound.
- Raw Magentic composition exposes planning, replanning, progress, review, and
  checkpoint state, but invalid next-speaker behavior can finalize an
  unsatisfied task and progress-ledger event payloads are mutable.
- The hosted diagnostic completed 9 of 9 blocks with zero infrastructure
  failures. In one synthetic case, plain Harness passed 7 of 9 scenario
  contracts, delegated Harness passed 1 of 9, and Magentic passed 4 of 8
  applicable scenarios. Magentic used substantially more provider calls and
  tokens than plain Harness.
- The hosted run used one trial per scenario and no calibrated model judge.
  It is insufficiently powered for superiority, non-inferiority, equivalence,
  or semantic-quality claims.

The hosted evidence therefore supports architecture restraint and directional
guidance. It does not establish that plain Harness is universally superior.

## Decision drivers

- Preserve a stable macro contract while allowing autonomous internal phase
  behavior.
- Validate artifacts and outcomes rather than exact LLM trajectories.
- Keep provider and orchestration integrations independently replaceable.
- Reuse official MAF workflow, Harness, checkpoint, and Magentic primitives.
- Avoid public abstractions that have only one example-local implementation.
- Keep optional and experimental mechanisms from becoming implicit defaults.
- State recovery, side-effect, cancellation, observability, and NativeAOT
  limits accurately.
- Leave room for narrow future extraction when repeated use and stronger hosted
  evidence establish a stable contract.

## Decision

### Recommended composition

For a known outer process with open-ended internal phases, Foundry recommends a
**fixed developer-authored macro workflow with autonomous phase executors**.

The macro workflow owns:

- phase dependencies and ordering;
- required versus optional branches;
- concurrency and join policy;
- artifact and outcome schemas;
- deterministic acceptance boundaries;
- checkpoint selection; and
- terminal delivery ownership.

An autonomous phase owns:

- planning and todo decomposition;
- model and tool selection;
- bounded internal retries;
- optional bounded child delegation; and
- correction before returning a candidate artifact.

Production correctness depends on accepted artifacts and explicit outcomes, not
on tool counts, delegation counts, marker prose, or one expected
agent-to-agent handoff sequence.

### Default phase executor

Plain Harness is the default candidate for an autonomous phase because it is
the smallest supported complete-bundle composition and was directionally the
most reliable and least expensive hosted arm in the reference workload.

This is a default engineering recommendation, not a statistical claim.

Harness loop evaluation remains the supported phase-local correction
mechanism. Reaching its iteration cap is not acceptance; the macro artifact
gate remains authoritative.

Background delegation remains explicit and opt-in. Use it only when a phase has
bounded internal work that benefits from concurrency and the host can impose
independent limits, cleanup, trust, and idempotency. The macro contract must not
require one exact child count or ordering.

### Artifact and branch contracts

Foundry does not promote the reference pipeline's phase outcome, artifact
reference, manifest, artifact store, or delivery ledger into public package
types.

Applications define contracts that fit their domain. A correct implementation
should:

- carry candidate content only to its immediate validator;
- persist accepted content under a stable caller-owned identity;
- bind reads to the active run or trust context;
- send references plus explicit gaps across later phases;
- represent completed, partial, failed, and skipped outcomes; and
- make external delivery independently idempotent or transactional.

All-settled fan-out remains application composition. Each branch emits one
bounded outcome envelope and an application-owned join normalizes missing or
duplicate outcomes. Foundry does not add a general all-settled helper from one
reference implementation.

### Checkpoint and recovery

ADR-0015's `StreamingRun`-returning start and resume extensions remain the
supported Foundry checkpoint convenience. No richer Foundry run handle,
checkpoint store, graph-runner overload, or durable orchestration abstraction
is added.

MAF remains the workflow state implementation. The caller owns:

- the `CheckpointManager` and backing store;
- checkpoint selection and persistence;
- artifact and delivery persistence;
- stable executor identifiers and compatible topology;
- cancellation and disposal; and
- idempotency for replayed side effects.

Recovery is at-least-once. Select the checkpoint after a deterministic accepted
artifact boundary and before downstream work that may replay.

### Magentic

Magentic remains optional and phase-local. Foundry documents the raw upstream
`MagenticWorkflowBuilder`; it does not add a Magentic convenience wrapper,
manager protocol, event abstraction, or default orchestration path.

Use Magentic only when one phase genuinely benefits from manager-owned
sequential planning, participant selection, stall detection, and replanning.
Keep naturally parallel specialist work in the outer macro graph. A Magentic
phase must still return the same candidate artifact contract and pass the same
deterministic boundary as a plain Harness phase.

Magentic checkpoints are an inner recovery layer. An outer checkpoint before
the phase replays the whole phase. Mid-phase Magentic recovery requires a
separately owned inner checkpoint lifecycle.

### NativeAOT

The supported NativeAOT claims remain narrow and executable:

- the complete Harness package's declared AOT fixture constructs and executes
  loop and background-agent capabilities;
- the checkpoint example publishes and executes with upstream built-in
  message and state types; and
- custom checkpointed messages and state require source-generated
  `System.Text.Json` metadata.

The example-local fixed-macro pipeline, custom artifact contracts, hosted
evaluation infrastructure, and raw Magentic composition are not promoted as a
NativeAOT-supported public profile. Their AOT compatibility is unverified.

### Public API and diagnostics

No new analyzer or diagnostic is added. The important misuse cases depend on
application semantics—artifact acceptance, branch requiredness, idempotent
delivery, or appropriate Magentic use—and cannot be detected reliably from
syntax or static topology alone.

Future extraction requires repeated use outside this reference, a
provider-neutral contract, deterministic tests, and hosted evidence that
measures the abstraction rather than the evaluation harness.

## Alternatives considered

### Extract a general autonomous-phase framework

This could standardize phase outcomes, artifacts, manifests, gates, retries,
checkpoints, and delivery. It was rejected because the contracts have only one
example-local implementation and are strongly shaped by domain semantics.
Premature extraction would recreate the brittle orchestration layer this work
is intended to avoid.

### Extract narrow phase outcome, all-settled, or artifact helpers

These helpers could reduce repeated code in the reference. They were rejected
because no repetition outside the reference and evaluation projects has been
demonstrated. The hosted run also exposed ongoing scenario and model
variability, so the stable helper boundary is not yet proven.

### Make background delegation the default Harness phase

This could increase internal parallelism. It was rejected because cancellation
and restore semantics are weaker, the provider supplies no independent bounds,
and the hosted reference did not demonstrate reliable use of both configured
children.

### Make Magentic the default open-ended phase

This could provide dynamic planning and replanning. It was rejected because
Magentic is sequential per round, adds manager calls and state, has important
fail-open and mutable-event behaviors, and consumed substantially more tokens
in the diagnostic workload without sufficient reliability evidence.

### Build a Foundry Magentic wrapper

A wrapper could normalize events, completion, and checkpoint behavior. It was
rejected because raw MAF composition is concise, upstream remains
experimental, and a wrapper would mostly mirror one implementation while
hiding limits consumers need to see.

## Consequences

### Positive

- The supported architecture preserves autonomous agent behavior without
  making nondeterministic trajectories production gates.
- Existing upstream implementations remain visible and replaceable.
- Public package boundaries stay narrow and dependency-direction safe.
- Artifact validation, checkpoint recovery, and delivery ownership are
  explicit.
- Future APIs require repeated evidence rather than one successful example.

### Negative

- Applications must define their own outcome, artifact, manifest, join, and
  delivery contracts.
- There is no Foundry all-settled macro helper or unified checkpointed result
  model.
- Background delegation and Magentic require application-specific safeguards
  and observability.
- Mid-phase Magentic recovery requires a second checkpoint lifecycle.
- The default recommendation is supported by directional rather than powered
  comparative evidence.

### Neutral

- ADR-0013, ADR-0014, and ADR-0015 remain accepted and are not superseded.
- Existing sequential, graph, handoff, declarative, and iterative workflow APIs
  are unchanged.
- Dynamic outer topology, durable orchestration, and sandbox design remain out
  of scope.
- The example and hosted evaluation remain public evidence, not stable package
  contracts.

## Confirmation

This decision is confirmed by:

- deterministic reference tests for success, partial fan-out, required
  failure, correction, cancellation, restore, and delivery replay;
- direct Harness loop and background-agent interaction tests;
- upstream-first checkpoint lifecycle tests and the executed checkpoint
  example;
- phase-local Magentic tests for planning, replanning, review, invalid
  speakers, inner restore, and artifact validation;
- hosted diagnostic run
  [31296045070](https://github.com/ncosentino/foundry/actions/runs/31296045070),
  which completed all nine blocks and published immutable evidence;
- strict documentation and guidance validation;
- package validation; and
- hosted build, test, and declared NativeAOT checks.

The hosted comparison remains insufficiently powered. Confirmation means the
decision is appropriately conservative, not that one arm has proven universal
superiority.

## References

- ADR-0013 demonstrates that upstream loop evaluation composes directly as a
  phase-local correction mechanism and records its iteration and usage limits.
- ADR-0014 demonstrates that upstream background delegation composes directly
  while retaining important cancellation, restore, cleanup, and trust
  constraints.
- ADR-0015 establishes the narrow upstream-first checkpoint convenience and
  caller-owned persistence model retained here.
- [Fixed-Macro Autonomous Phases](../autonomous-phase-pipeline.md) demonstrates
  the developer-authored macro graph and artifact-boundary pattern.
- [Phase-Local Magentic Comparison](../magentic-phase-comparison.md) documents
  raw upstream composition and measured MAF 1.17 behavior.
- [Autonomous Phase Evaluation 001 Results](../evaluations/autonomous-phase-001-results.md)
  interprets the hosted run, uncertainty, and no-promotion conclusion.
