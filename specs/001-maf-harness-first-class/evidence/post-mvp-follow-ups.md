# Post-MVP Follow-Up Work

**Task:** T129  
**Filed:** 2026-07-30  
**Root implementation issue:** [#16](https://github.com/ncosentino/foundry/issues/16)

Every accepted non-critical deviation or opportunity from the G8-G10 reviews is
tracked outside the MVP scope. Nothing below blocks the delivered profile.

## Filed follow-ups

| Issue | Title | Origin |
|---|---|---|
| [#142](https://github.com/ncosentino/foundry/issues/142) | Strengthen the hosted comparison case population | Gate G8: every contrast underpowered; continuous dimensions insufficient-sample |
| [#143](https://github.com/ncosentino/foundry/issues/143) | Measure observed judge agreement | Gate G8: human labels exist, but agreement was never measured, so the judge stays `UNCALIBRATED` |
| [#144](https://github.com/ncosentino/foundry/issues/144) | Resolve AOT-unverified capability dispositions | Gate G7: several capabilities are runtime-tested but AOT-unverified |

## Pre-existing related issues

These were filed earlier in the program and remain open; they are not re-filed
here:

- [#73](https://github.com/ncosentino/foundry/issues/73) — MAF Harness
  compaction skips intermediate local-history tool rounds.
- [#78](https://github.com/ncosentino/foundry/issues/78) — Harness follow-up:
  streaming tool binding and rejection diagnostics.

## Deviations fixed inside their gate rather than deferred

For completeness, these review findings were **not** deferred:

| Finding | Gate | Resolution |
|---|---|---|
| FR-060 had no enforcing test or documentation | G9 | `HarnessShellCompositionTests` plus a documentation section |
| Shell scan missed shared MSBuild files | G9 | Scan extended to `Directory.Build.*` and `*.targets`, with a coverage test |
| `ScenarioRunResult` positional parameters undocumented | G9 | `<param>` documentation added |
| Duplication ledger lacked release-bound dispositions | G9 | `evidence/duplication-ledger.md` |
| Evaluation jobs used a floating SDK setup | G8 | Routed through the exact-SDK action |
| Testing package description omitted the Harness scenario surface | G10 | Description corrected |

## Disposition

**PASS.** Every accepted non-critical opportunity is tracked in GitHub and
linked to the root implementation issue. No opportunity is recorded only in
prose.
