---
description: Results from the first hosted diagnostic run of the autonomous-phase comparison protocol.
---

# Autonomous Phase Evaluation 001 Results

## Evidence identity

| Field | Value |
| --- | --- |
| Workflow run | [31296045070](https://github.com/ncosentino/foundry/actions/runs/31296045070) |
| Source commit | `0ed7f680c9de29c7b276895b7bbf03fa198586cd` |
| Protocol | `autonomous-phase-eval-v1` |
| Requested model | `gpt-4.1` |
| Observed model | `gpt-4.1-2025-04-14` |
| MAF | `1.17.0.0` |
| MEAI | `10.8.0.0` |
| Artifact | `autonomous-phase-evaluation-31296045070-1` |
| Artifact ID | `9033156468` |
| Artifact digest | `sha256:aa353642abb194469666a77fd8da213eff6a66d253d3c038f4ea7fdec12cfe11` |
| Artifact expiry | 2026-11-07 |
| `report.json` SHA-256 | `842604d3242af36ba0e5bd9aa9c5f240978ef3c8dfe7b2faf2f453b33eb38b98` |
| `experiment.json` SHA-256 | `20fda8bd0bd780fa47813bd057f08625b3ec684ba9c2a46978a978adddac87ae` |

The provider probe passed with one ordinary response, one tool call, a final
tool-result response, model identity, and usage. The job used the short-lived
Actions installation token directly as the Copilot bearer; the legacy user
OAuth exchange endpoint does not accept that token type.

## Run disposition

- 9 of 9 blocks completed.
- 0 infrastructure failures occurred.
- 14 applicable arm/scenario contracts failed.
- Evidence strength: **INSUFFICIENTLY_POWERED**.
- Recommendation: **NO_SUPPORTED_RECOMMENDATION_YET**.
- Model-based semantic quality: **not calibrated and not scored**.

Contract failures are retained as evidence. The successful workflow conclusion
means the diagnostic matrix and publication infrastructure completed; it does
not mean every arm passed.

## Aggregate diagnostic results

| Arm | Applicable scenarios | Contract passes | Model calls | Input tokens | Output tokens |
| --- | ---: | ---: | ---: | ---: | ---: |
| Plain Harness | 9 | 7 | 18 | 11,814 | 1,748 |
| Delegated Harness | 9 | 1 | 38 | 25,158 | 3,118 |
| Phase-local Magentic | 8 | 4 | 85 | 230,129 | 25,371 |

These are single-trial workload totals. They are diagnostic observations, not
population estimates.

## Scenario contract matrix

| Scenario | Plain Harness | Delegated Harness | Magentic |
| --- | --- | --- | --- |
| Success | Pass | Fail | Fail |
| Optional branch failure | Pass | Fail | Pass |
| Required branch failure | Pass | Pass | Pass |
| Correction succeeds | Pass | Fail | Not applicable |
| Correction exhausted | Pass | Fail | Fail |
| Cancellation | Pass | Fail | Pass |
| Checkpoint restore | Pass | Fail | Fail |
| Delivery replay | Fail | Fail | Pass |
| Ineffective progress | Fail | Fail | Fail |

## Verified findings

The following claims are direct deterministic observations from the run:

- The Actions `GITHUB_TOKEN` path can authenticate the direct Copilot client
  when used as the API bearer under `copilot-requests: write`.
- Every block and every available failure record was published.
- Required-branch failure correctly skipped synthesis and preserved the
  optional sibling in all three arms.
- Plain Harness completed the ordinary, optional-failure, correction-success,
  correction-exhaustion, cancellation, and checkpoint-restore contracts.
- Delegated Harness did not demonstrate both configured background children in
  its live synthesis scenarios; observed child-session count was at most one.
- Magentic created plans and replans, and its participant coordination consumed
  substantially more provider calls and tokens than plain Harness in this
  fixture.
- No calibrated model-judge evidence exists for semantic quality.

## Directional findings

The following observations are useful engineering signals but are not powered
recommendations:

- Plain Harness was the least expensive and most contract-reliable arm in this
  one synthetic case.
- Background delegation added configuration and operational cost without a
  demonstrated contract benefit. The model often failed to use both children
  despite explicit instructions.
- Magentic handled the optional-gap scenario but was unstable on ordinary
  completion and recovery, and consumed about 19.5 times the plain-Harness input
  tokens in aggregate.
- The one-speaker-per-round manager loop is a poor match for work that is
  naturally parallel; the outer macro fan-out should remain outside Magentic.
- The existing artifact boundary and idempotent delivery contract were more
  reliable than the internal orchestration strategies they contained.

## Insufficiently powered or unresolved

- One trial per scenario cannot establish superiority, non-inferiority, or
  equivalence.
- Some failures were provider or trajectory failures after a single call; they
  need repeated trials to distinguish stochastic behavior from systematic arm
  defects.
- Semantic fidelity, unsupported-claim rate, usefulness, and uncertainty
  honesty were not judged because the required calibration set does not exist.
- Cost is represented by tokens and calls, not authoritative billed AI credits.
- The stable model alias does not identify immutable backend model weights.
- A fixed-size study should not be started until the remaining scenario
  adapters and arm behavior are stable enough that additional trials would
  measure the arms rather than test-harness drift.

## API and guidance implication

This run does not justify a new public phase, artifact, all-settled, Magentic,
or evaluation abstraction.

The evidence supports:

- keeping the fixed macro graph and artifact contracts example-local;
- documenting plain Harness as the simplest candidate for open-ended phases,
  without claiming statistical superiority;
- keeping background delegation opt-in and bounded;
- keeping Magentic phase-local and experimental;
- preserving raw MAF checkpoint ownership with caller-owned persistence; and
- deferring API extraction until the same glue repeats in another real
  integration with stronger hosted evidence.

The supported recommendation remains **no promotion from the demonstrated
experimental/example surfaces**.
