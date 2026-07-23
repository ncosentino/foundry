# Contract: Hybrid Context Assembly

## Purpose

Define the behavioral boundary that combines compacted conversation,
authoritative session state, workspace artifacts, eager tool-result offload,
and selective rehydration.

## Inputs

- Pinned system instructions
- Controlling user request and constraints
- Structured session state
- Ordered conversation blocks
- Tool-call/result transactions
- Live artifact references
- Explicit rehydration requests or deterministic rehydration decisions
- Effective model context boundary
- Reserved output allowance
- Declared token estimator or size estimator
- Compaction execution safety margin
- Cancellation

## Output

A valid ordered model-request message sequence plus a context-assembly snapshot
that explains:

- included blocks;
- excluded optional blocks;
- compaction decisions;
- artifact references;
- rehydrated bodies;
- category-level token or size attribution;
- remaining context capacity;
- sequence-validation result.

## Preservation contract

The assembler never silently drops:

- system instructions;
- controlling user constraints;
- accepted decisions;
- unresolved tasks and commitments;
- structured todos and modes;
- security and approval state;
- pending tool-call/result transactions;
- live artifact references;
- run, session, and correlation identity.

## Eager offload contract

- Oversized tool results are intercepted before their full payload enters chat
  history.
- The full payload is stored losslessly in Foundry workspace.
- Chat receives a bounded digest and artifact reference.
- Interception occurs in the tool-result transformation/serialization path,
  before an ordinary tool-result message containing the full payload is
  constructed or persisted.
- A tool that already writes an artifact can return a reference without creating
  a duplicate payload.
- Failed offload leaves conversation and workspace in an explicit, recoverable
  state or returns a structured failure.

## Compaction contract

- Compaction is explicit experimental opt-in.
- It triggers with enough margin to execute safely.
- System instructions remain verbatim.
- Tool-call/result transactions remain structurally valid.
- The selected strategy has a hard output bound.
- A failed or non-reducing strategy falls back deterministically or preserves
  the previous context only when it remains under the request envelope.
- An over-budget fallback must reduce eligible content or return structured
  irreducible-context termination; it cannot forward the unchanged over-budget
  context to the provider.
- Irreducible context returns a distinct structured termination.

## Rehydration contract

- Rehydration follows an explicit tool request or deterministic policy decision.
- A rehydration tool returns a request/reference outcome rather than the full
  payload through the ordinary tool-result path.
- The resolved payload enters the current context as a marked recoverable
  segment after eager-offload transformation, preventing immediate re-offload.
- Reference identity, ownership, and digest are validated.
- Missing, stale, unauthorized, and over-budget outcomes are distinct.
- Unrelated artifacts are not loaded.
- Rehydrated recoverable bodies may be evicted before durable references.

## Pipeline-surface contract

The implementation must model and trace two distinct composition surfaces:

1. `AIAgent` decorators and `AIContextProvider` lifecycle.
2. `IChatClient` middleware and the function-invocation loop.

The final ordering is proven against MAF 1.15 behavior. It is not inferred from
a single linear diagram.

## Envelope calculation

For each profile, the plan records:

```text
effective input envelope
  = provider/model context window
  - reserved model output allowance
  - compaction execution safety margin
```

The profile declares the estimator used for deterministic tests and the source
of provider/model limits. "Bounded overhead" means a predeclared absolute or
relative allowance for required summary metadata; a compaction pass outside
that allowance is non-reducing and must use fallback or terminate.
