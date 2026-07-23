# Contract: Comparative Evaluation Evidence

## Execution modes

- Current Foundry workspace-driven iterative loop
- Plain Harness with explicit compaction
- Hybrid Harness with compaction and Foundry workspace

## Controlled inputs

Every comparison pins:

- case-set and case version;
- chat provider and model;
- sampling configuration;
- controlling instructions;
- tools and tool versions;
- initial workspace state;
- token budget;
- iteration cap;
- retries;
- timeout;
- cancellation policy;
- capability profile and package versions.

## Required evidence dimensions

- deterministic task completion;
- conversation and decision continuity;
- cumulative and peak token usage;
- context-window safety;
- artifact production and reuse;
- tool trajectory and errors;
- end-to-end latency;
- cancellation;
- termination;
- diagnostics completeness;
- uncertainty.

## Case requirements

- Every case has a deterministic completion or artifact predicate.
- Every dimension used in a product decision has deterministic reference
  evidence.
- Development cases are labeled and excluded from unbiased comparison until a
  new case-set version is cut.

## Judge requirements

- Judges are corroborating signals only.
- Reports record judge model, version, rubric, and bias checks.
- Uncalibrated judge evidence remains advisory.
- Deterministic and judge disagreement is reported.

## Statistical requirements

- Trial count, aggregation, paired comparison, and uncertainty method are
  declared before execution.
- Point estimates without uncertainty are not comparative evidence.
- Hosted stochastic results are not sole automated merge or removal gates.
- Numeric product thresholds remain a later product decision.

