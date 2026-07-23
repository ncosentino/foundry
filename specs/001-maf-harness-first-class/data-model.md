# Data Model: First-Class Microsoft Agent Framework Harness Support

This document describes planning-level entities and state transitions. Names are
conceptual and do not commit Foundry to public C# type names.

## Harness Capability Profile

Represents the effective Harness-related capabilities for one agent.

### Fields

- Profile identity and version
- Agent construction lane: selected core providers or complete Harness bundle
- Capability entries
- Chat-provider requirements
- Experimental diagnostic status
- Middleware-order version
- Workspace-binding mode
- Session-persistence mode
- Telemetry ownership

### Validation

- Experimental capabilities require explicit opt-in.
- Provider-dependent capabilities require positive provider evidence.
- The effective profile must contain exactly one tool-invocation loop.
- Telemetry ownership must prevent duplicate instrumentation.
- The complete bundle must report default-on capabilities that cannot be
  overridden.

## Capability Entry

Represents one Harness capability in the versioned capability matrix.

### Fields

- Capability name
- Source package and version
- Stability: stable, experimental, provider-dependent, unsupported, deferred
- Default state
- Effective state
- Required provider capability
- Trust-boundary classification
- AOT status
- Diagnostics status
- Delivery phase

### State transitions

```text
Unassessed
  -> Supported
  -> ExperimentalOptIn
  -> Deferred
  -> Unsupported
```

Transitions require evidence and a recorded rationale.

## Trusted Execution Binding

Binds session and workspace state to a trusted host-issued execution identity.

### Fields

- User identity
- Orchestration identity
- Session identity
- Workspace identity
- Issuer or host boundary
- Validation status
- Tenant or partition identity when applicable

### Validation

- Missing or invalid identity fails closed.
- Restored session state must match the active binding.
- A session cannot read or mutate another binding's workspace or approvals.

## Agent Session Envelope

Represents logical session continuity and persistence evidence.

### Fields

- Session identity
- Chat-history provider kind
- Durability classification: in-memory, serialized, durable provider,
  service-managed
- Conversation working-set reference
- Structured session-state reference
- Workspace identity
- Enabled-provider state references
- Approval-state reference
- Serialization version or provider version

### Validation

- In-memory sessions are explicitly non-crash-durable.
- Restored provider state reports unsupported or missing fields.
- Restored standing approvals require host validation or reauthorization.

## Conversation Working Set

The bounded ordered messages considered for the next model call.

### Fields

- Pinned system instructions
- Controlling user request and constraints
- Recent conversation blocks
- Compacted summary blocks
- Tool-call/result transaction blocks
- Artifact references
- Rehydrated recoverable bodies
- Token or size estimate
- Effective context boundary
- Reserved output allowance

### Invariants

- System instructions remain verbatim.
- Tool calls and results remain paired.
- Required session state is not derived solely from summaries.
- The working set remains under the effective request envelope.
- Rehydrated bodies are distinguishable from durable references.

## Conversation Block

An indivisible sequence used by compaction and reduction.

### Kinds

- System instruction
- User request
- Assistant response
- Tool-call/result transaction
- Compacted summary
- Artifact reference
- Rehydrated artifact body

### Validation

- Tool-call/result transactions cannot be split.
- Pinned blocks cannot be evicted.
- Summary blocks record source-range and compaction evidence.

## Structured Session State

Small authoritative state that remains separate from conversation.

### Fields

- Current phase or mode
- Iteration and run counters
- Budget snapshot
- Todo and plan state
- Accepted decisions and constraints
- Pending commitments
- Approval state
- Background-work state
- Live artifact-reference set

### Invariants

- Structured state overrides conflicting conversational summaries.
- Security-relevant approval state cannot be changed by compaction.
- State growth is bounded by an explicit policy.

## Workspace Artifact

Bulk or durable content stored in Foundry workspace.

### Fields

- Canonical workspace path
- Content digest
- Size
- Media or content type
- Producing run, tool, and step
- Ownership binding
- Creation and update evidence
- Retention status
- Compare-exchange version when applicable

### Validation

- Canonical paths and traversal rejection follow `IWorkspace`.
- Reads verify ownership and digest.
- Concurrent updates preserve compare-exchange semantics.

## Artifact Reference

A bounded conversational handle to a workspace artifact.

### Fields

- Reference identity
- Canonical workspace path
- Content digest
- Content size
- Description or bounded summary
- Ownership binding
- Created-at run and step
- Staleness status
- Rehydration priority or pin state

### State transitions

```text
Live
  -> Rehydrated
  -> Live
  -> Stale
  -> Missing
  -> Expired
```

Stale, missing, or unauthorized references produce explicit evidence and do not
silently inject content.

## Tool-Result Offload Decision

Records whether a raw tool result is stored before entering chat history.

### Fields

- Tool call identity
- Tool name and argument digest
- Raw result size or token estimate
- Active context envelope
- Threshold policy and version
- Decision: inline, offload, existing artifact reference, fail
- Artifact reference when offloaded
- Failure evidence

### State transitions

```text
RawResult
  -> OffloadPending
  -> ArtifactPersisted
  -> ReferenceCommitted
  -> InlineResult
  -> OffloadedReference
  -> RecoveryRequired
  -> Failed
```

The offload decision occurs before the full result is appended to chat history.
Partial persistence and reference-commit failures are recorded so retries are
idempotent and cannot create silent duplicate artifacts.

## Compaction Decision

Records why and how conversation was reduced.

### Fields

- Trigger reason
- Strategy identity and stability
- Before and after token or size estimates
- Token margin
- Preserved block identities
- Evicted or summarized block identities
- Tool-sequence validation result
- Compactor token usage
- Fallback used
- Failure or irreducible-context evidence

### Validation

- Compaction does not modify system instructions.
- Output remains within the configured hard limit.
- Recompaction does not grow without bound.
- Valid tool-call/result sequencing is revalidated before dispatch.

## Rehydration Decision

Records an explicit request to expand an artifact reference into the active
working set.

### Fields

- Reference identity
- Request source: tool request or deterministic policy
- Relevance evidence
- Remaining context envelope
- Digest verification result
- Decision: full, partial, refused, stale, missing, unauthorized
- Included size
- Rehydration token or size attribution
- Delivery mode: marked recoverable context segment

### Validation

- Rehydration is observable and budget-aware.
- Unrelated artifacts are not loaded.
- Rehydrated bodies remain recoverable and may be evicted before references.
- Rehydrated content bypasses eager re-offload for the active request and is
  never returned as an ordinary oversized tool-result payload.

## Context Assembly Snapshot

Records the exact categories used to construct one model request.

### Fields

- Run, session, and step identity
- Capability profile
- Effective context boundary
- Reserved output allowance
- Token or size contribution by category
- Included conversation blocks
- Included artifact references
- Rehydration decisions
- Reduction decisions
- Final sequence-validation result

## Migration Gate Evidence

Represents a decision gate before dependency, capability, API, or removal work
proceeds.

### Fields

- Gate identity
- Scope: core uplift, satellite, composition, workspace, experimental
  capability, bundle, removal
- Required evidence
- Evidence artifact links
- Status: pending, passed, failed, deferred
- Reviewer and review date
- Rationale
- Follow-up trigger

## Temporary Duplication Entry

Tracks a parallel implementation during migration.

### Fields

- Existing path
- Candidate path
- Reason for coexistence
- Owner
- Parity evidence
- Removal or retention criteria
- Release-bound decision point
- Current disposition

## Evaluation Case Manifest

Defines one versioned comparative task case.

### Fields

- Case and case-set version
- Task category
- Input and controlling instructions
- Chat provider and model configuration
- Tool and workspace fixture versions
- Deterministic acceptance predicates
- Dimension-specific reference evidence
- Development-case status
- Trial and aggregation policy reference

## Evaluation Run Manifest

Records one comparable execution.

### Fields

- Execution mode
- Package and capability-profile versions
- Case-set version
- Sampling configuration
- Budgets, timeouts, retries, and cancellation policy
- Initial workspace state
- Deterministic results
- Judge model and rubric when used
- Uncertainty method
- Diagnostics and artifact locations

## Principal Relationships

```text
Harness Capability Profile
  -> Agent Session Envelope
  -> Trusted Execution Binding
  -> Conversation Working Set
  -> Structured Session State
  -> Artifact References
  -> Workspace Artifacts

Tool Result
  -> Tool-Result Offload Decision
  -> Workspace Artifact
  -> Artifact Reference
  -> Conversation Working Set

Conversation Working Set
  -> Compaction Decision
  -> Context Assembly Snapshot
  -> Model Request

Artifact Reference
  -> Rehydration Decision
  -> Context Assembly Snapshot

Migration Gate Evidence
  -> Capability Entry
  -> Temporary Duplication Entry
```
