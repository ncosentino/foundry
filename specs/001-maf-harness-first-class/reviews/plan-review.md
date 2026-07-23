# Plan and Task Review: First-Class Microsoft Agent Framework Harness Support

## Review Tracks

| Track | Model | Focus | Status |
|---|---|---|---|
| MAF integration | Claude Opus 4.8 | Package graph, provider placement, middleware, sessions | Complete |
| Hybrid context | Gemini 3.1 Pro | Offload, workspace authority, compaction, rehydration | Complete |
| Migration program | GPT-5.6 Terra | Task groups, dependencies, scope, release gates | Complete |
| Evaluation | Claude Opus 4.7 | Fixtures, hosted comparison, uncertainty, task coverage | Complete |
| AOT and source generation | Claude Sonnet 4.6 | Generated tools, reflection boundaries, analyzer scope | Complete |
| Autopilot and cleanup | Claude Sonnet 4.6 | Branch/PR flow, stage gates, scope control, post-implementation cleanup | Complete |

## Findings

### MAF integration

- **High**: Lane A code was incorrectly shown under the optional bundle project.
- **Medium**: Candidate package staging omitted exact satellite and MEAI entries.
- **Medium**: Middleware was modeled as one linear stack instead of agent/context
  and chat-client surfaces.
- **Low (historical)**: Quickstart initially omitted Workflows.Generators and
  was corrected before final analysis.
- **Rejected finding**: The review claimed `ChatHistoryProvider` was not a real
  upstream type; MAF 1.15 source confirms it is.

### Hybrid context

- **Critical**: The plan could place compaction where it misses intermediate
  function-invocation rounds.
- **Critical**: Rehydrated content could be immediately re-offloaded.
- **High**: Tool-result offload must transform the result before ordinary
  full-payload history message construction.
- **High**: The `AgentFileStore` bridge cannot invent compare-exchange semantics.
- **Medium**: An over-budget compaction fallback cannot preserve and forward the
  same over-budget context.
- **Medium**: Runtime composition guards are primary; static analyzers are
  conditional.

### Migration program

- **Blocking**: The compatibility group needed a direct Harness probe and
  executable AOT proof.
- **Blocking**: Workspace/identity/cancellation semantics required a feasibility
  spike before implementation.
- **Blocking**: Tool-result and compaction lifecycle seams required tracing
  before hybrid tasks.
- **Blocking**: Session restore and approval reauthorization were missing.
- **Blocking**: Public API review and auditable stop decisions occurred too late.
- **Blocking**: The duplication ledger lacked owners and release-bound triggers.
- **Blocking**: Hosted evaluation lacked an actual run and human decision step.
- **Non-blocking**: Optional bundle tests preceded project scaffolding.
- **Non-blocking**: Several parallel markers wrote the same evidence file.
- **Non-blocking**: The 110-item checklist required vertical-slice review units.

### Evaluation

- **High**: Phase 8 needed tests before evaluator and reporter implementation.
- **High**: The statistical analysis protocol had to be pre-registered.
- **High**: Judge calibration was bundled into one ambiguous task.
- **High**: Hosted workflow triggers could accidentally become stochastic merge
  gates.
- **Medium**: Paired-comparison evidence required a reusable primitive and
  known-answer tests.
- **Medium**: Per-item versus run-level evaluator ownership was ambiguous.
- **Medium**: Diagnostics-schema parity, retry-versus-trial semantics, cost/time
  caps, and intra-phase dependencies were missing.

### AOT and source generation

- **Critical**: Duplicate-loop and Harness-AOT analyzers were pre-authorized
  without demonstrated static value.
- **High**: Analyzer tests followed implementation and IDs were allocated too
  early.
- **High**: CI needed to execute the published AOT binary, not only inspect it.
- **Medium**: The compatibility gate tested the existing AOT app but not a
  direct Harness reference.
- **Medium**: Generated-tool ingress must depend on verified MAF 1.15 API shape.
- **Low**: A Harness scenario runner must extend rather than duplicate existing
  scenario infrastructure.

### Autopilot and cleanup

- **Blocking**: G8, G9, and G10 were shown as parallel in `tasks.md` rather than
  a strict sequence.
- **Blocking**: Cleanup referenced an approved retention decision that no task
  produced.
- **Non-blocking**: The cleanup PR needed explicit stage-review treatment.
- **Non-blocking**: G10 task mappings needed to appear in primary traceability
  rows.

## Disposition Log

| Finding | Disposition | Plan/task change |
|---|---|---|
| Lane ownership mismatch | Applied | Plan tree now keeps Lane A in the base MAF package and Lane B in the optional bundle |
| Package staging gaps | Applied | T005 stages exact 1.15/10.6 core and satellite candidates |
| Linear middleware model | Applied | Plan and contracts now require two-surface lifecycle tracing |
| Compaction intermediate rounds | Applied | G1/T013 proves actual lifecycle; T070 depends on the proven per-round seam |
| Rehydration re-offload loop | Applied | Rehydrated payload becomes a marked recoverable context segment after the offload boundary |
| Tool-result interception | Applied | T041 proves the seam; T051 extends/extracts shared result transformation rather than assuming a post-history middleware |
| Compare-exchange overclaim | Applied | Bridge contract preserves CAS only for Foundry callers and reports unsupported upstream semantics |
| Synchronous workspace cancellation | Applied | Bridge reports that mid-call interruption is unsupported for synchronous `IWorkspace` |
| Over-budget fallback | Applied | T060/contract require reducing fallback or structured termination |
| Direct Harness compatibility probe | Applied | T005/T006/T010 create, build, publish, and execute a direct probe |
| Session restore and approval validation | Applied | T029/T034 include restore, provider state, binding, and reauthorization |
| API gates | Applied | G2/G3/G4/G5/G6/G7 tasks record explicit dispositions before publication |
| Duplication ledger | Applied | Ledger includes IDs, owners, evidence, and release-bound triggers |
| Optional package order | Applied | T072 scaffolds production and test projects before tests |
| Unsafe parallel evidence writes | Applied | T007-T009 use separate evidence files |
| Speculative analyzers | Applied | T087 is feasibility; tests and implementation are conditional |
| AOT execution | Applied | T010 and T086 require binary execution |
| Generated-tool ingress uncertainty | Applied | T024 depends on T013 API evidence |
| Phase 8 tests-first | Applied | T096-T105 precede evaluator/reporter implementation |
| Pre-registered analysis | Applied | T095 creates the required protocol before hosted execution |
| Judge calibration | Applied proportionately | Rubrics, held-out subset, bias fixtures, advisory evaluators, and disagreement report are separate tasks; no fixed agreement threshold |
| Hosted workflow gating | Applied | T117 is dispatch/schedule only with resource caps and non-gating stochastic summaries |
| Paired comparison primitive | Applied | T102 tests and T112 implementation separate reusable evidence from Harness reporter |
| Actual hosted run/human decision | Applied | T119-T121 execute, publish, and record human-reviewed retention decisions |
| 110 tasks too granular for review | Applied | Checklist retained and expanded to 126 items, but plan requires vertical-slice PRs by migration group |
| `ChatHistoryProvider` naming finding | Rejected | Verified MAF 1.15 source exposes `ChatHistoryProvider` |
| G8/G9/G10 cleanup ordering | Applied | Task dependency graph now enforces G8 -> G9 -> G10 |
| Missing retention decision | Applied | T130 creates a human-reviewed per-artifact decision; T131 executes it |
| Cleanup PR review tier | Applied | Plan requires T131 to use stage-PR scope and gate review |
| G10 traceability gaps | Applied | FR/SC and explicit task mappings include T127-T131 |

## Final Review Outcome

All blocking and critical findings have been applied, modified with documented
rationale, or rejected using primary-source evidence. The plan and tasks retain
no unresolved review finding. Final Spec Kit cross-artifact consistency analysis
is still required before the package is considered complete.
