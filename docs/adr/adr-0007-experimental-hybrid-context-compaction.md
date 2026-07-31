---
title: "ADR-0007: Experimental hybrid context compaction"
status: "Accepted"
date: "2026-07-25"
authors: ["Nick Cosentino"]
tags: ["architecture", "decision", "agent-framework", "harness", "context", "compaction", "observability"]
supersedes: ""
superseded_by: ""
---

## Context and scope

ADR-0006 established that `IWorkspace` is Foundry's sole authoritative store
for bulk artifact bytes and that a conversation carries only a bounded
`artifact://sha256/...` reference in place of an offloaded body, recovered
only through an explicit, verified rehydration act. That decision deliberately
left conversation compaction policy — whether, when, and how a long-lived
hybrid agent's own history is kept within a provider's context limits — out of
scope. This decision picks that question back up and settles it: a single,
explicit, experimental opt-in hybrid compaction profile that assembles bounded
context for every provider call, together with privacy-safe, structured
observability over every decision it makes. It refines ADR-0006's
workspace-authority and rehydration framing into a concrete compaction
contract that governs when a rehydrated body is evicted and how a conversation
is kept within a hard limit; it does not supersede ADR-0006, and every
rule ADR-0006 established about workspace authority, reference shape, and
explicit rehydration is unchanged and continues to govern how a body ever
re-enters context.

This decision governs four things: how one experimental compaction profile is
selected and never defaulted on, how a bounded context is deterministically
assembled for a single provider call, how a rehydrated body's lifetime is kept
inside the conversation's own history boundary, and how every assembly
decision is made observable without ever exposing conversation content. It
does not approve a public Harness runtime configuration or composition API —
every mechanism introduced remains `internal` — and it does not aggregate this
leaf's observability into completed-run diagnostics, a sink, or a metrics
surface; that aggregation is explicitly deferred, as recorded in
the Gate G5 record (git history; see issue #21).

## Decision drivers

- A long-lived hybrid agent's conversation must eventually exceed any fixed
  provider context limit; Foundry needs one deterministic answer for what
  happens next, not an ad hoc one per caller.
- Exactly one coherent capability profile may govern whether compaction is
  active for a given composition — a second, independently-resolved profile
  or a second installed compaction component would reintroduce the
  divergence risk ADR-0005/ADR-0006 already rejected for workspace authority.
- An upstream-supplied `IChatReducer` cannot be trusted to enforce Foundry's
  own structural-preservation and hard-limit guarantees; its output must be
  treated as a proposal, never as ground truth.
- A rehydrated artifact body must never become a second, durable copy living
  indefinitely inside conversation history — its presence there must be
  bounded to the run that requested it, with the durable reference always
  available to fetch it again.
- The same rehydrated body must never cross the wire to the provider twice
  within one outer run purely because two nested provider calls both observed
  it in their respective snapshots.
- A policy that cannot converge (content that structurally cannot fit) or
  that is destabilized by concurrent mutation of the underlying history must
  fail with an explicit, distinguishable outcome — never silently forward an
  over-budget or stale context.
- Every compaction decision must be inspectable by tests and operators
  without exposing raw message text, artifact bodies, workspace paths, owner
  identity, tool arguments/results, exception text, or classifier output.
- Observability must not introduce an ambient singleton, an optional
  parameter, or a public runtime configuration surface, matching the pattern
  ADR-0006 already established for offload/rehydration observability.

## Decision

Hybrid context compaction is exactly one explicit, experimental opt-in
profile, never a default. A caller must both request
`HarnessCapability.Compaction` on the capability profile it resolves and
separately supply a `HarnessHybridProfile` for the exact same composition
call; either one without the other fails closed before any agent is
constructed, and the capability's own delivery-phase gating keeps it
`Deferred` until its resolver evidence is actually present at its declared
delivery phase. Enabling the capability never turns compaction on by itself,
and supplying a profile never turns it on without the capability also being
enabled — the two are read from, and validated against, the same single
resolved capability profile a composition call already carries, never a
second independently-resolved one.

Exactly one hybrid compaction component may ever be installed for a given
composition. It is wrapped at the proven per-provider-call seam — the
innermost position around the real provider `IChatClient`, beneath every
other Foundry-owned middleware — so it observes the exact message set
dispatched for every intermediate tool round and every injected extra call,
never merely the outer agent call and never only the first or last request.
This is a deliberate choice over MAF's own turn-scoped compaction seam, which
evaluates once per agent turn against a history index that has not yet
observed the current tool round's result, and is therefore structurally
insufficient for a caller that needs every intermediate provider request
bounded, not only the final one per turn.

Every call assembles bounded context deterministically from a fresh snapshot
of that call's own message set: recoverable rehydrated bodies are evicted
first, ahead of any reducer proposal; the configured upstream `IChatReducer`
is then invoked, bounded to a configured maximum attempt count, but its output
is never trusted directly — it is advisory only, and Foundry's own verifier
independently confirms every proposal is both strictly size-reducing and
structurally valid (required content preserved, message sequencing intact)
before accepting it. Every attempt is evaluated against one deterministic
trigger margin below a hard limit and against the hard limit itself, both
expressed in whatever unit the configured size estimator declares — never
assumed to be a provider token count unless an estimator says so explicitly.
When no accepted reducer proposal fits within the configured attempt bound,
a deterministic preservation-only fallback runs instead: required content,
plus any retained optional context that still fits, is kept and nothing
reducer-dependent is trusted to produce the final answer. If required content
alone still exceeds the hard limit even after that fallback, or if injected
concurrent mutation keeps invalidating in-flight proposals until the attempt
budget is exhausted before a version change can be consumed as a restart,
assembly terminates explicitly — `Irreducible` or `ConcurrentMutationLimit`
respectively — and this node throws rather than ever forwarding an
over-budget or stale context to the real provider.

A rehydrated body's presence in context is transient and scoped to the
current outer run's history boundary; the durable `artifact://sha256/...`
reference persists in the conversation regardless. A non-retransmission
coordinator reserves, and later commits or releases, one lease per digest per
provider call, so the exact same body is delivered to the real provider at
most once per outer run — a second nested call within the same run that would
otherwise re-observe an already-delivered body has it filtered back out of
its own snapshot before assembly ever considers it. This is deliberately not
automatic, repeated rehydration on every observation: a body earns its way
back into context only through the same explicit, budget-checked rehydration
act ADR-0006 already requires, and once delivered within a run it is not
delivered again within that same run.

Every assembly decision is observable through one shared, privacy-safe
contract: a categorical outcome (one of the three success outcomes or one of
the two termination outcomes, the termination outcomes doubling as the
explicit termination category), an explicit measurement unit, original/final
sizes, the trigger threshold and hard limit in force, an attempt count, the
ordered stages actually executed, per-category final size/entry-count
contributions computed with the same estimator that governed the policy
decision, and a final sequence-validity flag. Every categorical field is
assigned through a deterministic mapping from the internal structured
assembly result — never by parsing an exception or evidence string. For every
assembly attempt that reaches the assembler (i.e., after message adaptation,
snapshot integration, and assembler construction have all succeeded), exactly
one started event and exactly one terminal event (a completed event on
success, or a terminated event on termination, never both) are reported; a
context-composed/ready-for-dispatch event, carrying the identical diagnostics
instance as the completed event, is reported only on success and only after
the same post-assembly trust revalidation that already guards every dispatch —
never for a terminated attempt. A single opaque `AssemblyId` (a `Guid`) is
generated exactly once per attempt, at that same success gate immediately
before the started event is emitted, and is threaded identically onto the
started event, whichever terminal event follows, and the composed event on
success — so every event this attempt ever reports carries the same
`AssemblyId`, letting two concurrently-running assemblies for the same
agent/workflow remain pairable by that ID despite their `SequenceNumber`s
interleaving. Exceptional failures before assembly begins
(classifier exception, snapshot-construction failure) propagate directly
without emitting any event; exceptional failures during assembly (cancellation,
binding invalidation, reducer exception) propagate without masquerading as
Completed or Terminated. Reporting threads through the existing
`HarnessProviderCompositionRequest.ProgressAccessor` seam as a required
nullable constructor parameter with no optional parameter and no ambient
singleton; when the accessor is absent, nothing is reported and ordinary
assembly behavior is fully preserved. Offload and rehydration decisions
additionally carry a shared UTF-8 byte attribution — input bytes observed and
artifact-derived output bytes (the reference identity's byte length when an
artifact reference was written or reused, the resolved body's byte length when
rehydration succeeded), or `null` when no artifact-derived output was committed
— riding along on the exact same `HarnessArtifactDiagnostics` snapshot
ADR-0006 already attaches to both the internal outcome/result and its
corresponding progress event, so no second, independently-computed attribution
value can ever diverge from the one a test or operator already inspects. Null
output bytes for failure or recovery-required outcomes reflect only that no
artifact-derived output was produced; the caller may still emit a separate
bounded error string for those outcomes, which is not counted here.

No public Harness runtime configuration or composition API is introduced by
this decision. Every compaction mechanism, request, result, policy, and
status type remains `internal`; the only public surface is the observability
contract itself — diagnostics/attribution records, categorical enums, and
progress events — exactly as ADR-0006 already established for
offload/rehydration.

## Alternatives considered

### Unbounded history

A hybrid agent could simply forward its entire accumulated history on every
call and rely on the provider to reject or truncate an oversized request.
This was rejected because it pushes an unrecoverable, provider-specific
failure mode onto every long-lived agent and gives Foundry no deterministic,
inspectable point at which a bounding decision is made; a caller would learn
about an over-budget conversation only from a provider error, with no
structured evidence explaining why.

### Workspace-only rebuild or discard conversation history

Instead of compacting in place, a caller could discard accumulated
conversation history entirely and rebuild working context solely from
workspace-backed artifacts and references on each call. This was rejected
because it destroys ordinary conversational continuity (a plain back-and-forth
exchange is not an artifact and has nothing to rebuild from) and conflates two
different concerns ADR-0006 already kept separate: `IWorkspace` is
authoritative for bulk artifact bytes, not for the shape or continuity of a
model-facing conversation.

### Trust the upstream reducer without verification

The selected upstream `IChatReducer`'s output could be accepted directly as
the final bounded history, saving a verification pass. This was rejected
because an upstream reducer has no knowledge of Foundry's own structural
requirements — required system/authoritative/approval content, atomic
tool-call/tool-result exchange integrity, or message sequencing validity — and
a reducer proposal that violates any of them would be indistinguishable from
a correct one without independent verification. Treating the reducer as
advisory-only and verifying every proposal before it can be accepted is the
only way to keep the hard-limit and structural-preservation guarantees this
decision requires regardless of which reducer a caller configures.

### Outer-agent-only reduction

Compaction could be evaluated once per outer agent turn (mirroring MAF's own
built-in compaction seam) rather than once per real provider call. This was
rejected because an outer-turn-scoped evaluation is checked against a history
index that has not yet observed the current tool round's result, so an
intermediate tool-round request — the exact request most likely to be large,
immediately after a sizeable tool result — could still exceed the hard limit
before the next turn-level evaluation ever runs. Only a per-provider-call seam
observes every such request.

### Automatic repeated rehydration

A reference could be automatically re-resolved to its body every time a
snapshot observes it, rather than being filtered out once already delivered
within the current run. This was rejected for the same reason ADR-0006
rejected implicit/automatic rehydration generally: it removes the caller's
ability to bound when, and how many times, a potentially large body re-enters
context, and would silently retransmit an already-delivered body to the real
provider on every subsequent nested call within the same run.

## Consequences

### Positive

- A long-lived hybrid agent's context is kept within a deterministic hard
  limit at every intermediate provider call, not only at the outer turn
  boundary, with no ad hoc per-caller bounding logic required.
- Exactly one coherent capability profile and, when active, exactly one
  installed compaction component governs the decision — there is no path to
  two competing compactors or a mismatched capability/profile pair.
- An upstream reducer can never violate Foundry's own structural-preservation
  or hard-limit guarantees, because its output is always independently
  verified and never trusted directly.
- A rehydrated body is delivered to the real provider at most once per outer
  run, and its durable reference always remains available for a later,
  explicit, budget-checked recovery — never silently duplicated, never
  silently discarded.
- Every assembly decision — success or termination — is inspectable through
  a categorical, privacy-safe snapshot without ever exposing conversation
  content. On success, the identical diagnostics instance is shared by the
  internal dispatch result (`HarnessBoundedMessageAssembly.Diagnostics`), the
  completed event, and the composed event. On termination, the diagnostics
  snapshot is built from the internal terminating assembly result and carried
  by the terminated event, but no caller-visible internal result exists to
  compare it against — the thrown `HarnessCompactionIrreducibleException`
  carries only the outcome, final estimated size, and hard limit, never the
  diagnostics instance itself. Every attempt's four possible progress events
  (started, and whichever of completed/composed or terminated follow) also
  carry one opaque `AssemblyId` generated once per attempt, so two
  concurrently-running assemblies for the same agent/workflow remain
  independently pairable despite their `SequenceNumber`s interleaving.
- The observability contract adds no ambient singleton, optional parameter,
  or public runtime configuration, consistent with ADR-0006's precedent.

### Negative

- The configured size estimator is not guaranteed to be token-exact unless it
  is explicitly labeled `EstimatedTokens`; a `Utf8Bytes`-labeled estimator's
  hard limit is a byte bound, not a provider token bound, even though the two
  are sometimes loosely correlated.
- An arbitrary upstream `IChatReducer`'s actual reduction quality and latency
  remain outside this decision's control; this decision bounds correctness
  (never accepting a non-reducing or structurally invalid proposal) and
  attempt count, not reducer quality.
- This leaf's structured diagnostics are inspectable per-decision (via the
  attached result or the corresponding progress event) but are not yet
  aggregated into `IAgentRunDiagnostics`, `IDiagnosticsSink`, or
  `IAgentMetrics`; that aggregation is deferred to later hardening scope.
- No direct NativeAOT execution proof exists for the hybrid compaction
  profile itself; Harness's existing hosted AOT coverage compiles the
  composition graph but does not execute a hybrid-enabled agent under
  NativeAOT, so this capability's AOT status is recorded honestly as
  `Unverified`, not `Verified`.
- No public runtime configuration exists yet for selecting or tuning a hybrid
  profile; every policy input remains an internal construction-time value
  until a public configuration surface is a separate, later decision.

### Neutral

- Retention and deletion of workspace-backed artifacts referenced from a
  compacted conversation remain governed entirely by ADR-0006; this decision
  introduces no new retention or deletion behavior of its own.
- The trust boundary recorded for the Compaction capability
  (`ExternalContent`) reflects that hybrid-compacted context is ultimately
  host-classified conversational/tool content flowing through the same
  classification path every other context entry already flows through — not
  a new or different trust concern this decision introduces.
- A public Harness composition/configuration API remains undecided; every
  type this decision introduces is either an internal mechanism or a
  documented public diagnostics/attribution/progress record.

## Confirmation

The decision is confirmed by:

- policy/trigger/fallback/termination tests covering `WithinLimit`,
  `Reduced`, `PreservationFallback`, `Irreducible`, and
  `ConcurrentMutationLimit` outcomes, including bounded reducer-attempt
  exhaustion and restart-after-mutation behavior;
- capability resolver tests proving Compaction is `Enabled` only when both
  requested and accepted at its delivery phase, `Deferred` when accepted but
  the delivery phase has not yet been reached, and `Disabled` when not
  requested at all, with exact trust-boundary, AOT, and diagnostics-status
  field values asserted directly;
- progress-event tests proving exactly one started event and exactly one
  terminal event (never both a completed and a terminated event) per
  assembly attempt, a context-composed event only on success and only after
  trust revalidation, and no compaction event of any kind when no hybrid
  profile is configured;
- measurement-unit tests proving every diagnostics size is labeled with the
  exact unit the governing estimator declared, and that a fixed/constant
  test estimator is never mislabeled as reporting an estimated token count;
- category-contribution tests proving the sum of every final per-category
  contribution equals the reported final size exactly, for every category
  this decision distinguishes;
- non-retransmission tests proving a rehydrated body reserved by one nested
  provider call's lease is filtered out of a second, concurrently-running or
  subsequent call's own snapshot within the same outer run, and that a
  failed or canceled call releases its reservation so a later retry can still
  reserve and eventually deliver the same digest;
- artifact attribution tests covering every offload and rehydration outcome,
  including multibyte payload and reference-byte-count correctness, proving
  the shared UTF-8 byte attribution never diverges between the internal
  snapshot and its corresponding progress event; and
- reflection-based privacy tests asserting that no diagnostics or progress
  event's string properties or default string representation ever carries
  raw message text, an artifact body, a workspace path, an owner identity,
  tool arguments/results, exception text, or classifier output text.

Local build and test validation for this leaf is recorded in
the Gate G5 record (git history; see issue #21). An MAF or MEAI
upgrade that changes provider-call middleware ordering, chat-reducer
behavior, or progress-reporting behavior must rerun these contracts before
this decision can claim continued compatibility.

## References

- ADR-0006 establishes `IWorkspace` as the sole authoritative bulk-artifact
  store and the explicit, verified rehydration contract this decision's
  transient-delivery and non-retransmission rules build on.
- ADR-0005 establishes the trusted execution binding and selected-provider
  composition root this decision's per-provider-call seam and capability
  resolution continue to rely on.
- the Gate G5 record (git history; see issue #21) records the
  cumulative implementation evidence, policy/trigger/fallback/termination
  matrices, and public API disposition for this decision.
- `HarnessContextDiagnostics`, `HarnessContextAttribution`, and the
  `HarnessContextCompactionStartedEvent` / `HarnessContextCompactionCompletedEvent`
  / `HarnessContextCompactionTerminatedEvent` / `HarnessContextComposedEvent`
  progress events carry the structured observability contract confirmed by
  this record.
