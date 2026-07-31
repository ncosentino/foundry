# Specification Retention Decision

**Task:** T130  
**Decision status:** Approved  
**Reviewer:** `@ncosentino`  
**Reviewed:** 2026-07-30  
**Decision:** `ArchiveAll`

## Approved decision

Remove the entire `specs/001-maf-harness-first-class/` feature specification
folder from the working tree. Preserve ADRs, `CHANGELOG.md`, and durable
migration guidance.

Reviewer rationale, recorded verbatim:

> history is for github issues and commit messages, not for historic artifacts
> to linger in my codebase and pollute context

## Interpretation

"Archive" here means **removal from the working tree**, not relocation to an
`archive/` subfolder. A subfolder would still occupy the active codebase and
would not satisfy the stated intent.

The historical record is preserved by:

- **git history**, which retains every version of every deleted file;
- **GitHub issues** #16 and #24-#69, which narrate the program gate by gate; and
- **immutable issue links** that already pin `plan.md`, `tasks.md`, and
  `traceability.md` to commit `06b04e6daec39c4a1fb57c3c94e7189fd7803ea0`. Those
  links resolve from git history and do **not** break when the files leave
  `main`.

## Load-bearing artifacts and their disposition

Deleting the folder is not a pure removal: ten references outside it point into
it. Each is resolved rather than left dangling.

| Referencing artifact | Points at | Disposition |
|---|---|---|
| `docs/adr/adr-0005..0008` | `gate-g2/g4/g5/g6.md`, feasibility evidence | Citations rewritten to state the decision inline; ADRs preserved |
| `src/.../Harness/Context/HarnessArtifactIdentity.cs`, `HarnessArtifactReference.cs` | `harness-lifecycle-feasibility.md` | Path citations removed; the constraint is stated in the XML documentation itself |
| `src/.../Harness/Workspace/WorkspaceAgentFileStore.cs` | `workspace-identity-feasibility.md` | Same, including the runtime exception message |
| `src/NexusLabs.Foundry.Evaluation.Tests/.../HarnessHostedWorkflowTests.cs` | `hosted-eval-gate.md` | Retargeted to assert the non-gating guarantees directly against the workflow files |
| `src/NexusLabs.Foundry.Evaluation.Tests/.../HarnessPublishedComparisonArtifactTests.cs` | `retention-decisions.md` | Retargeted to the new ADR |
| `docs/maf-harness.md` | `retention-decisions.md` | Link retargeted to the new ADR |

The overlap retention decision is promoted to a durable
`docs/adr/adr-0010-harness-execution-mode-retention.md`, because it is a real
architectural decision rather than a planning artifact and the user's decision
explicitly preserves ADRs.

## Disclosed consequence

`artifacts/eval/reports/harness-001/run-30513567405-publication.md` is a signed,
checksum-bound artifact. Its recorded text names
`specs/001-maf-harness-first-class/evidence/retention-decisions.md` as the
formal decision artifact.

That file **must not be edited**, because editing it would invalidate the
publication manifest hash and the human signature bound to it. After cleanup,
that one path reference inside the immutable publication becomes historical: it
resolves through git history rather than through `main`.

This is disclosed rather than silently accepted. ADR-0010 records the
relocation so an auditor following the signed artifact can find the decision.
The signed human-review JSON, the publication manifest, and the checksum-verified
run bundle are all unaffected and remain in `artifacts/`.

## Not deleted

- `docs/adr/` — every accepted ADR, including the new ADR-0010.
- `CHANGELOG.md` — release notes and migration guidance.
- `docs/maf-harness.md`, `docs/iterative-agent-loop.md` — delivered guidance.
- `artifacts/eval/` — the frozen case set and the signed, checksum-verified
  comparison evidence.
- GitHub issues and their immutable pinned links.

## Execution

T131 executes this decision in a separate cleanup pull request, per the plan's
requirement that cleanup not be mixed with the reconciliation evidence.
