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
| Harness | One Harness agent, manifest tool, artifact loop evaluator | Accepted manifest reference and gaps | Candidate synthesis artifact |
| Magentic | Manager plus fixed participant catalog, plan, progress ledgers, stall replan | Accepted manifest reference and gaps | Candidate synthesis artifact |

Neighboring macro nodes and result consumers cannot distinguish the arms
through IDs or message types. Both use executor ID `synthesis-phase.v1`, receive
`ReferencePhaseArtifact`, and return `ReferencePhaseArtifact`. The same
`SynthesisArtifactBoundaryExecutor` remains authoritative.

The executor CLR types are different, so checkpoint compatibility is
arm-specific. Restore only the retained live run that created the checkpoint;
changing arms is a new run, not an in-place recovery operation.

## Matched comparison envelope

The deterministic probe gives both arms the same phase-level constraints:

| Constraint | Harness | Magentic |
| --- | --- | --- |
| Manifest input | Same accepted manifest | Same accepted manifest |
| Artifact policy | Exact accepted evidence and gaps | Exact accepted evidence and gaps |
| Correction attempts | At most 2 | At most 2 |
| Provider-call cap | 24 per synthesis execution | 24 shared by manager and participants per synthesis execution |
| Final authority | `SynthesisArtifactBoundaryExecutor` | `SynthesisArtifactBoundaryExecutor` |

The common provider-call budget counts chat-client invocations, including tool
rounds. It resets when an outer checkpoint replays the synthesis phase, but is
shared across all correction attempts within that phase execution. Exhausting
the cap fails the synthesis arm rather than granting one architecture extra
work.

Magentic's round, stall, and reset limits remain explicit because they control
different orchestration behaviors; they are not treated as equivalent to
provider calls. The probe records actual call usage under the common cap but
does not turn those deterministic counts into a quality or cost recommendation.

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

                Return one JSON object containing nonempty summary and
                recommendation fields. Evidence must contain exactly the
                accepted artifact references, and gaps must match the
                accepted manifest exactly.
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

The correction probe runs the same bounded Magentic factory twice. The first
attempt forces one stalled plan and returns a manifest-grounded artifact that
omits `recommendation`; the shared validator supplies correction feedback, and
the second attempt follows the same bounded path before returning a valid
artifact.

Within each attempt:

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
ledger said the request was not satisfied. The phase adapter therefore rejects any run that observed an invalid-speaker
warning or lacks a satisfied final ledger, even if upstream emitted plausible
final text. The unchanged artifact boundary still validates the final JSON.

## Shared artifact correction policy

Both arms use `ReferenceArtifactValidator` and the same structured correction
feedback. The Harness arm applies it through `DelegateLoopEvaluator`. The
Magentic adapter validates each final answer and may run one new phase-local
Magentic attempt with the same feedback before returning the candidate to the
macro graph. Both attempts share the phase's 24-call cap.

The final deterministic boundary remains authoritative after either arm
exhausts its two attempts. A focused test drives Magentic from a
manifest-grounded but incomplete artifact to a corrected artifact; adversarial
unknown evidence and gap mismatches are covered by the shared artifact corpus.

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
4. restores that checkpoint on the same live `StreamingRun`;
5. observes the restored review request;
6. approves the replanned plan; and
7. completes with a valid artifact.

Embedding Magentic inside one outer executor does not automatically expose its
inner checkpoint to the outer workflow. A production host that needs mid-phase
recovery must persist and associate that inner checkpoint separately. The
reference keeps the production recovery unit at the outer artifact boundary.
It does not claim fresh-workflow or process-restart resume; ADR-0016 documents
why Foundry limits checkpoint recovery to the retained live run on MAF 1.17.

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

## Probe status

This implementation is a behavioral probe, not a recommendation about which arm
to ship and not a decision about whether Foundry should add a Magentic
convenience API. It establishes that:

- raw MAF composition can stay inside one fixed macro phase;
- plan, replan, review, warning, and ledger behavior is observable;
- invalid-speaker paths can be rejected fail closed;
- both arms can share one artifact policy and provider-call cap; and
- outer same-run checkpoint replay preserves the macro recovery boundary.

Hosted evaluation still needs calibrated reliability, latency, token, and
recovery evidence before any architecture recommendation is made.

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
stall/replan, plan revision and approval, invalid speakers, same-run inner and
outer checkpoint restoration, matched provider-call exhaustion, shared artifact
correction, and final artifact validation.

## References

- [MAF Magentic guidance](https://learn.microsoft.com/en-us/agent-framework/workflows/orchestrations/magentic)
- [MAF 1.17 `MagenticWorkflowBuilder`](https://github.com/microsoft/agent-framework/blob/dotnet-1.17.0/dotnet/src/Microsoft.Agents.AI.Workflows/MagenticWorkflowBuilder.cs)
- [MAF 1.17 `MagenticOrchestrator`](https://github.com/microsoft/agent-framework/blob/dotnet-1.17.0/dotnet/src/Microsoft.Agents.AI.Workflows/Specialized/Magentic/MagenticOrchestrator.cs)
- [MAF 1.17 Magentic tests](https://github.com/microsoft/agent-framework/blob/dotnet-1.17.0/dotnet/tests/Microsoft.Agents.AI.Workflows.UnitTests/MagenticOrchestrationTests.cs)
