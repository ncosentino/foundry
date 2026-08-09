---
description: Hosted diagnostic protocol for comparing plain Harness, delegated Harness, and phase-local Magentic synthesis.
---

# Autonomous Phase Evaluation 001

This protocol evaluates three alternative executors inside the synthesis phase
of the fixed-macro reference pipeline:

1. plain Harness with the accepted-manifest tool;
2. Harness with fixed background `ManifestAnalyst` and `ContractCritic`
   children; and
3. phase-local Magentic with a fixed manager and the same participant roles.

The outer artifact manifest, synthesis boundary, checkpoint, and idempotent
delivery contracts remain unchanged.

The first hosted diagnostic result is recorded in
[Autonomous Phase Evaluation 001 Results](autonomous-phase-001-results.md).

## Status and interpretation

The first pull-request run was a **diagnostic matrix**, not a powered
statistical comparison:

- one public synthetic case;
- nine scenarios;
- one trial per scenario by default;
- every scenario executes all applicable arms as one paired block; and
- arm order rotates through the six possible permutations.

The report recommendation is therefore predeclared as
`NO_SUPPORTED_RECOMMENDATION_YET`. A successful run verifies transport,
instrumentation, scenario contracts, and recovery mechanics. It does not prove
superiority, non-inferiority, or cost advantage.

After that evidence run, the workflow is manual-only to prevent documentation
or maintenance pushes from consuming credits, hitting provider rate limits, or
overwriting the fixed protocol. `workflow_dispatch` accepts up to six trials
per scenario. Six trials cover each arm-order permutation once and form an
external pilot, but still do not automatically support a product
recommendation.

## Fail-closed provider probe

The matrix runs only after the existing direct
`NexusLabs.Foundry.Copilot.CopilotChatClient` proves:

- authentication through the ephemeral Actions `GITHUB_TOKEN`;
- an ordinary response;
- requested and observed model identity;
- input and output usage;
- one function-call request;
- deterministic tool execution;
- tool-result round trip; and
- a final response after the tool result.

The workflow grants `copilot-requests: write`, sets
an evaluation-local `ICopilotTokenProvider` that uses the `ghs_` Actions token
directly as the Copilot bearer, disables retries in the client, and never reads
`apps.json` or a personal token. A failed probe publishes
`provider-probe.json`, stops the matrix, and does not fall back to another
provider.

Fork pull requests do not receive the Copilot permission or run the hosted job.

## Scenario matrix

| Scenario | Injected condition | Contract pass |
| --- | --- | --- |
| Success | None | Completed valid artifact, accepted evidence IDs, one delivery |
| Optional branch failure | Operations branch is failed in the accepted manifest | Partial result, explicit gap, required sibling preserved |
| Required branch failure | Risk branch is failed in the accepted manifest | Synthesis skipped, optional sibling preserved, failed terminal result |
| Correction succeeds | First Harness candidate loses `recommendation` | Corrected within the phase bound; Magentic is not applicable |
| Correction exhausted | Every Harness candidate or the Magentic final answer is invalid | Bounded failed result with one authoritative delivery |
| Cancellation | First active provider call blocks until caller cancellation | Canceled run, no delivery, available usage retained |
| Checkpoint restore | First synthesis attempt fails after the manifest checkpoint | Fresh same-arm restore, no start-node replay, one delivery |
| Delivery replay | Identical result is published twice | Two attempts, one authoritative result, no additional provider call |
| Ineffective progress | Harness gets one invalid candidate; Magentic gets a forced stalled ledger | Harness corrects; Magentic replans; bounded completion |

Required-failure and delivery-replay cases are macro controls. They remain in
the matrix but should not be pooled as evidence that one synthesis executor is
better.

## Deterministic measurements

Each arm result records independent booleans for:

- synthesis schema validity;
- required artifact coverage;
- exact gap reporting;
- successful sibling preservation;
- evidence IDs restricted to accepted artifacts;
- accepted prior-phase replay;
- checkpoint restore;
- delivery attempts and authoritative count; and
- the scenario-specific contract.

There is no weighted composite score.

## Operational measurements

The provider wrapper records:

- model calls;
- function calls requested by the model;
- input and output tokens;
- cached input tokens when the provider reports them;
- distinct child agents that actually received a provider call;
- provider, child-provider, and phase failures as separate counters;
- checkpoints and restores;
- plan, replan, and progress-event counts;
- wall-clock duration; and
- one trace ID per arm.

Copilot billing cost is not reported as authoritative. Token counts are evidence
of usage, not a conversion to billed AI credits.

## Quality evidence

The synthetic fixture uses exact evidence IDs, so grounding and cross-artifact
consistency are checked deterministically.

No model-based quality score is allowed to affect this diagnostic
recommendation. The result marks quality evidence as `DETERMINISTIC_ONLY`.
A later fixed-size comparison requires a separately calibrated, arm-blinded
judge protocol before semantic-quality scores can be considered.

## Failure-safe artifacts

The hosted job writes:

```text
provider-probe.json
run-status.json
experiment.json
report.json
report.md
blocks/<block-id>.json
```

The run manifest is represented by the experiment result and per-arm
provenance. Every arm records the immutable source commit, source ref, protocol
version, requested and observed model, Foundry/MAF/MEAI assembly versions,
configuration hash, case identity, and trial identity.

Each completed block is written atomically before the next block starts.
`run-status.json` is replaced after every block. The Actions artifact upload
runs under `if: always()`, so provider, execution, cancellation, and reporting
failures retain whatever evidence was available.

The final state is one of:

- `Completed` — every block was produced and every applicable scenario contract
  passed;
- `CompletedWithContractFailures` — the full matrix completed, but one or more
  arms failed their expected scenario contract; or
- `FailedInfrastructure` — an experiment item failed before producing its block
  result.

Contract failures are evaluation evidence and do not suppress the report.
Infrastructure failures make the process fail after the partial report is
written.

## Engineering conclusion

All evaluation-specific provider probing, fault injection, telemetry, result
schemas, and reporting remain non-packable and example-local.

The diagnostic report explicitly recommends no extraction. Potential reusable
surfaces—phase outcomes, all-settled joining, Magentic ledger capture, and
common arm instrumentation—must first repeat outside this evaluation and prove
provider-neutral before becoming Foundry APIs.
