# Gate G10 Decision — Reconciliation and Specification Cleanup

## Decision

**PASS for the cumulative G10 reconciliation slice.**

G10 compares delivered behavior with the original plan, verifies documentation
accuracy, separates follow-up scope, and records the approved specification
retention decision:

1. an implementation-versus-plan variance report;
2. a documentation-versus-delivery parity audit;
3. post-MVP follow-ups filed as GitHub issues; and
4. a human-approved per-artifact retention decision.

## Evidence identity

| Task | Artifact | Disposition |
|---|---|---|
| T127 | `evidence/implementation-vs-plan.md` | Complete |
| T128 | `evidence/documentation-vs-delivery.md` | Complete |
| T129 | `evidence/post-mvp-follow-ups.md`, issues #142-#144 | Complete |
| T130 | `evidence/specification-retention-decision.md` | Approved by `@ncosentino` |
| T131 | Separate cleanup pull request | Executes after this gate |

Base: `bfc3acc300673f4019948d6d829c447c97000706` on `main`.

## Gate criteria

### Delivered behavior and public APIs map back to the plan

`evidence/implementation-vs-plan.md` maps every delivered package, public API
surface, capability profile, and overlap disposition to the plan. One
non-critical variance was found and fixed: the Testing package description
omitted the Harness scenario surface. No critical variance was found, so no
architecture regroup was triggered.

The absence of a new analyzer and the absence of a shell capability are
plan-conforming outcomes, not gaps: the plan made analyzer additions conditional
on demonstrated static value, and FR-060 requires shell to stay a separate
opt-in package.

### Documentation describes the delivered system

`evidence/documentation-vs-delivery.md` confirms the delivered documentation
describes what shipped: prerelease rather than stable, retained rather than
replaced, internal and experimental where that is true, and explicit that
`InternalsVisibleTo` is not a security boundary. One non-critical variance was
found and fixed.

### Unresolved non-critical scope is tracked outside the MVP

Three follow-ups are filed and linked to root issue #16:

- #142 strengthen the hosted comparison case population;
- #143 measure observed judge agreement; and
- #144 resolve AOT-unverified capability dispositions.

Six review findings were fixed inside their own gate rather than deferred; they
are listed in `evidence/post-mvp-follow-ups.md` so the distinction between
"fixed" and "deferred" is explicit.

### ADRs, changelog, and durable migration guidance remain available

The approved retention decision preserves `docs/adr/`, `CHANGELOG.md`, delivered
guidance, and the signed comparison evidence under `artifacts/eval/`. The
overlap retention decision is promoted to a durable ADR so it survives the
removal of the feature specification folder.

### Feature specification artifacts are removed only after an approved decision

`evidence/specification-retention-decision.md` records the reviewer, the date,
the verbatim rationale, the interpretation of "archive" as removal from the
working tree, the disposition of all ten inbound references, and one disclosed
consequence.

## Disclosed consequence of the approved cleanup

`artifacts/eval/reports/harness-001/run-30513567405-publication.md` is signed and
checksum-bound and therefore must not be edited. Its text names the retention
decision at its original specification path. After cleanup that reference
resolves through git history rather than `main`. ADR-0010 records the relocation
so the decision remains discoverable from the signed artifact.

This is recorded as a known consequence rather than treated as a defect, because
the alternative — editing the immutable artifact — would invalidate the human
signature bound to it.

## Deferred work

None within this program. Remaining opportunities are tracked as post-MVP
issues.
