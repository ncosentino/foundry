# G1 Candidate Generator and Analyzer Results

## Execution provenance

- Candidate branch: `harness/g1-validation`
- Base stage commit: `6ca3eb9`
- SDK: .NET `10.0.301`
- Environment: local Windows deterministic unit-test execution
- Hosted package-graph smoke:
  [PR #71 run 30043993631](https://github.com/ncosentino/foundry/actions/runs/30043993631)

## Canonical results

| Selection | Baseline passed | Candidate passed | Failed | Skipped |
|---|---:|---:|---:|---:|
| Generated-tool ingress | 60 | 60 | 0 | 0 |
| Foundry source generators | 147 | 147 | 0 | 0 |
| Foundry analyzers | 19 | 19 | 0 | 0 |

The commands and selectors are unchanged from `test-baseline.md`; no resolved
test count changed. The local canonical results are the comparison evidence;
the hosted run predates the current probe's AOT/tool-round edits and provides
package-graph smoke coverage only.

## Generated-tool Harness proof

`HarnessCompatibilityProbe` now runs Foundry's source generator and retrieves
the emitted `IAIFunctionProvider` through
`AgentFrameworkGeneratedBootstrap.TryGetAIFunctionProvider()`.

The probe:

1. resolves the generated `ProbeFunctions.Echo` `AIFunction`;
2. places that generated function in `HarnessAgentOptions.ChatOptions.Tools`;
3. receives a function call from the deterministic chat client;
4. executes the generated function through Harness;
5. observes `tool-result:aot` on the next model round.

This proves the existing generated-function output is accepted by the MAF 1.15
Harness tool loop without adding reflection-based discovery or a compatibility
wrapper.

## Disposition

Pass. Foundry generators, generated wrappers, and analyzers retain their
canonical behavior on the candidate graph.
