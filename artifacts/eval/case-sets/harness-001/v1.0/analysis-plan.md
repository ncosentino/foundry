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
- token/context budget, iteration/tool-round caps, maximum provider requests per
  attempt, attempt timeout, and cancellation policy;
- versioned provider pricing table and conservative cost-reservation formula;
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

All eight hosted cases must satisfy these conditions when the v1.0 manifest is
frozen. Development cases may exist in the manifest but are outside the hosted
ID set and excluded from this run.

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
- maximum provider requests per attempt: 8;
- global provider-request reservation budget: 1,152
  (`144 attempts × 8 requests`);
- workflow hard timeout: 60 minutes;
- scheduling deadline: 50 minutes;
- estimated provider-cost reservation budget: USD 25;
- maximum attempt duration: 120 seconds;
- maximum output tokens per provider request: 2,000; and
- maximum concurrent agent runs: 3.

The scheduling unit is one complete paired batch: all three arms for one
case/trial index. The 24 case/trial batches are shuffled once from the pinned
ordering seed. A batch is scheduled only when the hosted driver can reserve
the worst-case request and estimated-cost allowance for all three arms and
their permitted retry. In-flight work therefore consumes capacity that was
reserved before launch.

Estimated cost uses the pinned pricing table, request cap, context/token
budgets, and retry allowance. It is a hard cap on the protocol's estimated
cost, not a claim about the provider's final invoice. The per-attempt request
counter terminates an attempt before a ninth provider request.

No new batch is scheduled after the 50-minute deadline, leaving time for the
current reserved batch to finish and flush evidence before the 60-minute
workflow timeout. Reaching a scheduling cap produces the reporter-level run
state `TruncatedByCap`; it is not an `ExperimentItemStatus`.

After every paired batch, the hosted driver writes the batch result and
scheduling ledger incrementally. A truncated run retains completed batches,
records unscheduled batches explicitly, cannot be selectively extended after
outcome inspection, and is advisory with degraded power.

## Pairing and trial ordering

For every case/trial index, all three arms use the same:

- case inputs and workspace fixture;
- controlling instructions and tools;
- model/sampling configuration;
- derived case/trial seed, where the provider accepts a seed; and
- budgets and timeout.

Arm execution order is deterministically shuffled per case/trial from the
pinned arm-order seed within each complete paired batch. Pairing is analyzed
at the case level; aligned trial indices are a variance-reduction aid, not an
independence claim. A scheduling cap never schedules only one arm from a
case/trial batch.

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

- binary dimensions use majority outcome across the three planned trial slots;
- continuous dimensions use the trial mean as the case-level value only under
  the conditional-on-full-measurement rules below;
- the trial median is retained as a descriptive robustness value; and
- attempt/retry counts remain operational descriptors.

Pairwise analysis uses only case-level paired values. Trial rows are never
treated as 24 independent population samples.

For the pessimistic binary treatment, every **scheduled** trial slot yields a
0/1 value: unscorable, failed, timed-out, canceled, or invalid scheduled trials
are 0. A case/arm binary value requires all three trial batches to have been
scheduled; if global truncation leaves fewer than three scheduled slots, that
case is excluded from all case-level contrasts and counted as
`incomplete-due-to-cap`.

For the `Inconclusive` sensitivity, a case/arm cell is defined only when all
three scheduled trials are scorable. Cells with one or more unscorable trials
are dropped; two-trial ties are never broken post hoc.

At least six fully scheduled cases are required for any retention
recommendation. Below six, all results are descriptive truncation evidence
only.

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
| Cumulative tokens | Diagnostics counters | Dual-success full-cell trial mean | Conditional mean paired difference | Case-level paired percentile bootstrap plus pessimistic cap sensitivity | Secondary |
| Peak tokens | Diagnostics counters | Dual-success full-cell trial mean | Conditional mean paired difference | Case-level paired percentile bootstrap plus pessimistic cap sensitivity | Secondary |
| Attributed artifact/context cost | Attribution counters | Dual-success full-cell trial mean | Conditional mean paired difference | Case-level paired percentile bootstrap plus pessimistic cap sensitivity | Secondary |
| Latency | Monotonic duration | Dual-success full-cell trial mean | Conditional mean paired difference | Case-level paired percentile bootstrap plus pessimistic timeout sensitivity | Secondary |
| Judge dimensions | Versioned rubric | Trial mean per case | Advisory mean paired difference | Descriptive bootstrap only | Exploratory |

Continuous reports also include paired median difference descriptively. No
normality assumption or unpaired test is used.

The binary success estimand is the difference in the proportion of cases whose
three-trial majority succeeds. It is not an estimate of a per-trial success
probability.

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

The primary continuous estimand is explicitly **conditional on dual success**.
A case pair is valid only when both arms:

- scheduled all three trial slots;
- completed all three trials successfully for the deterministic completion
  predicate; and
- produced three finite, diagnostics-comparable values for the metric.

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

With at most eight cases, percentile-bootstrap coverage is unstable. Every
continuous interval is labeled descriptive and small-sample; it cannot support
a superiority, non-inferiority, or removal claim.

Every conditional continuous table is published adjacent to completion,
timeout, cancellation, and failure counts. A conditional latency/token/cost
advantage cannot justify retention or removal when the same arm has worse
deterministic completion evidence.

## Missing, unknown, invalid, and non-comparable samples

Every scheduled trial retains an explicit `ExperimentItemStatus`. Missing or
invalid metric values are evaluator outcomes mapped to the status/treatment
rules below; they are not additional enum members. Unscheduled batches are
recorded separately in the scheduling ledger.

### Binary dimensions

Primary treatment is pessimistic:

- execution failure, timeout, cancellation, missing metric, invalid metric,
  prerequisite failure, and evaluation failure count as trial failure.

Sensitivity treatment:

- unknown/unscorable trials use
  `ExperimentUnknownSampleTreatment.Inconclusive`. The entire case/arm cell is
  dropped unless all three trials are scorable; denominator changes are
  reported.

Both treatments are published. Divergence is evidence.

### Continuous dimensions

The primary estimate is conditional on dual full success as defined above.
Non-finite, missing, failed, timed-out, canceled, non-comparable, or incomplete
cells are dropped and counted.

The required pessimistic sensitivity substitutes pre-declared metric bounds
for **scheduled** failed/unscorable trials:

- latency: the full trial timeout including the permitted retry;
- cumulative tokens and attributed cost: the trial's reserved worst-case cap;
- peak tokens: the per-request context/output cap.

The sensitivity is labeled pessimistic and never replaces the conditional
primary estimate. Trials never scheduled because of a global cap are not arm
failures and are not imputed; their incomplete cases are excluded symmetrically
from all arm contrasts.

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

Whole-run caller cancellation produces `CanceledByCaller`. A binding scheduling
cap produces `TruncatedByCap`. These are reporter-level hosted-run states, not
new `ExperimentItemStatus` or `ExperimentRunOutcome` values.

Per-trial terminal failures remain in binary denominators under the primary
pessimistic treatment. Unscheduled paired batches remain outside item
denominators and are disclosed in the scheduling ledger.

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

At manifest freeze:

- hosted IDs are exactly `h001-01` through `h001-08`;
- all eight hosted cases are well-formed and have deterministic completion
  predicates;
- every decision dimension has deterministic reference evidence; and
- development cases are outside the hosted ID set and labeled explicitly.

A malformed manifest or invalid hosted case prevents the run from starting.
If a previously hidden case/reference defect is discovered during or after
execution, v1.0 is `InvalidInput`: no comparative conclusion or retention
recommendation is published. The defect bundle remains immutable, and a
corrected run requires a new case-set version. No post-hoc case or dimension
exclusion is permitted.

## Capture, replay, and provenance

Provider request/response capture and the full evidence bundle are separate
surfaces.

`EvaluationCaptureChatClient` captures provider request/response payloads only.
Each arm/case/trial/attempt uses its own capture-store namespace, and replay is
allowed only after validating the complete pinned-input tuple hash. The chat
capture key is never treated as proof that non-captured options match.

The hosted driver and reporter write an incremental attempt/batch evidence
bundle, using the existing JSON artifact writer, containing:

- request/response payloads;
- tool calls/results;
- workspace before/after references;
- diagnostics/progress records;
- evaluator inputs/outputs;
- retry/timeout/cancellation status; and
- timing/token/cost counters.

The batch bundle is flushed after every complete paired batch, before the next
batch is scheduled. `run-status.json` records `Completed`, `TruncatedByCap`,
`CanceledByCaller`, or `InvalidInput` as reporter-level states without changing
the core experiment outcome enums.

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

Deterministic evaluators rescore normalized evidence records and chat captures;
advisory judges replay captured transcripts. Workspace state, retry state,
progress, and diagnostics are retained as observed evidence and are not
reconstructed from the chat-response store.

## Stopping, peeking, and truncation

No interim outcome review may:

- add trials/cases;
- change exclusions;
- change metrics or methods;
- alter arm order;
- stop an unfavorable arm; or
- extend a favorable arm.

Only the pre-declared scheduling/deadline caps can truncate execution. Batches
are complete paired units, persisted before the next batch begins. A rerun
after code, case, model, rubric, or input changes is a new registered run and
is not pooled with v1.0.

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

The retention decision must cite workload-specific deterministic parity,
completion/reliability evidence, uncertainty, and migration guidance. Judge
evidence cannot break a deterministic tie or override uncertainty. Hosted
stochastic or judge evidence alone cannot remove an existing implementation or
establish a default; when deterministic evidence is tied or inconclusive, the
default disposition is retention pending stronger evidence.

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
