# Overlap Retention Decisions

**Task:** T121  
**Decision status:** Approved  
**Reviewer:** `@ncosentino`  
**Reviewed:** 2026-07-30T14:32:59Z  
**Decision:** `RetainAllPendingStrongerEvidence`

## Evidence basis

This decision rests only on the human-reviewed publication for workflow run
[30513567405](https://github.com/ncosentino/foundry/actions/runs/30513567405):

- `artifacts/eval/reports/harness-001/run-30513567405-publication.md`
- `artifacts/eval/reports/harness-001/run-30513567405-human-review.json`
- `artifacts/eval/reports/harness-001/run-30513567405-publication-manifest.json`

Bundle checksum digest:
`f76e31d9424e15b8d0b56b12b75a1020626284adb361c609a659a07dfa370d29`

The GitHub Models runs `30270567078` and `30273935931`, the raw token-exchange
run `30400286731`, and the probe-gated run `30511810798` remain excluded and are
not pooled into this decision.

## What the evidence supports

- Eight fully scheduled cases, 24/24 complete paired batches, and 72/72 arm
  trials executed through the approved Copilot Enterprise path.
- Diagnostics parity is 8/8 comparable cases for every contrast.
- `retentionEligible: true` means only that the pre-registered minimum of six
  fully scheduled cases was met.

## What the evidence does not support

- Every completion interval includes zero and every contrast is underpowered.
- Every secondary binary interval includes zero.
- Every continuous comparison is `insufficient-sample` with at most two paired
  cases, and pessimistic sensitivity materially diverges for hybrid token
  metrics.
- Judge evidence is `UNCALIBRATED`; observed judge agreement is not established,
  so no judge signal supports arm ranking.

Plain Harness completed 24/24 item executions while Iterative and Hybrid each
completed 21/24, but that difference is not statistically separable at this case
count and does not establish superiority.

## Decision

Retain all three execution modes with no default change and no removal:

| Overlap | Decision | Rationale |
|---|---|---|
| DUP-001 Foundry `IWorkspace` and upstream file stores | Retain | Workspace remains the authoritative bulk-content store; artifact-reuse differences are zero. |
| DUP-002 `IIterativeAgentLoop` and Harness/LoopAgent | Retain | No workload-specific parity evidence separates the loops; every completion interval includes zero. |
| DUP-006 Foundry preservation policy and upstream compaction | Retain | Hybrid `h001-02` failed closed on irreducible compaction, which argues against removing the Foundry preservation contract. |
| DUP-008 Selected provider lane and complete bundle | Retain | Both lanes retain distinct supported scenarios; the comparison did not test lane consolidation. |

Overlaps DUP-003, DUP-004, DUP-005, and DUP-007 were reviewed at G2, G5, or G7
and are unchanged by this hosted comparison.

## Consequences

- No accepted ADR is superseded and none is rewritten. ADR-0005, ADR-0006,
  ADR-0007, and ADR-0008 remain accurate: they describe retained, optional, and
  experimental surfaces rather than a selected default.
- The experimental hybrid profile stays internal and experimental. Its
  irreducible-compaction failure is a known limitation, not a removal trigger.
- Removal of any retained surface requires a new case-set version with a larger
  case population, workload-specific parity evidence, and migration guidance.

## Reversal criteria

Revisit this decision when any of the following becomes true:

- a paired completion interval excludes zero at a pre-registered case count that
  is not underpowered;
- continuous evidence reaches a sufficient paired sample under the frozen
  protocol; or
- judge agreement is measured and reaches the pre-registered calibration
  threshold, making judge evidence usable for ranking.
