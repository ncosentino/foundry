# Pre-Registered Hosted Analysis Protocol — harness-001 v1.0

## Status and freeze rule

**Status: PRE-REGISTERED.**

This protocol is frozen before evaluator implementation, case execution, or
result inspection. Once merged, any change to the arm definitions, hosted case
IDs, trial count, retry semantics, metrics, exclusions, uncertainty methods,
caps, or judge policy requires a new case-set version.

The hosted run is advisory and non-gating. It cannot by itself merge code,
enable a default, remove an execution path, or satisfy a retention decision.

## Decision questions

### Primary question

On the eight hosted `harness-001` v1.0 cases, under identical pinned inputs,
what is the paired case-level difference in deterministic task-completion
success among:

- **A — iterative**: current Foundry workspace-driven iterative execution;
- **B — plain-harness**: plain MAF Harness with explicit upstream compaction;
- **C — hybrid**: Harness execution with Foundry workspace/offload and
  experimental hybrid context.

The pre-declared pairwise contrasts are:

1. B minus A;
2. C minus A;
3. C minus B.

### Secondary deterministic questions

The same paired contrasts are reported for:

- conversation/decision continuity;
- context-window safety and compaction validity;
- artifact production, reuse, and rehydration;
- tool trajectory and tool errors;
- cancellation and timeout behavior;
- termination appropriateness;
- diagnostics-schema completeness and parity;
- cumulative and peak token usage;
- artifact/context cost attribution; and
- end-to-end latency.

### Exploratory questions

Versioned LLM judges may score qualitative dimensions after deterministic
evaluation. Judge evidence is advisory, is never part of the primary analysis,
and cannot override a deterministic reference.

## Scope and prohibited conclusions

The run estimates effects only for:

- case set `harness-001` version `v1.0`;
- the pinned provider/model/build and sampling configuration;
- the package and code versions recorded in the run bundle; and
- the dimensions defined in this protocol.

The report must not claim:

- universal or general arm superiority;
- transfer to untested providers, models, or workloads;
- causal effects beyond the paired implementation comparison;
- significance or product value from a judge-only dimension;
- parity for a dimension whose diagnostics schema is not comparable;
- that retries are independent trials; or
- that a point estimate without uncertainty is comparative evidence.

## Immutable inputs

Every run records and hashes:

- case-set ID/version and canonical manifest JSON;
- case ID/version and deterministic reference files;
- arm ID/version;
- provider, model, model build/date, and endpoint class;
- temperature, top-p, max output, stop conditions, penalties, and tool policy;
- controlling/system instructions;
- tool names, schemas, and implementation versions;
- initial workspace fixture;
- token/context budget, iteration/tool-round caps, attempt timeout, and
  cancellation policy;
- capability profile;
- package graph and git commit;
- global run seed, per-case/trial seed derivation, arm-order seed, and bootstrap
  seed;
- evaluator IDs/versions;
- judge model/version, rubric hashes, and calibration-set hash; and
- workflow run ID plus artifact checksums.

Pinning does not make provider output deterministic. Repeated trials and paired
uncertainty quantify the remaining stochastic variation.

## Hosted cases, trials, and caps

### Case set

The hosted comparison contains exactly eight hosted cases:

`h001-01` through `h001-08`.

Each case must have:

- one deterministic completion predicate;
- deterministic dimension references for every dimension used in a decision;
- a versioned initial workspace;
- fixed prompts/tools/budgets; and
- no development-case label.

Development cases may exist in the manifest but are excluded from this run.

### Trials

Each hosted case runs exactly three independent trials per arm:

```text
8 cases × 3 trials × 3 arms = 72 planned agent runs
```

Three trials provide a small within-case stochastic sample and an unambiguous
binary majority while keeping the non-gating workflow capped. The case, not the
trial, is the population unit. This design is intentionally underpowered for
small differences and must be reported as such.

### Binding workflow caps

- planned agent runs: 72;
- maximum attempts, including retries: 144;
- maximum provider requests across all attempts: 432;
- maximum wall clock: 60 minutes;
- maximum estimated provider cost: USD 25;
- maximum attempt duration: 120 seconds;
- maximum output tokens per provider request: 2,000; and
- maximum concurrent agent runs: 3.

The first reached cap stops new scheduling. In-flight attempts may finish
within their attempt timeout. A capped run is `TRUNCATED`, retains all captured
evidence, and cannot be selectively extended after outcome inspection.

## Pairing and trial ordering

For every case/trial index, all three arms use the same:

- case inputs and workspace fixture;
- controlling instructions and tools;
- model/sampling configuration;
- derived case/trial seed, where the provider accepts a seed; and
- budgets and timeout.

Arm execution order is deterministically shuffled per case/trial from the
pinned arm-order seed. Pairing is analyzed at the case level; aligned trial
indices are a variance-reduction aid, not an independence claim.

## Retry versus independent-trial semantics

An **independent trial** is one scheduled case/arm replicate with its own
derived trial seed. It contributes at most one terminal trial outcome.

A **retry** is an operational re-attempt of the same trial after:

- transient execution failure;
- provider timeout; or
- task-level cancellation caused by a retryable infrastructure condition.

Each trial permits at most one retry. Retries:

- reuse the same trial identity and seed;
- do not increase statistical sample counts;
- increment attempt counts;
- are reported separately; and
- cannot replace a terminal deterministic task failure with a new draw.

After retry exhaustion, the terminal trial status remains failure, timeout, or
cancellation.

## Unit of analysis and aggregation

The primary analysis unit is the **case**.

Within each case/arm cell:

- binary dimensions use majority outcome across the three trials;
- continuous dimensions use the trial mean as the case-level value;
- the trial median is retained as a descriptive robustness value; and
- attempt/retry counts remain operational descriptors.

Pairwise analysis uses only case-level paired values. Trial rows are never
treated as 24 independent population samples.

## Metric definitions and methods

| Metric | Deterministic reference | Case-level value | Paired estimand | Method / uncertainty | Classification |
|---|---|---|---|---|---|
| Task completion | Completion predicate | 3-trial majority | Difference in success proportion | Exact McNemar discordance evidence plus paired proportion-difference interval | Primary advisory |
| Continuity | Required decision/state references | 3-trial majority | Difference in success proportion | Same paired binary method | Secondary |
| Context safety | No-overflow and structural-validity references | 3-trial majority | Difference in success proportion | Same paired binary method | Secondary |
| Artifact reuse/rehydration | Artifact/digest/reference predicates | 3-trial majority | Difference in success proportion | Same paired binary method | Secondary |
| Tool trajectory | Required/forbidden tool sequence | 3-trial majority | Difference in success proportion | Same paired binary method | Secondary |
| Cancellation | Expected cancellation category and no success-shaped output | 3-trial majority | Difference in success proportion | Same paired binary method | Secondary |
| Termination | Expected terminal category | 3-trial majority | Difference in success proportion | Same paired binary method | Secondary |
| Diagnostics parity | Required schema fields/relationships | Pass/fail | Comparability precondition | Exact fixture comparison; no cross-arm numeric comparison when failed | Precondition |
| Cumulative tokens | Diagnostics counters | Trial mean per case | Mean paired difference | Case-level paired percentile bootstrap | Secondary |
| Peak tokens | Diagnostics counters | Trial mean per case | Mean paired difference | Case-level paired percentile bootstrap | Secondary |
| Attributed artifact/context cost | Attribution counters | Trial mean per case | Mean paired difference | Case-level paired percentile bootstrap | Secondary |
| Latency | Monotonic duration | Trial mean per case | Mean paired difference | Case-level paired percentile bootstrap | Secondary |
| Judge dimensions | Versioned rubric | Trial mean per case | Advisory mean paired difference | Descriptive bootstrap only | Exploratory |

Continuous reports also include paired median difference descriptively. No
normality assumption or unpaired test is used.

## Paired binary evidence

For each contrast and binary metric, form a paired case table:

```text
                 Y success   Y failure
X success            a           b
X failure            c           d
```

The point estimate is:

```text
delta = ((a + b) / n) - ((a + c) / n) = (b - c) / n
```

where `n = a + b + c + d`.

Report:

- `a`, `b`, `c`, `d`;
- valid paired case count;
- discordant count `b + c`;
- raw paired difference;
- exact two-sided McNemar/binomial discordance probability; and
- a two-sided 95% paired difference interval using Newcombe/MOVER with Wilson
  component intervals.

With fewer than 25 discordant cases—which is expected for this eight-case
run—the report must label the comparison underpowered. P-values are descriptive
only and are not gates.

If there are no discordant cases, report `delta = 0`, the paired interval, and
`discordant = 0`; do not claim equivalence.

## Paired continuous evidence

For each valid case pair, compute:

```text
d_i = value_X(case_i) - value_Y(case_i)
```

Report:

- valid paired case count;
- dropped/non-comparable count;
- mean and median of `d_i`;
- per-arm case-level mean/median; and
- a two-sided 95% percentile-bootstrap interval for the mean paired difference.

The bootstrap:

- resamples cases with replacement;
- uses exactly 10,000 resamples;
- uses the pinned bootstrap seed; and
- never resamples individual trials independently of their case.

If fewer than four valid case pairs remain, report descriptive values only and
mark the interval `insufficient-sample`.

## Missing, unknown, invalid, and non-comparable samples

Every trial retains an explicit `ExperimentItemStatus`.

### Binary dimensions

Primary treatment is pessimistic:

- execution failure, timeout, cancellation, missing metric, invalid metric,
  prerequisite failure, and evaluation failure count as trial failure.

Sensitivity treatment:

- unknown/unscorable trials are excluded (`Inconclusive`), with denominator
  changes reported.

Both treatments are published. Divergence is evidence.

### Continuous dimensions

Non-finite, missing, failed, timed-out, canceled, or non-comparable values are
not imputed in the primary estimate. The case pair is dropped and counted.
A pessimistic sensitivity may substitute the worst finite observed value, but
must be labeled sensitivity-only.

### Diagnostics parity

If one arm lacks the schema required for a token, cost, trajectory, or
relationship dimension, that dimension is `NON_COMPARABLE` for the affected
pair. Values from mismatched schemas are never compared.

## Failure, timeout, and cancellation reporting

The report distinguishes:

- caller cancellation of the whole hosted run;
- per-attempt timeout;
- task-level cancellation;
- execution failure;
- retry exhaustion; and
- deterministic task failure.

Whole-run caller cancellation or a binding cap produces a truncated run.
Per-trial terminal failures remain in binary denominators under the primary
pessimistic treatment.

## Multiplicity and result language

All results are advisory. No contrast has an automated rejection/acceptance
threshold.

- The task-completion contrasts are the primary reported family.
- Secondary deterministic dimensions are descriptive with intervals.
- Judge dimensions are exploratory.
- Raw exact probabilities may be reported, but no metric is promoted after
  seeing results and no "statistically significant" product claim is made.

This avoids adding multiplicity machinery that the small, non-gating run
cannot support honestly.

## Advisory judges and calibration

Judges run only on captured/replayed artifacts after agent execution.

Before judge results are published:

- rubrics are versioned and hashed;
- a held-out human-labeled calibration set is scored;
- binary/nominal dimensions report agreement and Cohen's kappa;
- ordinal dimensions report weighted agreement;
- calibration denominators and confusion counts are retained;
- a judge below kappa 0.60 is labeled `UNCALIBRATED`;
- uncalibrated results cannot rank arms, even advisorially.

Bias fixtures require:

- both pair presentation orders;
- position-flip rate;
- verbosity/length correlation;
- style-only perturbations with unchanged deterministic content; and
- deterministic-versus-judge disagreement reporting.

Order-inconsistent pairwise judgments are reported as disagreement, not forced
into a winner. When judge and deterministic reference conflict, the
deterministic reference governs the decision dimension.

The judge model should be from a different model family than the generator
when hosted credentials make that feasible. If not, the shared-family
limitation is explicit.

## Exclusions fixed before execution

Excluded from the hosted analysis:

- any manifest case with `developmentCase = true`;
- any case outside `h001-01` through `h001-08`;
- any case missing its deterministic completion predicate;
- any dimension missing deterministic reference evidence; and
- malformed manifest entries.

The manifest records every excluded case. No post-hoc case or dimension
exclusion is allowed for v1.0. A newly discovered invalid case requires a new
case-set version; the v1.0 run remains published with the defect/truncation
noted.

## Capture, replay, and provenance

Each attempt captures:

- request/response payloads;
- tool calls/results;
- workspace before/after references;
- diagnostics/progress records;
- evaluator inputs/outputs;
- retry/timeout/cancellation status; and
- timing/token/cost counters.

The report bundle contains:

- canonical manifest and hash;
- case/rubric/reference hashes;
- package graph and git SHA;
- workflow/run IDs;
- model and sampling controls;
- seeds;
- raw and normalized trial rows;
- paired evidence;
- judge calibration/disagreement;
- diagnostics-parity report; and
- checksums for every published artifact.

Deterministic evaluators and advisory judges operate on capture/replay inputs
where possible so rescoring does not perturb agent execution.

## Stopping, peeking, and truncation

No interim outcome review may:

- add trials/cases;
- change exclusions;
- change metrics or methods;
- alter arm order;
- stop an unfavorable arm; or
- extend a favorable arm.

Only the pre-declared caps can truncate execution. A rerun after code, case,
model, rubric, or input changes is a new registered run and is not pooled with
v1.0.

## Governance and human review

The immutable comparison report requires a human signature block containing:

- reviewer identity and date;
- artifact bundle/checksum reviewed;
- deterministic anchors relied on;
- paired uncertainty acknowledged;
- diagnostics parity status;
- judge calibration/disagreement acknowledged;
- truncation/cap status; and
- retention recommendation.

The retention decision must cite workload-specific deterministic parity and
migration guidance. Hosted stochastic evidence or judge evidence alone cannot
remove an existing implementation or establish a default.

## Method references

- Newcombe, R. G. (1998), interval estimates for paired proportions:
  <https://doi.org/10.1002/(SICI)1097-0258(19981130)17:22%3C2635::AID-SIM954%3E3.0.CO;2-C>
- McNemar exact paired binary test:
  <https://www.statsmodels.org/stable/generated/statsmodels.stats.contingency_tables.mcnemar.html>
- Case-level paired bootstrap principles:
  <https://docs.scipy.org/doc/scipy/reference/generated/scipy.stats.bootstrap.html>
- Position bias in LLM judges:
  <https://arxiv.org/abs/2406.07791>
- Self-preference bias in LLM judges:
  <https://arxiv.org/abs/2410.21819>
