---
title: "ADR-0005: First-class Microsoft Agent Framework Harness integration"
status: "Accepted"
date: "2026-07-24"
authors: ["Nick Cosentino"]
tags: ["architecture", "decision", "agent-framework", "harness", "composition", "identity", "telemetry"]
supersedes: ""
superseded_by: ""
---

## Context and scope

Foundry added workspace state, iterative execution, diagnostics, generated tools,
and evaluation support while Microsoft Agent Framework did not yet offer a
complete agent Harness. MAF 1.15 now provides stable core providers and an
opinionated Harness bundle. Adopting those capabilities can remove duplicated
orchestration work, but adopting the entire bundle as Foundry's only path would
also import defaults that have not passed Foundry's trust, workspace, context,
telemetry, and NativeAOT requirements.

MAF and Microsoft.Extensions.AI expose composition through both `AIAgent`
decorators and `IChatClient` middleware. Some middleware owns internal loops.
In particular, function invocation may make multiple model calls for tool
rounds, and message injection may make additional model calls when messages
arrive during a request. A trust check placed only around the outer agent call
does not therefore protect every provider call or tool invocation.

This decision governs how Foundry integrates MAF Harness capabilities, records
capability evidence, composes the initial selected-provider lane, assigns tool
loop and telemetry ownership, and binds execution identity. It does not approve
a public Foundry Harness API, implement workspace file providers, select a
conversation compaction policy, or make the complete Harness bundle mandatory.

## Decision drivers

- Existing Foundry consumers must not acquire a second tool loop, telemetry
  recorder, or changed default execution path.
- Trust-sensitive model, tool, session, and workspace operations must fail
  closed when host-issued execution identity is absent or changes.
- Neutral Foundry packages must remain provider-neutral and independent of
  Needlr.
- Stable MAF capabilities should be adoptable incrementally without committing
  Foundry to every Harness default.
- Capability availability, upstream stability, diagnostics, NativeAOT status,
  trust boundary, and delivery evidence must remain inspectable.
- MAF and MEAI upgrades must not silently change middleware ordering or
  ownership semantics.
- The architecture must support non-Azure hosts and arbitrary `IChatClient`
  providers.

## Decision

Foundry will provide first-class MAF Harness integration through two distinct,
opt-in consumption lanes.

The selected-provider lane belongs in
`NexusLabs.Foundry.MicrosoftAgentFramework`. It composes individually selected
stable MAF core capabilities and must not reference the complete
`Microsoft.Agents.AI.Harness` bundle. The complete-bundle lane will remain an
optional, separately gated package that may depend on that bundle and expose
its opinionated construction path.

Capability selection is governed by an internal, schema-versioned evidence
profile. Each capability records its source and version, stability,
default-bundle behavior, effective state, required provider capability, trust
boundary, NativeAOT status, diagnostics status, and the evidence level required
to enable it. A profile is executable only when every selected capability has
passed its required evidence. The profile also records the MAF version and a
middleware-order version so an upstream upgrade cannot inherit old ordering
claims implicitly.

The initial selected-provider composition supports two complete ownership sets:

- MAF function invocation with MAF/MEAI OpenTelemetry; or
- Foundry diagnostics function invocation with Foundry telemetry.

Tool-loop and telemetry ownership must align. Mixed ownership, an existing
function loop, existing message injection, pre-existing telemetry, a
non-executable profile, a complete-bundle profile, or a capability without
current evidence is rejected before agent construction.

For MAF 1.15 and MEAI 10.6, the effective chat-client order is:

```text
FunctionInvokingChatClient
  -> MessageInjectingChatClient
  -> trusted execution-binding client
  -> selected telemetry client
  -> provider IChatClient
```

The execution-binding client is inside the message-injection loop so every
provider call is validated before and after execution, including additional
calls initiated by injected messages. The function invoker validates again
immediately before each tool body executes. Generated tools enter through
Foundry's existing generated-function provider and fail closed when generated
metadata cannot be resolved.

Agent construction uses `UseProvidedChatClientAsIs` so MAF does not add a second
default loop or telemetry layer. The returned internal agent rejects all
per-run `AgentRunOptions`; safe run-time overrides require later
capability-profile evidence rather than forwarding a caller-owned mutable
options object. It does not expose a callable `IChatClient`, the raw
`ChatClientAgent`, or mutable function-loop middleware through `GetService`.
Message injection remains available through a narrow internal surface that
validates the same trusted binding without exposing the underlying chat client.

The trusted binding is captured per execution from host-issued user,
orchestration, session, and authorized workspace state. It is immutable and is
compared with the active execution context on every guarded operation. Model
input, restored session state, and path strings cannot select or replace the
authorized workspace. A reusable ambient or singleton workspace bridge is not
approved.

All of these contracts remain internal candidates. Public promotion requires
later conformance evidence and a separate API disposition. Ordinary
`AgentFactory` and Foundry's existing iterative loop remain unchanged unless a
caller explicitly chooses a Harness profile.

## Alternatives considered

### Adopt only the complete Harness bundle

This would minimize Foundry composition code and follow Microsoft's most
opinionated path. It was rejected as the only integration because complete
bundle defaults include capabilities and trust surfaces that have not all
passed Foundry's workspace, approval, compaction, diagnostics, and NativeAOT
requirements. It would also make incremental stable capability adoption harder.

### Keep only Foundry's existing iterative loop

This preserves current behavior and avoids new MAF composition seams. It was
rejected because Foundry would continue owning capabilities now provided by
MAF, consumers could not use standard Harness providers, and the framework
would accumulate a parallel ecosystem rather than integrate replaceable
upstream components.

### Publish a general Harness API with the composition foundation

This would let consumers adopt the work immediately. It was rejected because
the current names and shapes have evidence only for generated tools, message
injection, execution binding, loop ownership, and telemetry. History,
approvals, skills, workspace providers, compaction, and the complete bundle
still need independent conformance evidence. Publishing now would turn an
incremental candidate into a premature compatibility contract.

### Resolve workspace and identity from ambient singleton services

This would allow one reusable agent graph across executions. It was rejected
because late, background, restored, or incorrectly flowed calls could select
the wrong workspace. Per-execution binding is less convenient but preserves the
host authorization boundary and fails closed after scope completion.

## Consequences

### Positive

- Foundry can adopt stable MAF capabilities incrementally without making the
  complete Harness bundle mandatory.
- Every model call made by function and message-injection loops crosses the
  trusted execution boundary.
- Every tool invocation is revalidated at the point of execution.
- Loop and telemetry duplication become construction failures instead of
  runtime ambiguity.
- Capability and ordering claims are versioned and can be invalidated on an
  upstream upgrade.
- Non-Azure, provider-neutral `IChatClient` hosts remain supported.
- Existing non-Harness construction remains behaviorally unchanged.

### Negative

- Selected-provider agents require per-execution binding and cannot freely
  replace their chat-client pipeline or apply per-run agent options.
- Raw chat middleware is intentionally hidden, so capabilities such as message
  injection need narrow Foundry-owned access surfaces.
- Foundry must maintain composition and conformance tests across MAF and MEAI
  upgrades.
- The selected-provider composition glue has not yet been directly executed by
  a NativeAOT application; the current AOT evidence covers the underlying
  package and generated-tool paths.
- The initial ownership rule rejects potentially valid mixed telemetry and tool
  loop combinations until separate evidence justifies them.
- No consumer-facing API is available from this decision alone.

### Neutral

- Foundry's existing iterative loop remains available and is not reimplemented
  as a Harness compatibility shim.
- `IWorkspace` remains Foundry's authoritative bulk-artifact abstraction, but
  no `AgentFileStore` bridge is approved by this decision.
- Conversation continuity, approval restoration, workspace providers, hybrid
  context, and the complete Harness bundle remain separate decisions and gates.

## Confirmation

The decision is confirmed by:

- capability-profile tests that reject unsupported lanes, experimental
  capabilities without acceptance, unavailable provider capabilities, and
  capabilities whose evidence is not yet available;
- generated-wrapper tests that resolve generated functions without reflection
  fallbacks and fail closed on missing generated metadata;
- deterministic non-Azure composition tests that execute a generated tool with
  exactly one function loop and one telemetry owner;
- execution-binding tests covering missing and changed identity, workspace,
  session, scope completion, model calls, injected-message service calls,
  streaming calls, and immediate pre-tool validation;
- opt-out tests that keep ordinary `AgentFactory` behavior unchanged;
- hosted build, test, and documentation checks; and
- existing standard and Harness NativeAOT checks for the underlying package and
  generated-tool paths.

An MAF or MEAI upgrade must update the recorded version, re-prove middleware
order and service discovery, and rerun these contracts before the profile can
claim compatibility. A minimum NativeAOT application must also invoke the
selected-provider composition path directly before a supported profile is
promoted. Repository evidence cannot prove hosted model availability, provider
quality, or durable cross-process session behavior; those require
provider-specific and hosted validation.

## References

- ADR-0004 assigns Microsoft Agent Framework and neutral agent infrastructure to
  Foundry while keeping Needlr as an optional integration boundary.
- `specs/001-maf-harness-first-class/evidence/gate-g2.md` records the cumulative
  implementation evidence and internal API disposition for this decision.
- `specs/001-maf-harness-first-class/evidence/workspace-identity-feasibility.md`
  demonstrates why workspace authority must be bound per execution and why a
  generic `AgentFileStore` bridge remains partial.
- `HarnessProviderComposition`, `HarnessCompositionGuard`, and
  `HarnessGuardedAgent` carry the composition, ownership, and service-surface
  enforcement contracts confirmed by this record.
- MAF 1.15 `MessageInjectingChatClient` owns an internal provider-call loop,
  which is why the trusted binding is placed inside message injection.
- MEAI 10.6 `FunctionInvokingChatClient` owns tool rounds and supplies
  `AIFunctionArguments.Services`, which is why one function loop and explicit
  service-provider propagation are required.
