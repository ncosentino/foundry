---
title: "ADR-0006: Hybrid context and workspace authority"
status: "Accepted"
date: "2026-07-24"
authors: ["Nick Cosentino"]
tags: ["architecture", "decision", "agent-framework", "harness", "workspace", "context", "observability"]
supersedes: ""
superseded_by: ""
---

## Context and scope

ADR-0005 established that `IWorkspace` remains Foundry's authoritative
bulk-artifact abstraction and that no `AgentFileStore` bridge is approved as a
second authority. Building on that foundation, Foundry's selected-provider and
iterative Harness lanes now eagerly offload oversized tool results to the
authorized workspace, mint bounded content-addressed references for them, and
explicitly resolve those references back into recoverable context segments on
request. This decision refines ADR-0005's workspace-authority framing into a
concrete hybrid-context contract for that offload/rehydration mechanism, and
adds privacy-safe, structured observability over both directions of it. It is
a refinement of an accepted decision, not a supersession: ADR-0005's
composition, ownership, and trust-binding rules are unchanged and continue to
govern how any workspace access is authorized.

This decision governs three things: which store is authoritative for bulk
artifact bytes, what a conversation is allowed to carry in place of those
bytes, and how — and how observably — a caller may get the bytes back. It does
not decide a conversation compaction policy (when or whether to summarize,
evict, or automatically re-offload context), does not approve a public Harness
API, and does not aggregate this observability into completed-run diagnostics
or metrics; those remain separate, later decisions.

## Decision drivers

- A conversation held in a chat history must not silently grow to contain
  every large tool result it has ever produced.
- Exactly one component may hold bulk artifact bytes as ground truth; a second
  authoritative store would reintroduce the divergence and staleness problems
  ADR-0005 already rejected.
- Any reference a conversation carries in place of bytes must be bounded,
  content-addressed, and verifiable, not a bare path or an opaque handle.
- Recovering an artifact's body must be an explicit, request-driven act with
  its own binding, digest, and budget checks — never an implicit or
  automatically triggered side effect of reading a reference.
- Offload and rehydration decisions must be inspectable by tests and operators
  without ever exposing artifact bodies, workspace paths, owner identity, or
  raw exception text.
- The `AgentFileStore` bridge introduced for MAF interoperability must not be
  mistaken for, or grow into, a second source of truth for artifact content.
- Adding observability must not introduce an ambient singleton, an optional
  parameter, or a public runtime configuration surface.

## Decision

Foundry's `IWorkspace` is the sole authoritative store for bulk artifact
bytes. A conversation never carries a tool result's full body once that
result has been offloaded; it carries only a bounded
`artifact://sha256/<digest>` reference, sized to be inspected and remembered
cheaply. At or below a configured inline byte threshold, content is kept in
the conversation as-is and no reference is minted, so the threshold's own
selection behavior stays inspectable at every size, not only above it. Above
the threshold, content is offloaded once, addressed by digest, and the same
reference is reused on a subsequent identical write rather than duplicating
the write.

Rehydration is always an explicit act on an explicit reference, never an
implicit consequence of a reference merely appearing in context. A dedicated
rehydration mechanism resolves one reference at a time and only ever returns
a usable body when every one of three checks passes: the reference's
recorded owner matches the current trusted execution binding, the content at
the reference's workspace path still matches the reference's recorded digest,
and the observed size fits the caller-supplied byte budget. Any other outcome
returns a bounded, categorical result and never injects content.

The MAF `AgentFileStore` bridge is, and remains, a partial adapter over the
same authorized `IWorkspace` — not a second authority and not the path either
the offload or the rehydration mechanism uses to reach workspace bytes. Both
mechanisms read and write the bound `IWorkspace` directly, so the bridge's
unsupported operations, list-cost characteristics, and cancellation
limitations have no bearing on whether an artifact can be offloaded or
rehydrated.

Both directions of this decision are now observable through one shared,
privacy-safe contract: a categorical operation (offload or rehydration), a
categorical outcome, a categorical content kind (an ordinary tool result or a
recovered context segment), a stable categorical reason, the observed and
configured/budgeted UTF-8 byte counts, and the bounded reference identity when
one exists. Rehydration decisions and the offload seam's explicit
recoverable-segment bypass use the recovered-context category; ordinary
tool-result decisions use the tool-result category. Every categorical reason
is assigned at the exact
point a decision is made through a deterministic mapping or factory — never
recovered by parsing a human-readable evidence string, and every non-null
reference identity is validated to be exactly the canonical
`artifact://sha256/{64-lowercase-hex}` shape before a snapshot can be
constructed. This structured snapshot is attached to the internal offload
outcome or rehydration result the decision produced, and the identical
instance is what a public progress event reports, so a test or operator
observing either surface sees the same data; the events additionally carry
the reporting agent's true parent, resolved through a narrow internal
reporter-context accessor rather than a hard-coded value, so nested
sub-agent correlation is accurate without expanding the public
`IProgressReporter` surface. Reporting is best-effort through the existing
progress-reporter accessor seam: when no accessor or no active scope is
present, ordinary behavior is fully preserved and nothing is reported or
thrown. No new ambient singleton, optional parameter, or public runtime
configuration is introduced; the diagnostics and progress types are the only
new public surface — the snapshot factories themselves remain internal — and
every public member carries complete documentation.

## Alternatives considered

### Let `AgentFileStore` be a second authoritative store

Bulk bytes could instead be considered authoritative wherever the `AgentFileStore`
bridge last observed them, letting MAF-facing code treat the bridge as its own
store. This was rejected because it reopens exactly the divergence and
staleness risk ADR-0005 already closed: two authorities for the same bytes
means one of them can drift, and the bridge's own unsupported operations
(delete, bounded search) make it structurally unfit to be trusted as ground
truth.

### Implicit/automatic rehydration

A reference appearing in conversation history could be automatically resolved
back to its body the next time it is read, saving callers an explicit step.
This was rejected because it removes the caller's ability to bound *when* a
potentially large body re-enters context, defeats the budget check this
decision requires on every resolution, and would make a reference's mere
presence in history carry an implicit side effect rather than a bounded,
inert identifier.

### Truncate or summarize instead of referencing

Oversized tool results could be truncated or summarized in place rather than
offloaded and referenced. This was rejected because both approaches are lossy
by construction: truncation discards data unpredictably and summarization
requires a model call and an accuracy judgment neither this decision nor its
underlying mechanism is positioned to make. A content-addressed reference
preserves the exact body for later, explicit, verified recovery instead.

### Derive categorical reasons from existing human-readable evidence text

Existing internal failure paths already produce descriptive evidence
strings; reasons could be derived from those strings via pattern matching
rather than adding a parallel structured field. This was rejected because
string-derived categories are brittle against future wording changes and
because deriving a stable category from free text is itself a privacy risk
surface — the safer contract is a reason assigned directly by the factory or
deterministic mapping that already knows which case it is handling.

## Consequences

### Positive

- A conversation's size is decoupled from the cumulative size of every large
  tool result it has ever produced.
- Exactly one authoritative store exists for bulk artifact bytes; the MAF
  bridge cannot be mistaken for a second one.
- Every rehydration is bound, digest-verified, and budget-checked before any
  body is returned, so a stale, unauthorized, or oversized reference never
  silently reappears in context.
- Offload and rehydration decisions are inspectable end-to-end — including
  the Inline case — without ever exposing an artifact body, a workspace path,
  owner identity, or a raw exception message.
- The observability contract is exactly-once per decision and adds no ambient
  singleton, optional parameter, or public runtime configuration.

### Negative

- `IWorkspace` exposes no size metadata, so resolving a reference against a
  byte budget still requires reading the full body first; the budget check is
  applied after the read rather than short-circuiting it.
- The structured diagnostics attached to an offload outcome or rehydration
  result are not yet aggregated into completed-run diagnostics, sinks, or
  metrics; an operator must currently inspect the outcome/result or progress
  event directly rather than a run-level summary.
- Rehydrated content is only ever content the same session previously
  offloaded through this mechanism; there is no cross-session or
  cross-owner sharing path, by design of the owner-match check.
- No delete or retention policy exists for offloaded artifacts; a workspace
  accumulates every distinct offloaded digest indefinitely under this
  decision alone.

### Neutral

- Conversation compaction policy — if and when a session ever needs to
  proactively evict or re-offload already-inlined context — remains a
  separate, later decision and is explicitly out of scope here.
- The MAF `AgentFileStore` bridge's own supported/unsupported operation
  surface is unchanged by this decision; it continues to matter only for MAF
  interoperability, never for offload or rehydration correctness.
- A public Harness API remains undecided; every type introduced by this
  decision is either an internal mechanism or a documented public
  diagnostics/progress record, not a configuration or composition surface.

## Confirmation

The decision is confirmed by:

- offload tests covering the inline, offloaded, existing-reference, no-workspace-failed,
  and recovery-required outcomes, including the exactly-once-write and
  reuse-on-identical-content behaviors;
- rehydration tests covering the resolved, stale, missing, unauthorized, and
  over-budget outcomes, including owner-mismatch and digest-mismatch
  detection;
- observability tests asserting correlation and sequencing across mixed
  offload and rehydration decisions, categorical outcome/reason/content
  correctness, observed and configured/budgeted byte counts, bounded
  reference identity, and exactly one event per decision;
- child and nested-child reporter tests confirming `AgentId`, `ParentAgentId`,
  `Depth`, and a shared, monotonically increasing sequence number are
  correctly correlated for both offload and rehydration events, including
  through a private, non-public reporter-context accessor that never expands
  the public `IProgressReporter` surface and falls back to `null` safely for
  a reporter that does not implement it;
- tests asserting that the internal diagnostics factories reject a malformed,
  wrong-prefix, or non-canonical reference identity, and reject a reference
  identity on an outcome that must not carry one;
- reflection-based privacy tests asserting that no artifact body, workspace
  path, owner identity, or raw exception message ever appears in a
  diagnostics or progress event's string properties or default string
  representation, including a dedicated case that injects a unique
  workspace-write exception message and confirms it never leaks;
- tests asserting that a missing progress accessor or an accessor without an
  active scope preserves ordinary offload/rehydration behavior, emits
  nothing, and never throws; and
- tests asserting that the diagnostics instance attached to an internal
  offload outcome or rehydration result is exactly the same instance carried
  by its corresponding progress event.

Local build and test validation for this leaf is recorded in
the Gate G4 record (git history; see issue #20). An MAF or MEAI
upgrade that changes workspace, tool-result, or progress-reporting behavior
must rerun these contracts before this decision can claim continued
compatibility.

## References

- ADR-0005 establishes selected-provider MAF Harness integration, the trusted
  execution binding, and the "`IWorkspace` is authoritative, no `AgentFileStore`
  bridge is a second authority" framing this decision refines.
- the Gate G4 record (git history; see issue #20) records the
  cumulative implementation evidence, status matrix, and public API
  disposition for this decision.
- the workspace identity feasibility record (git history)
  demonstrates why the `AgentFileStore` bridge remains partial and why
  workspace authority must be bound per execution.
- `HarnessArtifactDiagnostics`, `HarnessArtifactOffloadDecisionEvent`, and
  `HarnessArtifactRehydrationDecisionEvent` carry the structured
  observability contract confirmed by this record.
