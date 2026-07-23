# Specification Review: First-Class Microsoft Agent Framework Harness Support

## Review Tracks

| Track | Model | Focus | Status |
|---|---|---|---|
| MAF architecture | Claude Opus 4.8 | Harness stable-line accuracy, sessions, middleware, provider boundaries | Complete |
| Hybrid context | Gemini 3.1 Pro | Compaction, preservation, workspace offload, rehydration | Complete |
| Migration governance | GPT-5.6 Terra | Brownfield scope, package boundaries, API evolution, AOT | Complete |
| Evaluation | Claude Opus 4.7 | Measurability, deterministic versus hosted evidence, uncertainty | Complete |

## Findings

### MAF architecture

- **High**: Compaction is upstream experimental and opt-in but the core story
  treated it as an implicit default.
- **High**: Harness default file memory and Foundry workspace offload could form
  competing authoritative stores.
- **Medium**: The specification did not define middleware/decorator ordering.
- **Medium**: Default-on Harness capabilities, especially message injection,
  were not inspectable at the Foundry boundary.
- **Medium**: Preservation requirements cannot be assumed from an upstream
  compaction strategy without verification.
- **Low**: Shell packaging and manual composition needed an explicit
  assumption.

### Hybrid context

- **Critical**: Oversized tool results must be offloaded eagerly before they
  enter chat history; retroactive compaction can be too late.
- **Critical**: Compaction must preserve valid tool-call/result message pairs.
- **High**: Rehydration decisions and stale-reference behavior were ambiguous.
- **High**: Recompaction needed a hard output bound and deterministic fallback.
- **High**: Compaction must trigger with enough margin to run the compactor.
- **Medium**: Irreducible context requires a structured termination.

### Migration governance

- **Blocking**: Core MAF/MEAI uplift must be a coherent application package
  graph and a prerequisite gate.
- **Blocking**: DevUI and Hosting preview compatibility must not indefinitely
  block stable core support.
- **Blocking**: Trusted execution identity and restored approval validation
  were underspecified.
- **Blocking**: The minimum NativeAOT Harness profile was not defined.
- **Non-blocking**: Stable capability adoption needed a staged capability
  matrix rather than an all-at-once commitment.
- **Non-blocking**: Concrete overlaps require owners, parity evidence, and
  release-bound retention or deletion decisions.

### Evaluation

- **Critical**: Hosted comparisons lacked complete fair-comparison controls and
  predeclared uncertainty treatment.
- **Critical**: Evaluation dimensions lacked operational definitions.
- **Critical**: Judge signals required calibration and bias evidence.
- **High**: Deterministic fixture invariants needed explicit fixture scope.
- **High**: Every decision dimension needed a deterministic anchor.
- **High**: Hosted stochastic results must not be sole automated gates.
- **High**: The case set required versioning and development-case separation.
- **Medium**: Latency, errors, cancellation, contention, and attribution needed
  explicit coverage.

## Disposition Log

| Finding | Disposition | Specification change |
|---|---|---|
| Experimental compaction drives P1 | Applied | US1 and SC-002 now require explicit experimental compaction opt-in; FR-012 records the status and safety margin |
| Competing file stores | Applied | FR-021 and Assumptions make Foundry workspace authoritative for the hybrid profile |
| Middleware ordering | Applied | FR-052 defines one tested composition order |
| Default-on capability control | Applied with upstream limitation | FR-053 requires effective-state inspection and records bundle defaults that cannot be disabled |
| Upstream preservation assumptions | Applied | FR-013 requires verification of the selected strategy |
| Eager tool-result offload | Applied | US1, edge cases, and FR-014 move offload to the tool-invocation boundary |
| Tool-call/result pair integrity | Applied | FR-013 requires valid MEAI message sequences |
| Rehydration mechanism | Applied with broader wording | FR-016 permits explicit tool requests or deterministic policies, but prohibits opaque injection |
| Stale references | Applied | FR-040 requires explicit stale-reference evidence |
| Bounded recompaction | Applied | FR-041 requires a hard output limit and deterministic fallback |
| Irreducible context | Applied | FR-049 requires distinct structured termination |
| Core package graph | Applied | FR-054 defines a coherent application dependency graph |
| DevUI/Hosting satellite gates | Applied | FR-008 and SC-008 separate stable core from satellite status |
| Trusted identity and approvals | Applied | FR-022 and FR-024 fail closed and require host validation |
| NativeAOT profile | Applied | FR-055 and SC-017 define a minimum supported profile |
| Capability staging | Applied | FR-026 and SC-018 require a versioned capability matrix |
| Overlap ownership/removal | Applied | FR-056 and SC-011 require concrete evidence and release-bound decisions |
| Fair-comparison controls | Applied | FR-044, FR-057, and SC-010 define pinned controls and uncertainty |
| Dimension definitions | Applied | FR-058 requires operational definitions |
| Judge calibration | Applied | FR-046 makes uncalibrated judge evidence advisory |
| Stochastic merge gates | Applied | FR-059 prohibits stochastic-only automated decisions |
| Deterministic fixture scope | Applied | SC-002, SC-004, SC-007, SC-013, and SC-014 are fixture-scoped |
| Cancellation and contention | Applied | SC-006 and SC-016 add explicit fixtures |
| Tool-only rehydration recommendation | Modified | Rehydration remains implementation-neutral but must be explicit, deterministic, and observable |
| Fixed numeric evaluation thresholds | Deferred | The specification requires evidence and uncertainty; thresholds remain a planning/product decision |
| Stable baseline advanced after review | Applied | NuGet verification on 2026-07-22 moved the planning candidate from first-stable 1.14.0 to coherent MAF/Harness 1.15.0; MEAI remains 10.6.0 |

## Final Review Outcome

All critical and blocking findings were addressed or deliberately modified with
documented rationale. No unresolved constitution conflict or clarification
marker remains. The specification is ready for technical planning after final
identifier and placeholder validation.
