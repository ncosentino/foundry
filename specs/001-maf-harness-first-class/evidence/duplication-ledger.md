# Temporary Duplication Ledger — Release Dispositions

**Task:** T125  
**Reviewed:** 2026-07-30

Every entry from the plan's temporary duplication ledger has a release-bound
disposition below. Dispositions for DUP-001, DUP-002, DUP-006, and DUP-008 are
bound to the approved G8 decision in
`evidence/retention-decisions.md`, which is signed by `@ncosentino` against
bundle checksum
`f76e31d9424e15b8d0b56b12b75a1020626284adb361c609a659a07dfa370d29`.

## Dispositions

| ID | Overlap | Review group | Disposition | Bound to |
|---|---|---|---|---|
| DUP-001 | Foundry `IWorkspace` and upstream file stores | G8 | **Retain both.** Workspace stays the authoritative bulk-content store. Artifact-reuse differences were `0.0` in every contrast. | G8 decision |
| DUP-002 | `IIterativeAgentLoop` and Harness/LoopAgent | G8 | **Retain both.** No workload-specific parity evidence separates the loops; every completion interval includes zero. | G8 decision |
| DUP-003 | Foundry diagnostics and upstream OpenTelemetry | G7 | **Resolved.** One declared telemetry owner per profile; the Foundry bundle emits no additional spans or metrics. No temporary suppression bridge remains to delete. | Gate G7 |
| DUP-004 | Foundry function loop and Harness invocation | G2 | **Resolved.** Exactly one tool-invocation loop per effective profile; dual-loop combinations are rejected before execution. | Gate G2 |
| DUP-005 | Structured state and conversation summary | Permanent | **Permanent invariant.** Structured state remains authoritative; no deletion is scheduled. | Plan |
| DUP-006 | Foundry preservation policy and upstream compaction | G8 | **Retain both.** Hybrid `h001-02` failed closed on irreducible compaction in all three trials, which argues against removing the Foundry preservation contract. | G8 decision |
| DUP-007 | Foundry approval events and upstream approval execution | G7 | **Retain Foundry events.** They provide host-visible diagnostics and identity semantics that upstream execution does not expose. | Gate G7 |
| DUP-008 | Selected provider lane and complete bundle | G8 | **Retain both lanes.** Each retains a distinct supported scenario, and the comparison did not test lane consolidation. | G8 decision |

## Outstanding deletions

None. No entry is scheduled for deletion in this release.

## Reversal criteria

Deletion of any retained overlap requires all of the following, per the approved
G8 decision:

1. a new case-set version with a larger case population;
2. workload-specific parity evidence for the surface being removed; and
3. published migration guidance.

The G8 comparison satisfied none of these, so no removal is release-bound.

## Disposition

**PASS.** Every ledger entry has an explicit release-bound disposition, and no
entry is left undecided.
