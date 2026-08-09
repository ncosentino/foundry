---
description: Protocol-invalidated raw evidence from the first hosted autonomous-phase comparison run.
status: protocol-invalidated
---

# Autonomous Phase Evaluation 001 Results

!!! danger "Protocol invalidated"
    This run is preserved as immutable debugging evidence, but its arm pass
    counts and comparative conclusions are invalid. It must not be used to
    recommend, rank, or reject an orchestration architecture.

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

- Protocol status: **INVALIDATED**.
- 9 of 9 blocks completed.
- 0 infrastructure failures occurred.
- 14 applicable arm/scenario contracts failed.
- Comparative evidence strength: **PROTOCOL_INVALID**.
- Recommendation: **NONE**.
- Model-based semantic quality: **not calibrated and not scored**.

Contract failures are retained as evidence. The successful workflow conclusion
means the diagnostic matrix and publication infrastructure completed; it does
not mean every arm passed.

## Why the protocol was invalidated

An adversarial audit found that the implementation did not measure equivalent
arm quality:

- delegated Harness correctness required observing two child sessions, so an
  otherwise-correct result failed when the model used fewer children;
- prompts contained the expected JSON shape and expected evidence IDs;
- evidence scoring accepted an authorized subset instead of requiring complete
  manifest-derived coverage;
- one Magentic fault injector searched for text that differed from the actual
  prompt, so the intended fault did not activate;
- cancellation stopped an injected delay before provider or child work began;
- displayed fallback output could come from the response before fault
  injection rather than the value evaluated by the workflow; and
- the arms used different correction, round, reset, and provider-call budgets.

The same source and observed model also produced materially different
plain-Harness results across hosted and locally authorized runs. One trial per
scenario cannot separate stochastic variation from systematic behavior.

## Raw invalidated counts

| Arm | Applicable scenarios | Contract passes | Model calls | Input tokens | Output tokens |
| --- | ---: | ---: | ---: | ---: | ---: |
| Plain Harness | 9 | 7 | 18 | 11,814 | 1,748 |
| Delegated Harness | 9 | 1 | 38 | 25,158 | 3,118 |
| Phase-local Magentic | 8 | 4 | 85 | 230,129 | 25,371 |

These are single-trial workload totals. They are diagnostic observations, not
population estimates or valid comparative scores.

## Raw invalidated scenario matrix

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

## Evidence that remains valid

Only transport and publication facts survive protocol invalidation:

- The Actions `GITHUB_TOKEN` path can authenticate the direct Copilot client
  when used as the API bearer under `copilot-requests: write`.
- Every block and every available failure record was published.
- No calibrated model-judge evidence exists for semantic quality.

## Unresolved

- One trial per scenario cannot establish superiority, non-inferiority, or
  equivalence.
- Semantic fidelity, unsupported-claim rate, usefulness, and uncertainty
  honesty were not judged because the required calibration set does not exist.
- Cost is represented by tokens and calls, not authoritative billed AI credits.
- The stable model alias does not identify immutable backend model weights.
- No additional live trials are valid until the scorer, fault injection,
  cancellation boundary, arm budgets, traces, and calibration contracts are
  corrected and independently reviewed.

## API and guidance implication

This run does not justify a new public phase, artifact, all-settled, Magentic,
or evaluation abstraction.

No positive or negative architecture recommendation may be derived from these
counts. API decisions must use corrected evidence plus the independent
deterministic contracts of the affected feature.
