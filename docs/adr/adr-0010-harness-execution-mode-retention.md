# ADR-0010: Retain all Harness execution modes pending stronger evidence

## Status

Accepted — 2026-07-30

## Context

Foundry ships three overlapping ways to run a long-lived agent:

1. the Foundry workspace-driven iterative loop;
2. plain Harness with explicit compaction; and
3. an experimental hybrid of Harness compaction plus the Foundry workspace.

That overlap was accepted deliberately during Harness integration, on the
condition that a later comparison would decide whether to select a default or
remove a surface.

A pre-registered hosted comparison was executed and published. Workflow run
[30513567405](https://github.com/ncosentino/foundry/actions/runs/30513567405)
ran on a dedicated PitCrew runner through the official GitHub Copilot SDK with
Copilot Enterprise billing: 24/24 complete paired batches, 72/72 scheduled arm
trials, 164 provider requests, 8/8 fully scheduled cases, and 307/307 verified
checksums.

## Evidence

| X minus Y | Completion difference | 95% paired interval |
|---|---:|---:|
| Plain Harness minus Iterative | +0.125 | [-0.215, 0.471] |
| Hybrid minus Iterative | 0.000 | [-0.375, 0.375] |
| Hybrid minus Plain Harness | -0.125 | [-0.471, 0.215] |

Every completion interval includes zero and every contrast is underpowered at
eight cases. Every secondary binary interval includes zero. All twelve
continuous comparisons are `insufficient-sample`, retaining at most two paired
cases. Diagnostics parity is 8/8 comparable cases for every contrast. Judge
evidence remains `UNCALIBRATED` and unusable for arm ranking because observed
judge agreement was never measured.

Plain Harness completed 24/24 item executions while Iterative and Hybrid each
completed 21/24, driven by two systematic failures: Iterative exhausted the
eight-tool-call cap on one case, and Hybrid failed closed on irreducible
compaction on another. Those differences are not statistically separable at this
case count.

## Decision

Retain all three execution modes. Change no default and remove nothing.

| Overlap | Decision | Rationale |
|---|---|---|
| Foundry `IWorkspace` and upstream file stores | Retain | Workspace remains the authoritative bulk-content store; artifact-reuse differences were zero |
| Iterative loop and Harness/LoopAgent | Retain | No workload-specific parity evidence separates them |
| Foundry preservation policy and upstream compaction | Retain | Hybrid failed closed on irreducible compaction, arguing against removing the preservation contract |
| Selected-provider lane and complete bundle | Retain | Each retains a distinct supported scenario; lane consolidation was never tested |

`retentionEligible: true` in the published evidence means only that the
pre-registered six-case scheduling minimum was met. It is not a recommendation.

## Human review

The decision is bound to a human signature. `@ncosentino` reviewed run
`30513567405` on 2026-07-30 and acknowledged the deterministic anchors, paired
uncertainty, diagnostics parity, judge omission, and cap/truncation treatment.

The signature is recorded in
`artifacts/eval/reports/harness-001/run-30513567405-human-review.json`, bound to
bundle checksum
`f76e31d9424e15b8d0b56b12b75a1020626284adb361c609a659a07dfa370d29`.

## Consequences

- No ADR is superseded. ADR-0005, ADR-0006, ADR-0007, and ADR-0008 continue to
  describe retained, optional, and experimental surfaces rather than a selected
  default.
- The experimental hybrid profile stays internal and experimental. Its
  irreducible-compaction failure is a known limitation, not a removal trigger.
- Consumers keep choosing explicitly between plain Foundry MAF agents, the
  optional Harness bundle, and the Foundry iterative loop.
- Removing any retained surface requires a new case-set version with a larger
  case population, workload-specific parity evidence, and migration guidance.

## Reversal criteria

Revisit this decision when any of the following becomes true:

- a paired completion interval excludes zero at a case count that is not
  underpowered;
- continuous evidence reaches a sufficient paired sample under the frozen
  protocol; or
- judge agreement is measured and reaches the pre-registered calibration
  threshold, making judge evidence usable for ranking.

Tracked as [#142](https://github.com/ncosentino/foundry/issues/142) and
[#143](https://github.com/ncosentino/foundry/issues/143).

## Provenance note

This ADR is the durable home of a decision first recorded during the Harness
migration program at
`specs/001-maf-harness-first-class/evidence/retention-decisions.md`. That
feature specification folder was removed from the working tree by an approved
retention decision; its contents remain in git history and in the GitHub issues
that narrate the program.

The signed publication
`artifacts/eval/reports/harness-001/run-30513567405-publication.md` is immutable
and checksum-bound, so it still names the original specification path. It was
deliberately not edited, because editing it would invalidate the human signature
bound to it. This ADR is the current location of that decision.
