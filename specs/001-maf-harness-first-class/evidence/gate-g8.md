# Gate G8 Decision — Hosted Comparative Evidence and Retention

## Decision

**PASS for the cumulative G8 hosted-comparison slice.**

G8 delivers a pre-registered, executed, published, and human-signed comparison
of the three execution modes without asserting any conclusion the evidence does
not support:

1. a frozen `harness-001` `v1.0` analysis protocol;
2. deterministic evaluators and paired evidence primitives;
3. pinned deterministic case references;
4. three-arm experiments, reporting, and advisory judge machinery;
5. a capped, non-gating, dispatch-only hosted workflow;
6. one completed comparison through the approved provider path;
7. an immutable published artifact with a real human signature; and
8. an approved overlap retention decision.

No default arm is selected. No overlap is removed. No accepted ADR is
superseded.

## Evidence identity

| Slice | Issue | PR / commit | Disposition |
|---|---|---|---|
| Pre-registered protocol | #55 | #120 / `b26ea4ba` | Merged |
| Evaluator and paired-evidence tests | #56 | #122 / `adc1a84c` | Merged |
| Judge calibration assets | #57 | #123 / `d499727a`, #129 | Merged; human-attested |
| Case loading and deterministic evaluators | #58 | #122, #124 / `4bfc2a25` | Merged |
| Experiments, reporters, advisory judges | #59 | #125 / `efd6b4f2` | Merged |
| Capped non-gating workflow | #60 | #126 / `9f6e719b` | Merged |
| Official Copilot SDK provider | #61 | #130 / `f187e7cc` | Merged |
| Hardened result-replay probe | #61 | #137 / `2ac9d32c` | Merged |
| Approved execution, publication, retention | #61, #62, #63 | #138 / `7c7c6a50` | Merged |

Final G8 integration head: `7c7c6a50a9c363875346a81b3a96ff5424d38141` on
`harness/g8-integration`.

All local .NET commands use
`$env:NUGET_PACKAGES='G:\dev\caches\nuget\packages'`.

## Approved execution path

The authoritative run is workflow
[30513567405](https://github.com/ncosentino/foundry/actions/runs/30513567405)
at source commit `2ac9d32cdf31d17e2fb937ffca567562a6d069c5`.

It satisfies every required control:

- dedicated PitCrew `foundry-ci` runner profile;
- official `GitHub.Copilot.SDK` runtime with the workflow-scoped token;
- GitHub Copilot Enterprise billing, explicitly confirmed;
- `contents: read` and `copilot-requests: write` only;
- no GitHub Models endpoint, permission, or hosted-runner fallback; and
- a fail-closed two-turn provider probe before any batch was scheduled.

The probe requires Copilot to return the expected declaration-only tool call and
then reproduce an undisclosed external tool result across a stateless transcript
boundary. It rejects repeated tool calls and mixed prose separately.

| Measure | Value |
|---|---:|
| Complete paired batches | 24 / 24 |
| Scheduled arm trials | 72 / 72 |
| Operational attempts | 72 |
| Provider requests | 164 |
| Estimated reservation accounting | USD 3.28 |
| Fully scheduled cases | 8 / 8 |
| Bundle checksum entries | 307 |

## Excluded runs

| Run | Reason |
|---|---|
| `30270567078` | Unpaced GitHub Models driver defect; HTTP 429 handling did not match the frozen retry policy. |
| `30273935931` | Completed historical GitHub Models evidence produced through an unapproved provider and billing path. |
| `30400286731` | Legacy raw Copilot token exchange returned HTTP 404 before inference; no arm output existed. |
| `30511810798` | The original probe disclosed its expected final token and could not prove external tool-result replay; the scheduler never started. |

Each exclusion is recorded in the repository and none is pooled with the
approved evidence.

## Gate criteria

### Stochastic evidence is not the sole automated gate

`Harness Evaluation` and the registered `harness-evaluation-dispatch` bridge are
absent from required branch protection. Required checks remain `build-test-pack`,
`docs`, and `aot`. Evidence is recorded in `evidence/hosted-eval-gate.md`.

### Reports include operational definitions and uncertainty

The publication reports paired differences with 95% intervals, exact McNemar
probabilities, discordant counts, diagnostics parity, pessimistic and
inconclusive binary treatments, and conditional plus pessimistic continuous
sensitivity.

| X minus Y | Completion difference | 95% paired interval |
|---|---:|---:|
| Plain Harness minus Iterative | +0.125 | [-0.215, 0.471] |
| Hybrid minus Iterative | 0.000 | [-0.375, 0.375] |
| Hybrid minus Plain Harness | -0.125 | [-0.471, 0.215] |

Every completion interval includes zero. Every secondary binary interval
includes zero. Every continuous comparison is `insufficient-sample`.

### Every proposed removal has parity evidence and migration guidance

No removal is proposed, so this criterion is satisfied vacuously. The approved
decision is `RetainAllPendingStrongerEvidence` for DUP-001, DUP-002, DUP-006,
and DUP-008.

## Human review

`run-30513567405-human-review.json` is signed by `@ncosentino`, reviewed
`2026-07-30T14:32:59Z`, and bound to bundle checksum
`f76e31d9424e15b8d0b56b12b75a1020626284adb361c609a659a07dfa370d29`.

The reviewer acknowledged deterministic anchors, paired uncertainty, diagnostics
parity, judge omission, and cap/truncation treatment. No identity,
acknowledgment, recommendation, or signature was fabricated at any point; the
publication remained `PendingHumanReview` until the reviewer acted.

## Judge disposition

Judge evidence remains `UNCALIBRATED` and unusable for arm ranking.

- Human-attested eligible calibration items: 7, attested by `@ncosentino`.
- Observed judge agreement: not measured.
- `UsableForArmRanking`: `false`.

Human labels establish calibration ground truth; they do not establish judge
agreement. Judge execution was therefore omitted from the comparison.

## Accepted limitations

- The official Copilot SDK exposes no raw tool-capable chat-completion API, so
  each arm's transcript is supplied as a JSON meta-prompt. The treatment is
  identical across arms, but results are Copilot SDK transcript-replay evidence.
- Eight cases cannot separate the arms; every contrast is underpowered.
- Continuous evidence retains at most two paired cases per dimension.
- Iterative `h001-01` exhausted the eight-tool-call cap in all trials, and
  Hybrid `h001-02` failed closed on irreducible compaction in all trials. These
  are recorded limitations, not superiority evidence for any arm.
- `retentionEligible: true` means only that the pre-registered six-case
  scheduling minimum was met.

## Deferred work

- Shell composition tests, cross-artifact analysis, public API review, and the
  duplication ledger remain G9.
- Release notes and migration guidance remain G9.
- Implementation-versus-plan reconciliation and specification cleanup remain
  G10.
