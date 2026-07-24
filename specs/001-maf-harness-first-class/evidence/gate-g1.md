# Gate G1 Decision

## Decision

**PASS FOR PACKAGE, AOT, AND CORE LIFECYCLE COMPATIBILITY**

The core MAF 1.15 / Harness 1.15 / MEAI 10.6 graph is coherent and can advance
to G2 internal composition work. No consumer-facing Harness API is approved by
this gate. Restored/forged approval security, custom-provider durability,
composed telemetry, workspace offload, and hybrid compaction remain downstream
gates.

## Evidence identity

- Pre-uplift baseline commit:
  `06b04e6daec39c4a1fb57c3c94e7189fd7803ea0`
- G1 stage branch: `harness/g1-integration`
- Completed leaf PRs:
  [#70](https://github.com/ncosentino/foundry/pull/70),
  [#71](https://github.com/ncosentino/foundry/pull/71), and
  [#72](https://github.com/ncosentino/foundry/pull/72), and
  [#74](https://github.com/ncosentino/foundry/pull/74)
- G1 stage PR:
  [#75](https://github.com/ncosentino/foundry/pull/75)
- Hosted Harness AOT implementation commit:
  `932cb5ec701a9aa9dc93c3315e8fc16eacede6c5`
- Stage CI:
  [build/test/AOT run 30055150928](https://github.com/ncosentino/foundry/actions/runs/30055150928),
  [documentation run 30055150876](https://github.com/ncosentino/foundry/actions/runs/30055150876), and
  [Harness AOT run 30055150937](https://github.com/ncosentino/foundry/actions/runs/30055150937)

## Gate evidence

| Criterion | Result | Evidence |
|---|---|---|
| Coherent core package graph | Pass with a known Microsoft.Extensions patch-level runtime risk assigned to the G2 non-Azure smoke | `package-candidate.md` |
| No unexplained targeted regression | Pass | `test-core-workflows.md`, `test-generators-analyzers.md`, `test-evaluation-diagnostics.md` |
| Generated-tool Harness NativeAOT publishes and executes | Pass on Windows x64 and GitHub-hosted Linux x64 | `aot-candidate.md` |
| Raw tool-result interception before `FunctionResultContent` | Pass through public FICC `FunctionInvoker` | `uplift-delta.md` |
| Per-service history lifecycle | Pass; exact two-load/two-store callbacks and default in-memory round-trip observed | `uplift-delta.md` |
| Message injection seam | Pass; queued message observed in the function loop | `uplift-delta.md` |
| Approval request/response lifecycle | Pass for approve/reject and exactly-once invocation; restored/forged trust remains G3 | `uplift-delta.md` |
| Per-tool-round complete-pair compaction | Unavailable in the stock Harness pipeline; explicitly recorded | `uplift-delta.md` |
| DevUI satellite | Compile/package pass; interactive runtime deferred | `devui-satellite.md` |
| Hosting satellites | Compile/package pass; alpha protocol runtime deferred | `hosting-satellite.md` |

## Stop-condition evaluation

- Coherent core graph exists: **yes**
- Existing generated-tool AOT path remains viable: **yes**
- New generated-tool Harness AOT path executes: **yes**
- Targeted regressions have a migration path: **yes**
- Required lifecycle seams are either proven or explicitly unavailable: **yes**

The G1 stop condition is not triggered.

## Constraints carried into later gates

1. **G2 composition:** configure exactly one FICC and use its public
   `FunctionInvoker` seam for any generic pre-history tool-result transformation.
   G2 must not implement workspace offload, artifact persistence, rehydration,
   or hybrid compaction.
2. **G2 telemetry:** T019 must prove one effective telemetry owner against the
   MEAI 10.6 semantic-convention changes.
   The G2 non-Azure smoke must also exercise the accepted Microsoft.Extensions
   implementation/abstraction patch-level mix. Gate G2 cannot pass until that
   runtime risk is closed or explicitly failed with a migration decision.
3. **G4 eager offload:** offload must occur through `FunctionInvoker` or an
   equivalent explicit selected-provider seam before FICC constructs ordinary
   `FunctionResultContent`.
4. **G5 hybrid context:** do not treat Harness `CompactionProvider` as a
   reliable intermediate-round compaction hook. The local-history sentinel
   causes it to skip after round one. The default history reducer runs before
   each retrieval but sees stored history without the current function result.
   Upstream clarification is tracked in issue #73.
5. **G3 approvals and continuity:** validate forged and restored approval
   identity, standing-approval host authorization, and custom history-provider
   session restoration. Do not depend on internal approval chat-client types.
6. **Satellites:** DevUI and Hosting remain optional preview/alpha integrations
   and do not block the stable core lane.
7. **Workflow diagnostics:** group-chat diagnostic stage order is not execution
   order; tests and consumers must use invocation evidence where ordering is
   contractual.

## API disposition

- Harness profile and composition APIs: internal candidate only.
- Experimental MAF surfaces: no public Foundry promotion.
- Package publication: none.
- CI pack outputs are validation artifacts only. DevUI and Hosting packages must
  not be published from this increment before their runtime/release gate passes.
- Ordinary non-Harness behavior: unchanged except compatibility updates required
  by the package uplift.

## Next permitted work

G2 may begin with internal capability evidence, generated-tool ingress,
trusted execution binding, deterministic composition ordering, one-loop guards,
and telemetry ownership.
