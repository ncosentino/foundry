# G1 Candidate Evaluation, Diagnostics, Progress, and Testing Results

## Local deterministic diagnostics selection

The canonical core selection that includes diagnostics, progress, and scenario
testing passed 102 tests with zero failures or skips. No broad evaluation suite
was run locally.

## Hosted Evaluation and Reporting selection

Evidence comes from the successful
[PR #71 hosted run 30043993631, attempt 2](https://github.com/ncosentino/foundry/actions/runs/30043993631).
That run validates the committed MAF 1.15 / MEAI 10.6 package graph and predates
the current probe-only AOT/tool-round edits, which do not change Evaluation or
Reporting code.
The exact canonical classes from `test-baseline.md` produced:

| Class | Passed |
|---|---:|
| `ExperimentRunnerEvaluationTests` | 4 |
| `ExperimentRunnerSchedulingTests` | 4 |
| `AgentRunDiagnosticsEvaluationExtensionsTests` | 5 |
| `EvaluationCaptureChatClientTests` | 17 |
| `ToolCallTrajectoryEvaluatorTests` | 16 |
| **Canonical Evaluation total** | **46** |
| `MeaiReportingExperimentAdapterTests` | **15** |

The full hosted projects also passed:

- `NexusLabs.Foundry.Evaluation.Tests`: 295
- `NexusLabs.Foundry.Evaluation.Reporting.Tests`: 15

## Hosted retry disposition

The first attempt's Evaluation test host hit the five-minute inactivity watchdog
while
`ExperimentRunnerRetryTests.RunAsync_DelayedRetry_ReleasesWorkerAndSharedLimiter`
was active. The same test passed in 150 ms on the prior green baseline and the
failed-job rerun completed successfully. No local evaluation run or unrelated
code change was made for the watchdog event.

## Disposition

Pass. Evaluation, reporting, diagnostics, progress, and testing surfaces show no
reproducible candidate regression. T013 records the MEAI 10.6 telemetry schema
delta; composed ownership and no-duplication validation remains G2 T019, where
the Foundry Harness composition seam exists.
