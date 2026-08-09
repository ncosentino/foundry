---
description: Compare raw MAF Magentic orchestration with a Harness agent inside one fixed-macro synthesis phase.
---

# Phase-Local Magentic Comparison

Magentic is evaluated as an optional executor **inside one open-ended synthesis
phase**. It is not the outer workflow, and it does not replace deterministic
artifact gates, branch outcomes, checkpoint selection, or delivery.

The fixed macro topology remains:

```text
... -> accepted specialist manifest
    -> synthesis executor
    -> synthesis artifact gate
    -> idempotent delivery
```

Only the synthesis executor changes:

| Arm | Internal behavior | Macro input | Macro output |
| --- | --- | --- | --- |
| Harness | One Harness agent, manifest tool, schema loop evaluator | Accepted manifest reference and gaps | Candidate synthesis artifact |
| Magentic | Manager plus fixed participant catalog, plan, progress ledgers, stall replan | Accepted manifest reference and gaps | Candidate synthesis artifact |

Neighboring macro nodes and result consumers cannot distinguish the arms
through IDs or message types. Both use executor ID `synthesis-phase.v1`, receive
`ReferencePhaseArtifact`, and return `ReferencePhaseArtifact`. The same
`SynthesisArtifactBoundaryExecutor` remains authoritative.

The executor CLR types are different, so checkpoint compatibility is
arm-specific. Resume a run with the same synthesis arm that created its
checkpoint; changing arms is a new run, not an in-place recovery operation.

## Raw MAF composition

The comparison uses `MagenticWorkflowBuilder` directly:

```csharp
Workflow workflow = new MagenticWorkflowBuilder(manager)
    .AddParticipants([manifestAnalyst, contractCritic])
    .WithName("phase-local-magentic-synthesis.v1")
    .RequirePlanSignoff(false)
    .WithMaxRounds(8)
    .WithMaxStalls(0)
    .WithMaxResets(2)
    .WithPromptOverrides(
        new MagenticPromptOverrides
        {
            FinalAnswerPrompt =
                """
                Complete the synthesis task using only accepted evidence:
                {task}

                Return one JSON object containing nonempty summary,
                evidence, and recommendation fields.
                """,
        })
    .Build();
```

The manager and both participants are supplied explicitly and constructed
through `FoundryHarnessAgentFactory`. No participant is created dynamically.

The manager has no tools because MAF 1.17 does not support manager tool calls.
`ManifestAnalyst` receives the run-authorized manifest tool.
`ContractCritic` has no tools and checks the artifact contract when selected.
Harness loop evaluation and background agents are disabled inside participants
so the comparison measures Magentic coordination rather than nested
orchestration.

Do not call `WithOutputFrom` or `WithIntermediateOutputFrom` on this builder.
Either call suppresses Magentic's default output designations, and the internal
orchestrator is not available through the public builder API for re-selection.

## Planning and progress evidence

The autonomous comparison arm forces one stalled plan:

1. manager creates the initial task ledger;
2. first progress ledger reports a stall;
3. Magentic resets and replans;
4. the new plan selects `ManifestAnalyst`;
5. the participant resolves the accepted manifest;
6. the next ledger reports the request satisfied; and
7. the manager emits the final JSON artifact.

MAF reuses and updates the ledger object. It can mutate that object later in the
same super-step before either lockstep or off-thread event delivery, so copying
fields from `MagenticProgressLedgerUpdatedEvent` is not a reliable historical
snapshot.

The example counts the raw progress events but records immutable ledger values
at the manager-response boundary, before the orchestrator receives and mutates
them. The phase adapter also uses those producer-side snapshots for its final
satisfaction decision.

A live integration needs equivalent manager chat-client middleware if it wants
historical ledger values; the raw event payload alone is insufficient.

Available raw events are:

| MAF event | Meaning | Current Foundry treatment |
| --- | --- | --- |
| `MagenticPlanCreatedEvent` | Initial task ledger | Captured by the example-local probe |
| `MagenticReplannedEvent` | Reset or reviewer-requested plan update | Captured by the probe |
| `MagenticProgressLedgerUpdatedEvent` | Satisfaction, stall, progress, next speaker, instruction | Event counted; immutable values captured at manager response |
| `RequestInfoEvent` carrying `MagenticPlanReviewRequest` | Human plan approval or revision | Handled through raw `StreamingRun` requests |
| `WorkflowWarningEvent` | Invalid/empty speaker and progress-ledger failures | Captured and classified |
| `WorkflowOutputEvent<List<ChatMessage>>` | Manager final answer or limit termination | Accepted only with a satisfied ledger and valid artifact |

Foundry does not add new public progress events for these concepts in this
comparison. Harness model and tool diagnostics can still be enabled by
supplying a `ProgressAccessor` to the manager and participants. A public
plan/replan progress contract would be premature before hosted evidence shows
how consumers use it.

## Plan review

`RequirePlanSignoff` defaults to `true` upstream. An autonomous phase must set it
to `false` deliberately or it will halt at a pending request.

Focused tests keep signoff enabled and cover both responses:

- approval proceeds with the proposed plan;
- revision adds reviewer feedback, triggers `MagenticReplannedEvent`, presents
  the new plan, and then proceeds after approval.

Plan revisions do not consume round or reset limits. A host offering human
review must bound that interaction separately.

## Stall and limit semantics in MAF 1.17

The configured values are explicit, but their operational thresholds matter:

| Configuration | Measured behavior |
| --- | --- |
| `WithMaxStalls(0)` | First stalled ledger triggers replan |
| `WithMaxStalls(3)` | Replan occurs on the fourth net stall |
| Non-stalled round | Decrements stall count by one rather than resetting it |
| `WithMaxResets(1)` | First replan is created, but its first coordination round is blocked |
| `WithMaxResets(2)` | One reset and its replanned execution are allowed |
| `WithMaxRounds(0)` | Planning or review can occur, but no progress-ledger round runs |

Zero and negative values are not rejected by the upstream builder. The
reference uses positive round/reset bounds and intentionally uses zero stalls
to force one deterministic replan.

## Invalid next speaker

Speaker matching is exact, case-sensitive, and untrimmed.

- Empty speaker: warning, then the first participant is selected.
- Unknown or whitespace speaker: warning, then Magentic immediately asks the
  manager for a final answer.

That second path can emit a plausible final artifact even though the progress
ledger said the request was not satisfied. The phase adapter therefore rejects
any run that observed an invalid-speaker warning or lacks a satisfied final
ledger. The unchanged artifact boundary still validates the final JSON.

## Two checkpoint layers

The outer and inner checkpoints have different recovery units.

### Outer checkpoint

The existing checkpoint after `pre-synthesis-artifact-gate.v1` remains the
production macro boundary. Restoring it reruns the whole Magentic synthesis
phase, then the synthesis gate and idempotent delivery. Research and specialists
do not rerun.

### Inner Magentic checkpoint

Magentic also checkpoints its task context, current speaker, participant
sessions, plan-review request, and progress state. A focused test:

1. approves the initial plan;
2. forces a stall and replan;
3. captures the checkpoint at the stalled replan review;
4. builds a structurally identical workflow with stable manager and participant
   IDs;
5. restores the checkpoint;
6. approves the replanned plan; and
7. completes with a valid artifact.

Embedding Magentic inside one outer executor does not automatically expose its
inner checkpoint to the outer workflow. A production host that needs mid-phase
recovery must persist and associate that inner checkpoint separately. The
reference keeps the production recovery unit at the outer artifact boundary.

## Sequential coordination tradeoff

Magentic selects one participant per round. This is useful when the open-ended
phase benefits from adaptive planning and replanning, but it is a latency and
cost penalty for work that is naturally parallel:

- each active round requires a manager progress-ledger call;
- planning and every replan require two manager calls;
- only one participant receives a turn token per round; and
- the final answer requires another manager call.

The fixed macro graph therefore keeps its specialist fan-out outside Magentic.
Magentic is compared only for the synthesis phase where sequential coordination
may be justified.

## Result

The raw MAF builder is sufficient for this comparison. Foundry does not need a
Magentic convenience API yet:

- construction is already concise;
- bounds and review behavior remain visible;
- raw events expose the important state;
- the existing artifact boundary corrects Magentic's untyped final output; and
- a wrapper would mostly mirror upstream types while hiding important limits.

This is not a general endorsement. Hosted evaluation must compare reliability,
latency, token usage, and recovery quality before choosing Magentic over the
plain Harness synthesis phase.

## Run both arms

```bash
# Plain Harness synthesis
dotnet run --project \
  src/Examples/AgentFramework/AutonomousPhasePipelineApp

# Phase-local Magentic synthesis
dotnet run --project \
  src/Examples/AgentFramework/AutonomousPhasePipelineApp \
  -- --magentic
```

The deterministic tests cover plan creation, progress ledgers, forced
stall/replan, plan revision and approval, invalid speakers, fresh-workflow
checkpoint restoration, outer checkpoint replay, and final artifact
validation.

## References

- [MAF Magentic guidance](https://learn.microsoft.com/en-us/agent-framework/workflows/orchestrations/magentic)
- [MAF 1.17 `MagenticWorkflowBuilder`](https://github.com/microsoft/agent-framework/blob/dotnet-1.17.0/dotnet/src/Microsoft.Agents.AI.Workflows/MagenticWorkflowBuilder.cs)
- [MAF 1.17 `MagenticOrchestrator`](https://github.com/microsoft/agent-framework/blob/dotnet-1.17.0/dotnet/src/Microsoft.Agents.AI.Workflows/Specialized/Magentic/MagenticOrchestrator.cs)
- [MAF 1.17 Magentic tests](https://github.com/microsoft/agent-framework/blob/dotnet-1.17.0/dotnet/tests/Microsoft.Agents.AI.Workflows.UnitTests/MagenticOrchestrationTests.cs)
