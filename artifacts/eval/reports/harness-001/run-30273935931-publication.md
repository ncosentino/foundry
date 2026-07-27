# Harness Comparison Publication

**Case set:** `harness-001` `v1.0`

**Authoritative workflow run:** [30273935931](https://github.com/ncosentino/foundry/actions/runs/30273935931)

**Source commit:** `8fd8f427f1d9c64cc5aeee318c47a3f6823f80c3`

**Model:** `openai/gpt-4.1-mini`

**Run state:** `Completed`

Human review status: `PendingHumanReview`

No retention or removal decision is published by this artifact. The complete
workflow bundle is retained in `run-30273935931/`; this document is a human-
readable index over that immutable evidence.

## Immutable inputs and execution

| Field | Value |
|---|---|
| Manifest SHA-256 | `ae3fb57aaba55865047fc4b83ccf08f071f706047de127f3498a63f90a60de48` |
| Analysis plan SHA-256 | `fd75e42c576c78b16bd13c06bf51db889963ce99a29bbab4028fc3ef93de2263` |
| Pricing table SHA-256 | `74fd11bee9794560f14ffd6b17faaac88bfce20695af860ab5c829f7489ce0a7` |
| Judge manifest SHA-256 | `b9915f357bfcde9b4635d9927dfada051e962ea73826797b6487380ec409cfd9` |
| Global / batch / arm / bootstrap seeds | `137` / `104729` / `130363` / `155921` |
| Complete paired batches | 24 / 24 |
| Scheduled arm trials | 72 / 72 |
| Operational attempts | 72 |
| Provider requests | 180 |
| Minimum request interval | 4,000 ms |
| Estimated cost | USD 3.60 |
| Workflow duration | 18m 20s |
| Bundle checksum entries | 323 |

The earlier workflow run `30270567078` is excluded and not pooled. Its unpaced
driver produced HTTP 429 failures without the pre-registered transient retry
treatment. See `run-30270567078-exclusion.md`.

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

## Secondary deterministic dimensions

All reported paired intervals include zero.

- Continuity: every contrast difference is `0.0`.
- Artifact reuse: every contrast difference is `0.0`.
- Cancellation: every contrast difference is `0.0`.
- Context safety: Hybrid minus Iterative and Hybrid minus Plain Harness are
  `-0.333`, with interval `[-0.792, 0.291]`.
- Tool trajectory: Plain Harness minus Iterative and Hybrid minus Iterative are
  `+0.5`, with interval `[-0.273, 0.905]`.
- Termination: Plain Harness minus Iterative and Hybrid minus Iterative are
  `+0.5`, with interval `[-0.273, 0.905]`.

Systematic failures retained in pessimistic denominators:

- Iterative `h001-01`: all trials reached the eight-tool-call cap.
- Hybrid `h001-02`: all trials failed closed with irreducible hybrid compaction
  above the hard limit.
- Plain Harness `h001-08`: one trial reached the eight-request cap; its other two
  trials passed and the case majority succeeded.

## Diagnostics parity

Every contrast reports:

- comparable cases: 8;
- non-comparable cases: 0; and
- incomplete-due-to-cap cases: 0.

## Continuous evidence

Continuous comparisons retain zero to two conditional paired cases. Every
continuous interval is `insufficient-sample`; no interval bounds are published.

| Contrast | Dimension | Conditional n | Mean X-Y | Pessimistic n | Pessimistic mean X-Y |
|---|---|---:|---:|---:|---:|
| Plain Harness - Iterative | Cumulative tokens | 2 | 2812.50 | 2 | 2812.50 |
| Plain Harness - Iterative | Peak tokens | 1 | 63.00 | 2 | 1659.83 |
| Plain Harness - Iterative | Attributed cost | 2 | 1701.50 | 2 | 1701.50 |
| Plain Harness - Iterative | Latency ms | 1 | 23421.75 | 2 | 56322.56 |
| Hybrid - Iterative | Cumulative tokens | 1 | 712.33 | 2 | 38008.17 |
| Hybrid - Iterative | Peak tokens | 1 | 211.33 | 2 | 2757.67 |
| Hybrid - Iterative | Attributed cost | 2 | 162.17 | 2 | 162.17 |
| Hybrid - Iterative | Latency ms | 2 | 28652.39 | 2 | 28652.39 |
| Hybrid - Plain Harness | Cumulative tokens | 1 | -153.67 | 2 | 35195.67 |
| Hybrid - Plain Harness | Peak tokens | 0 | n/a | 2 | 1097.83 |
| Hybrid - Plain Harness | Attributed cost | 2 | -1539.33 | 2 | -1539.33 |
| Hybrid - Plain Harness | Latency ms | 1 | -1697.40 | 2 | -27670.16 |

No efficiency, token, latency, or cost ranking is supported.

## Advisory judge evidence

Judge evidence is `UNCALIBRATED`.

- Human-attested eligible calibration items: 0;
- provisional calibration items: 7; and
- usable for arm ranking: `false`.

Judge execution was omitted. No judge preference or disagreement is used to
support any result.

## Human-review signature block

The signable block is `run-30273935931-human-review.json`. It binds review to the
SHA-256 digest of `run-30273935931/checksums.sha256`:

`4191c63f2bd86afe7317e8e3fabcbd4d1f9b779b295d3110daa159488913b659`

The following remain unset until a human reviewer acts:

- reviewer identity and review date;
- deterministic-anchor acknowledgment;
- paired-uncertainty acknowledgment;
- diagnostics-parity acknowledgment;
- judge-calibration/omission acknowledgment;
- truncation/cap acknowledgment;
- retention recommendation; and
- signature.

Until those fields are completed, this publication remains advisory and
unsigned.
