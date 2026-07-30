# Harness Comparison Publication

**Case set:** `harness-001` `v1.0`

**Authoritative workflow run:** [30513567405](https://github.com/ncosentino/foundry/actions/runs/30513567405)

**Source commit:** `2ac9d32cdf31d17e2fb937ffca567562a6d069c5`

**Provider:** official GitHub Copilot SDK

**Model:** `gpt-5-mini`

**Billing path:** GitHub Copilot Enterprise

**Runner profile:** PitCrew `foundry-ci`

**Run state:** `Completed`

Human review status: `HumanReviewed`

The complete workflow bundle is retained unchanged in `run-30513567405/`; this
document is a human-readable index over that immutable evidence.

## Immutable inputs and execution

| Field | Value |
|---|---|
| Manifest SHA-256 | `ae3fb57aaba55865047fc4b83ccf08f071f706047de127f3498a63f90a60de48` |
| Analysis plan SHA-256 | `fd75e42c576c78b16bd13c06bf51db889963ce99a29bbab4028fc3ef93de2263` |
| Pricing table SHA-256 | `272dea841b50891966df3a1cba9e6b7126b407cc2ebf7e7e90ef4595194467ea` |
| Judge manifest SHA-256 | `8df3ee0afcf8bf856e7ba14ed7358bb0a912f56d002b6ac783609bd57e156dc4` |
| Global / batch / arm / bootstrap seeds | `137` / `104729` / `130363` / `155921` |
| Complete paired batches | 24 / 24 |
| Scheduled arm trials | 72 / 72 |
| Operational attempts | 72 |
| Provider requests | 164 |
| Minimum request interval | 4,000 ms |
| Estimated reservation accounting | USD 3.28 |
| Bundle checksum entries | 307 |

A two-turn declaration-only provider probe passed before the scheduler started.
It required Copilot to return the expected tool call and then reproduce an
undisclosed external tool result across a stateless transcript boundary.

## Excluded runs

These runs are explicitly excluded and are not pooled with this evidence:

| Run | Reason |
|---|---|
| `30270567078` | Unpaced GitHub Models driver defect; HTTP 429 handling did not match the frozen retry policy. |
| `30273935931` | Completed historical GitHub Models evidence produced through an unapproved provider and billing path. |
| `30400286731` | Legacy raw Copilot token exchange returned HTTP 404 before inference; no arm output existed. |
| `30511810798` | The original probe disclosed its expected final token and could not prove external tool-result replay; the scheduler never started. |

## Primary deterministic completion

| X minus Y | Difference | 95% paired interval | Discordant | Exact McNemar |
|---|---:|---:|---:|---:|
| Plain Harness minus Iterative | +0.125 | [-0.215, 0.471] | 1 | 1.0 |
| Hybrid minus Iterative | 0.000 | [-0.375, 0.375] | 2 | 1.0 |
| Hybrid minus Plain Harness | -0.125 | [-0.471, 0.215] | 1 | 1.0 |

Every completion interval includes zero. Every contrast is underpowered.

Case-majority completion:

- Iterative: 7 / 8;
- Plain Harness: 8 / 8; and
- Hybrid: 7 / 8.

Item executions succeeded 21/24 for Iterative, 24/24 for Plain Harness, and
21/24 for Hybrid.

## Secondary deterministic dimensions

All reported paired intervals include zero. Each denominator contains only the
cases whose frozen manifest declares that dimension, and every scheduled failure
is retained under the pessimistic treatment.

- Continuity: every contrast difference is `0.0`.
- Artifact reuse: every contrast difference is `0.0`.
- Cancellation: every contrast difference is `0.0`.
- Context safety: Hybrid minus Iterative and Hybrid minus Plain Harness are
  `-0.333`, with interval `[-0.792, 0.291]`.
- Tool trajectory: Plain Harness minus Iterative and Hybrid minus Iterative are
  `+0.5`, with interval `[-0.273, 0.905]`.
- Termination: Plain Harness minus Iterative and Hybrid minus Iterative are
  `+0.5`, with interval `[-0.273, 0.905]`.
- Hybrid minus Plain Harness tool trajectory and termination are `0.0`.

The following systematic failures remain in their applicable
dimension-specific pessimistic denominators:

- Iterative `h001-01`: all trials reached the eight-tool-call cap.
- Hybrid `h001-02`: all trials failed closed with irreducible hybrid compaction
  above the hard limit.

## Diagnostics parity

Every contrast reports:

- comparable cases: 8;
- non-comparable cases: 0; and
- incomplete-due-to-cap cases: 0.

## Continuous evidence

Continuous comparisons retain one or two conditional paired cases. Every
continuous interval is `insufficient-sample`; no interval bounds are published.
Each continuous metric was pre-registered for two cases, not for the full
eight-case completion population.

| Contrast | Dimension | Conditional n | Mean X-Y | Pessimistic n | Pessimistic mean X-Y |
|---|---|---:|---:|---:|---:|
| Plain Harness - Iterative | Cumulative tokens | 2 | 4089.33 | 2 | 4089.33 |
| Plain Harness - Iterative | Peak tokens | 2 | 1267.67 | 2 | 1267.67 |
| Plain Harness - Iterative | Attributed cost | 2 | 2491.50 | 2 | 2491.50 |
| Plain Harness - Iterative | Latency ms | 2 | 19157.58 | 2 | 19157.58 |
| Hybrid - Iterative | Cumulative tokens | 1 | 1156.67 | 2 | 35416.67 |
| Hybrid - Iterative | Peak tokens | 1 | 277.33 | 2 | 2527.33 |
| Hybrid - Iterative | Attributed cost | 2 | 501.00 | 2 | 501.00 |
| Hybrid - Iterative | Latency ms | 2 | 11661.19 | 2 | 11661.19 |
| Hybrid - Plain Harness | Cumulative tokens | 1 | -1081.00 | 2 | 31327.33 |
| Hybrid - Plain Harness | Peak tokens | 1 | -1813.67 | 2 | 1259.67 |
| Hybrid - Plain Harness | Attributed cost | 2 | -1990.50 | 2 | -1990.50 |
| Hybrid - Plain Harness | Latency ms | 2 | -7496.39 | 2 | -7496.39 |

No efficiency, token, latency, or cost ranking is supported.

## Advisory judge evidence

Judge evidence is `UNCALIBRATED`.

- Human-attested eligible calibration items: 7;
- attested by: `@ncosentino`;
- observed judge agreement: not yet measured;
- calibration state: `UNCALIBRATED`; and
- usable for arm ranking: `false`.

| Calibration artifact | SHA-256 |
|---|---|
| Judge manifest | `8df3ee0afcf8bf856e7ba14ed7358bb0a912f56d002b6ac783609bd57e156dc4` |
| Calibration manifest | `a02baa5ad18a035e5a6a2acbdbe9c75a2e727c63cf749cec50218c49877a2ae2` |
| Human-attested held-out labels | `98323a323b03a20c4e9f0bd6cc42a84d33d46f67c84b2dfc6c42ae8ae610e776` |

Judge execution remains omitted. Human labels establish calibration ground
truth; they do not establish judge agreement. No judge preference or
disagreement supports any arm result.

## Provider transcript limitation

The official Copilot SDK exposes no raw tool-capable chat-completion API, so the
adapter supplies each arm's complete transcript as a JSON meta-prompt. That
treatment is identical across arms and preserves arm-produced transcript
differences, but these results are Copilot SDK transcript-replay evidence and
must not be pooled with the excluded GitHub Models runs.

## Human-review signature block

The signed block is `run-30513567405-human-review.json`. It binds review to the
SHA-256 digest of `run-30513567405/checksums.sha256`:

`f76e31d9424e15b8d0b56b12b75a1020626284adb361c609a659a07dfa370d29`

Recorded review:

- reviewer: `@ncosentino`;
- reviewed at: `2026-07-30T14:32:59Z`;
- deterministic-anchor acknowledgment: recorded;
- paired-uncertainty acknowledgment: recorded;
- diagnostics-parity acknowledgment: recorded;
- judge-calibration/omission acknowledgment: recorded;
- truncation/cap acknowledgment: recorded; and
- retention recommendation: `RetainAllPendingStrongerEvidence`.

The reviewer recommends retaining the iterative loop, plain Harness, and the
experimental hybrid profile pending stronger evidence, with no default change
and no removal. The formal decision artifact is
`specs/001-maf-harness-first-class/evidence/retention-decisions.md`.
