# Gate G7 Decision — Harness Hardening, NativeAOT, and Guidance

## Decision

**PROVISIONAL PASS for the cumulative G7 hardening slice. Final G7.4 hosted
evidence is pending publication.**

G7 promotes only evidence-backed profiles and documentation:

1. a reusable `IHarnessScenario` / `HarnessScenarioRunner` lifecycle;
2. source-generated tool parity across Harness ingress paths;
3. `AotHarnessApp`, published and executed as a native binary;
4. a reviewed no-analyzer disposition;
5. first-class Harness and context-strategy guidance;
6. a deterministic non-Azure selected-provider example; and
7. explicit public/internal profile and AOT dispositions.

No new speculative Harness diagnostic rule ships. The existing MAF source
generator remains packaged through MSBuild's analyzer mechanism, and existing
`FDRYMAF` rules are unchanged.

## Evidence identity

| Slice | Commit / PR | Evidence | Disposition |
|---|---|---|---|
| Scenario runner, parity, and NativeAOT proof | `0985a8c5` / PR #115 | 213 Harness tests, 69 generated-wrapper tests, 2,181 core tests, native binary execution | Merged |
| Analyzer feasibility | `a1fec14a` / PR #116 | Candidate matrix, independent MAF and rubber-duck review | Merged; approve none |
| Guidance, selected-provider example, and final gate | Current G7.4 branch | Docs/example/local validation | Hosted checks pending |

Hosted PR #115 checks passed:

- [`build-test-pack`](https://github.com/ncosentino/foundry/actions/runs/30216503291/job/89831535737)
- [`aot`](https://github.com/ncosentino/foundry/actions/runs/30216503291/job/89831535698)
- [`aot-harness`](https://github.com/ncosentino/foundry/actions/runs/30216503291/job/89831535706)
- [`docs`](https://github.com/ncosentino/foundry/actions/runs/30216503287/job/89831535866)

Hosted PR #116 checks passed:

- [`build-test-pack`](https://github.com/ncosentino/foundry/actions/runs/30218032143/job/89835487741)
- [`aot`](https://github.com/ncosentino/foundry/actions/runs/30218032143/job/89835487774)
- [`aot-harness`](https://github.com/ncosentino/foundry/actions/runs/30218032143/job/89835487707)
- [`docs`](https://github.com/ncosentino/foundry/actions/runs/30218032141/job/89835487857)

All local .NET commands use
`$env:NUGET_PACKAGES='G:\dev\caches\nuget\packages'`.

## Scenario and NativeAOT disposition

`HarnessScenarioRunner` extends existing scenario infrastructure without making
the shipping Testing package depend on the optional complete bundle.
Scenarios own agent construction; the runner owns:

- generated-provider-only tool resolution;
- one per-run DI scope;
- workspace and trusted execution-context scope;
- real `AgentSession` creation and use;
- metadata-transparent generated-body execution tracking; and
- base plus Harness-specific verification evidence.

`AotHarnessApp` is the minimum supported NativeAOT Harness profile. It proves:

- source-generated tools with no reflection fallback;
- optional Foundry complete-bundle factory construction;
- a deterministic non-Azure provider;
- workspace and session lifecycle;
- trim/AOT warnings as errors; and
- native binary execution with nonzero invariant exits.

The minimum profile excludes dynamic skills/scripts, background agents, loop
evaluators, file providers, approvals, and experimental hybrid compaction.

## Analyzer disposition

T087 approved no new Harness analyzer. No candidate misuse was both fully
statically decidable and non-redundant with compiler required-member/nullability
diagnostics, source-generator and existing analyzer rules, fail-closed runtime
guards, package validation, or trim/NativeAOT tooling.

T088-T090 did not execute. No diagnostic ID or analyzer release entry was
allocated. Issue #53 closed with the no-analyzer disposition.

## Profile and API disposition

### Public supported candidates

- Plain Foundry MAF agents and workflows remain public.
- The optional complete-bundle package and its explicit configuration/default
  inspection remain public prerelease candidates.
- `IHarnessScenario`, `HarnessScenarioRunner`, and their context/result records
  are public testing candidates.

### Internal selected-provider candidate

`HarnessProviderComposition`, capability profiles, execution binding, provider
plugins, workspace bridges, and experimental hybrid compaction remain
`internal`. `HarnessHybridApp` receives friend-assembly access solely to
demonstrate and test the stable selected-provider seam. It is not a supported
consumer composition API.

The example is non-packable. Because the repository assemblies are unsigned,
the `InternalsVisibleTo` grant is not a security boundary and could be imitated
by an assembly with the same name. The gate treats the seam as unsupported
internal implementation, not as inaccessible security-sensitive functionality
or a compatibility promise.

The example requests:

- GeneratedTools
- FunctionInvocation
- MessageInjection
- OpenTelemetry

with Foundry owning both the tool loop and telemetry. It uses a deterministic
non-Azure `IChatClient`, generated workspace tool, trusted binding, real
session, and progress evidence.

### Hybrid context disposition

The task-defined example name refers to the selected-provider seam where hybrid
context could attach, not to an enabled compaction feature. Experimental hybrid
compaction is deliberately disabled. The profile uses `StableOnly`; compaction
remains:

- experimental;
- internal;
- dependent on explicit policy/reducer/classifier/snapshot collaborators; and
- AOT-unverified.

The example demonstrates the seam and prints `Compaction=Disabled` rather than
claiming support it does not exercise.

## AOT capability disposition

| Capability | G7 disposition |
|---|---|
| Generated tools | Verified by generated wrappers and native binary |
| Function invocation | Verified in native complete-bundle profile; stable selected-provider runtime example |
| Message injection | Verified by prior Harness compatibility/AOT evidence |
| OpenTelemetry | Compatible; ownership/dedup tested, but provider export is not part of the native fixture |
| Per-service history | Runtime-tested; not part of the minimum native profile |
| Approvals | Runtime-tested; not part of the minimum native profile |
| Todo / AgentMode / Skills / WebSearch | AOT unverified |
| FileMemory / FileAccess | AOT unverified |
| Experimental hybrid compaction | AOT unverified |
| Background agents / loop evaluators | Unexposed and AOT unverified |

## Documentation disposition

The public guidance distinguishes:

- plain Foundry MAF agents;
- the internal selected-provider candidate;
- the optional complete bundle;
- Foundry iterative execution;
- generated tools and NativeAOT; and
- experimental hybrid context.

Package maps include the optional Harness package. Context guidance no longer
presents iterative execution as the only valid answer to conversation growth.

## Accepted limitations and deferred work

- Selected-provider composition remains internal.
- The optional bundle and testing APIs remain prerelease candidates.
- Hybrid compaction remains experimental and AOT-unverified.
- The older G1 compatibility/AOT probe remains until the cleanup gate confirms
  the G7 replacement evidence.
- Hosted comparative quality, token, latency, continuity, cancellation, and
  uncertainty evaluation remains G8.
- Stable release and retention/deletion decisions remain G9.

## Gate completion condition

Replace the provisional decision with a final PASS after the G7.4 PR:

1. runs the selected-provider example;
2. passes build/test/package, standard AOT, Harness AOT, and documentation jobs;
3. records the merge commit and final G7 integration head; and
4. closes issue #54 with that evidence.
