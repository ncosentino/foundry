# Quickstart Validation Guide: First-Class MAF Harness Support

This guide defines the validation sequence for the future implementation. It
does not authorize dependency or runtime changes from this specification branch.

## 1. Validate Spec Kit artifacts

From the repository root:

```powershell
.\.specify\scripts\powershell\check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks
```

Expected:

- active feature directory is `specs/001-maf-harness-first-class`;
- `spec.md`, `plan.md`, and `tasks.md` exist;
- research, data model, contracts, quickstart, and tasks are listed;
- review and traceability artifacts are verified separately because the stock
  prerequisite script does not enumerate them.

Run the Spec Kit consistency analysis after task generation:

```text
/speckit.analyze
```

Expected:

- no critical constitution conflict;
- every functional requirement maps to at least one task;
- no task is unmapped;
- no unresolved clarification or placeholder remains.

## 2. Compatibility gate validation

The first implementation group creates an isolated candidate package graph.

Required candidate versions:

```text
Microsoft.Agents.AI                1.15.0
Microsoft.Agents.AI.Workflows      1.15.0
Microsoft.Agents.AI.Workflows.Generators 1.15.0
Microsoft.Agents.AI.Harness        1.15.0
Microsoft.Extensions.AI                      10.6.0
Microsoft.Extensions.AI.Abstractions         10.6.0
Microsoft.Extensions.AI.OpenAI               10.6.0
Microsoft.Extensions.AI.Evaluation           10.6.0
Microsoft.Extensions.AI.Evaluation.Quality   10.6.0
Microsoft.Extensions.AI.Evaluation.Reporting 10.6.0
```

Validate with the repository's standard build:

```powershell
dotnet build src\NexusLabs.Foundry.slnx
```

Expected:

- core MAF, Workflows, Generators, Analyzers, Evaluation, and Testing compile;
- DevUI and Hosting status is recorded separately as passed, failed, or
  deferred;
- no Harness capability is exposed yet.

## 3. Selected-provider non-Azure scenario

Run the future deterministic Harness scenario with:

- a scripted or non-Azure `IChatClient`;
- source-generated Foundry tools;
- selected stable providers only;
- Foundry diagnostics and progress;
- no complete Harness bundle.

Expected:

- exactly one tool-invocation loop;
- generated tools execute without reflection-only discovery;
- effective capabilities are inspectable;
- unselected providers remain disabled;
- no duplicate model or tool telemetry.

## 4. Eager offload and rehydration scenario

Use a deterministic tool whose single result exceeds the configured active
context envelope.

Expected:

1. The raw result is intercepted before entering chat history.
2. The full result is stored once in `IWorkspace`.
3. The conversation receives a digest-backed reference.
4. A later explicit rehydration loads only that artifact.
5. Stable artifact content is not retransmitted on unrelated turns.
6. Missing, stale, unauthorized, and over-budget references return distinct
   evidence.

## 5. Experimental compaction scenario

Explicitly enable the experimental hybrid compaction profile.

The fixture includes:

- pinned system instructions;
- accepted decisions;
- unresolved todos;
- an approval;
- valid tool-call/result pairs;
- artifact references;
- enough history to trigger compaction with safety margin.

Expected:

- system instructions are byte-identical;
- all labeled preservation items survive;
- tool-call/result pairs remain valid;
- repeated compaction stays within documented bounded overhead;
- a non-reducing compactor uses deterministic fallback or preserves prior
  context;
- irreducible context returns structured termination;
- compaction input/output tokens are separately attributed.

## 6. Session isolation and cancellation

Run two deterministic sessions with distinct trusted execution bindings and
overlapping paths, tool arguments, and approval identifiers.

Expected:

- no cross-session history, workspace, todo, mode, or approval visibility;
- restored standing approvals remain untrusted until host validation;
- contention produces explicit evidence;
- cancellation during model, tool, compaction, persistence, rehydration,
  approval, background work, and outer loop yields cancellation outcomes rather
  than success-shaped results.

## 7. NativeAOT profile

Publish the future minimum Harness AOT example:

```powershell
dotnet publish src\Examples\AgentFramework\AotHarnessApp\AotHarnessApp.csproj -c Release -r win-x64 /p:PublishAot=true
```

Expected:

- trim and AOT warnings are errors;
- generated tools are used;
- no assembly scanning or reflection-only fallback executes;
- the published application completes a scripted Harness scenario.

The concrete example project path and supported RIDs are supplied by the
implementation tasks after the compatibility gate.

## 8. Hosted comparative evaluation

Run only through hosted CI.

Execution modes:

- current Foundry iterative loop;
- plain Harness with explicit compaction;
- hybrid Harness plus Foundry workspace.

Expected artifacts:

- versioned case-set manifest;
- pinned controls and capability profiles;
- deterministic predicates;
- per-mode dimension results;
- paired comparison and uncertainty;
- judge calibration records where judges are used;
- diagnostics and telemetry parity;
- retention/removal decision artifact.

Hosted stochastic results are evidence for human review and are not the sole
automated merge or removal gate.
