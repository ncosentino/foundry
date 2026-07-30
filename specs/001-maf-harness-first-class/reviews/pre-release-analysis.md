# Pre-Release Cross-Artifact Analysis

**Task:** T123  
**Analyzed:** 2026-07-30  
**Scope:** the implemented Harness profile as delivered through Gate G8.

This analysis re-runs the Spec Kit cross-artifact consistency check against the
implemented system rather than the intended system. `reviews/final-analysis.md`
closed G0 before implementation; this document supersedes it as the pre-release
record.

## Artifacts checked

- `spec.md`, `plan.md`, `tasks.md`, `traceability.md`, `constitution.md`
- `contracts/harness-profile.md`, `contracts/hybrid-context.md`,
  `contracts/workspace-file-store.md`, `contracts/diagnostics-events.md`,
  `contracts/evaluation-evidence.md`
- `evidence/gate-g6.md`, `evidence/gate-g7.md`, `evidence/gate-g8.md`,
  `evidence/hosted-eval-gate.md`, `evidence/retention-decisions.md`,
  `evidence/api-review.md`, `evidence/duplication-ledger.md`
- delivered source, packages, and `docs/maf-harness.md`

## Findings

| ID | Severity | Finding | Resolution |
|---|---|---|---|
| P1 | High | FR-060 required shell to be a separate opt-in, manually composed capability with no `HarnessAgentOptions` shell property, but no test enforced it and the documentation did not state it. | Resolved in this gate: `HarnessShellCompositionTests` enforces the package boundary, capability absence, and documentation claim; `docs/maf-harness.md` gained a dedicated section. |
| P2 | Medium | The Testing package promoted `ScenarioRunResult` with an undocumented positional parameter list, violating the constitution's XML documentation rule. | Resolved in this gate: every positional parameter now has a `<param>` element. |
| P3 | Medium | `plan.md` listed the temporary duplication ledger but no artifact recorded release-bound dispositions. | Resolved in this gate by `evidence/duplication-ledger.md`, which binds DUP-001/002/006/008 to the signed G8 decision. |
| P4 | Low | The G8 workflow and dispatch bridge still used a floating SDK setup after the repository adopted the exact-SDK runner contract on `main`. | Resolved before the G8 gate merged: both evaluation jobs now use `./.github/actions/setup-dotnet`, and `scripts/test-runner-profile.ps1` asserts the fourth usage. |

No critical finding was identified.

## Consistency confirmations

- **Requirement coverage.** FR-034, FR-035, FR-044, FR-046, FR-057, FR-058, and
  FR-059 map to G8 tasks T095-T121, all of which are complete and evidenced by
  `evidence/gate-g8.md`. FR-060 maps to T122, which is complete.
- **Success criteria.** SC-010 and SC-015 were explicitly deferred from G7 to G8
  and are now satisfied by the approved run, its publication, and its signature.
  SC-011 remains open by design and belongs to G10.
- **Gate ordering.** The delivered branch topology follows the declared
  `G8 -> G9 -> G10` dependency. G8 merged to `main` through PR #139 before G9
  work started.
- **Constitution.** Neutral package boundaries, evidence-gated API evolution,
  hybrid conversation/workspace authority, deterministic testing, explicit API
  discipline, and AOT/source-generation preservation all hold. No compatibility
  shim was added for a former alpha package identity.
- **Contracts versus delivery.** `contracts/evaluation-evidence.md` requires
  operational definitions, uncertainty, judge calibration records, and a
  retention decision artifact. All four exist and are checksum-bound.
- **Stability language.** The optional bundle and Testing scenario APIs are
  described as prerelease in `README.md`, `docs/maf-harness.md`, and
  `docs/iterative-agent-loop.md`, consistent with their alpha package versions.

## Known divergences accepted for release

These are recorded rather than fixed, and each is already disclosed in delivered
documentation or gate evidence:

- Selected-provider composition stays internal; `HarnessHybridApp` uses
  `InternalsVisibleTo`, which is not a security boundary because the assemblies
  are unsigned.
- Experimental hybrid compaction stays internal and AOT-unverified.
- The hosted comparison is underpowered at eight cases and supports no default
  or removal decision.
- The Copilot SDK exposes no raw tool-capable chat-completion API, so hosted
  evaluation supplies transcripts as an identical-per-arm JSON meta-prompt.

## Disposition

**PASS.** No critical cross-artifact inconsistency remains. Every High and
Medium finding was resolved inside this gate rather than deferred to G10.
