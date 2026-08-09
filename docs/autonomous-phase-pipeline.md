---
description: Build fixed macro workflows whose Harness phases remain autonomous while deterministic artifact gates own correctness.
---

# Fixed-Macro Autonomous Phases

Foundry's reference architecture keeps the outer workflow deterministic without
making an agent's internal tool and model-call topology part of the production
contract:

```text
deterministic intake
  -> autonomous research Harness phase
  -> deterministic research artifact gate
  -> concurrent specialist Harness phases
  -> all-settled outcome join
  -> deterministic pre-synthesis artifact gate
  -> autonomous synthesis Harness phase
  -> deterministic synthesis artifact gate
  -> idempotent deterministic delivery
```

The macro graph is developer-authored. Each Harness phase can still plan, call
tools, delegate bounded work, and correct its own output before returning to the
graph.

The complete offline implementation is under
`src/Examples/AgentFramework/AutonomousPhasePipelineApp*`.

The [Phase-Local Magentic Comparison](magentic-phase-comparison.md) swaps only
the synthesis executor while preserving this outer graph and artifact contract.

## Ownership boundaries

| Owner | Responsibilities | Does not own |
| --- | --- | --- |
| Macro workflow | Phase dependencies, required and optional branches, concurrency, join policy, checkpoint selection, terminal routing | Exact model-call count, tool order, delegation count |
| Harness phase | Planning, provider calls, tool selection, bounded child delegation, phase-local correction | Cross-phase acceptance, business delivery |
| Artifact gate | Schema validation, content-addressed persistence, accepted reference, explicit gaps and outcome | Prompt strategy inside the phase |
| Delivery sink | Idempotency key, authoritative write, conflicting replay rejection | Exploratory research or synthesis |

This is the "macro-deterministic, micro-agentic" boundary. Reliability depends
on the graph and accepted artifacts, not on predicting one exact LLM trajectory.

## Harness phases stay atomic to the macro graph

Every autonomous phase in the example is constructed through
`FoundryHarnessAgentFactory`.

The research phase enables upstream background agents. Its coordinator uses the
real `background_agents_*` tools to:

1. start two child tasks;
2. wait for each task;
3. retrieve each result; and
4. clear both terminal tasks.

The child calls overlap, but the macro graph sees one research phase and one
candidate artifact. A deterministic gate then validates the candidate JSON and
stores it before specialists may run.

Upstream background tasks do not inherit parent cancellation. The reference
test holds both children, cancels the workflow, and proves cancellation cannot
finish until the independently bounded children are released. Production child
agents therefore need their own timeout and cleanup policy.

The synthesis phase enables upstream loop evaluation. Its first candidate omits
a required schema field. A `DelegateLoopEvaluator` applies deterministic JSON
validation and requests one corrected iteration with structured feedback. The
second candidate passes without rerunning research or either specialist.

The evaluator does not accept marker prose. It applies the same
manifest-grounded evidence and gap contract as the final synthesis artifact
gate.

## Real concurrent all-settled specialists

The research artifact gate fans one accepted reference out to a required risk
specialist and an optional operations specialist. Both phases run concurrently.
Each receives only the artifact reference and resolves its body through a tool.

Each specialist wrapper always emits one bounded outcome envelope unless the
workflow itself is canceled:

| Outcome | Meaning |
| --- | --- |
| `Completed` | Valid artifact was accepted and stored |
| `Partial` | The aggregate can continue with explicit optional gaps |
| `Failed` | A phase failed, timed out, returned invalid data, or failed to emit |
| `Skipped` | A dependent autonomous phase was deliberately not invoked |

The join is a custom batching executor rather than a MAF fan-in barrier.
`AddFanInBarrierEdge` waits for every source to emit; it is not an all-settled
failure policy. The custom join receives ordinary edges, normalizes duplicate
or individually missing outcomes after the join is activated, orders branches
deterministically, and forwards the full settled set.

An optional failure produces a `Partial` manifest. Its successful sibling
artifact survives and synthesis receives an explicit gap. A required failure
produces a `Failed` manifest, and the synthesis wrapper returns `Skipped`
without invoking the synthesis agent.

## Artifact references, not transcripts

Candidate content crosses only the immediate phase-to-gate edge. After
validation, the example stores the body under a SHA-256 content address:

```text
artifact://sha256/<digest>
```

Subsequent workflow messages contain references, outcome metadata, and explicit
gaps. The pre-synthesis gate writes an accepted manifest containing:

- the research reference;
- each settled branch outcome;
- completed or partial specialist references;
- required versus optional status; and
- explicit gaps.

The synthesis prompt contains the manifest reference and outcome summary, not
prior agent transcripts or artifact bodies. A dedicated tool resolves the
manifest and its referenced bodies when the phase needs them.

Artifact tools authorize each reference against the active run ID. A model
cannot substitute an artifact or manifest that exists in the shared store but
belongs to another run.

The synthesis artifact gate derives its acceptance contract from that stored
manifest. The candidate must report:

- exactly the research reference and every completed or partial specialist
  reference in `evidence`;
- no unknown reference and no reference from a failed or skipped branch; and
- exactly the manifest's explicit gap set in `gaps`.

Order is not significant, but omissions, additions, labels in place of
content-addressed references, and duplicate values fail validation. Execution
details such as tool order or child-agent count are not part of acceptance.
The offline contract corpus includes both known-good variants and adversarial
missing, duplicate, unknown, failed-branch, and gap-mismatch candidates.

The host owns both persistence boundaries and injects them while constructing
the example runtime:

```csharp
var artifacts = new ReferenceArtifactStore();
var delivery = new IdempotentDeliverySink();

ReferencePipelineRuntime runtime = ReferencePipelineFactory.Create(
    request,
    ReferencePipelineOptions.Default,
    artifacts,
    delivery);
```

## Checkpoint before synthesis

The selected checkpoint is the `SuperStepCompletedEvent` immediately following
`pre-synthesis-artifact-gate.v1`:

```csharp
bool gateCompleted = false;
CheckpointInfo? beforeSynthesis = null;

await foreach (WorkflowEvent workflowEvent in run.WatchStreamAsync(cancellationToken))
{
    if (workflowEvent is ExecutorCompletedEvent
        {
            ExecutorId: "pre-synthesis-artifact-gate.v1",
        })
    {
        gateCompleted = true;
    }

    if (beforeSynthesis is null &&
        gateCompleted &&
        workflowEvent is SuperStepCompletedEvent
        {
            CompletionInfo.Checkpoint: { } checkpoint,
        })
    {
        beforeSynthesis = checkpoint;
        gateCompleted = false;
    }
}
```

Restoring that checkpoint replays synthesis, its artifact gate, and delivery.
It does not rerun research or either specialist. This is the intended recovery
unit: the accepted manifest protects earlier autonomous work.

The example uses the upstream typed-input `InProcessExecution.RunStreamingAsync`
path because its start executor accepts `ReferencePipelineRequest`, not the
agent `ChatMessage` plus `TurnToken` protocol. The Foundry
`StartCheckpointedAgentRunAsync` helper remains the correct path for workflows
whose start node is an agent.

## Idempotent deterministic delivery

Delivery is not an agent tool. The deterministic sink keys authoritative output
by run ID:

- the first delivery becomes authoritative;
- an identical replay returns that result without another authoritative write;
  and
- a replay carrying a different synthesis digest fails rather than silently
  replacing the result.

Workflow output events may still appear again after restore. Exactly-once
external mutation is not guaranteed by this in-memory ledger. It proves
same-process duplicate suppression. A durable integration still needs an
independently idempotent sink or an atomic transaction that covers both the
external effect and its delivery receipt.

## Why artifact gates instead of internal topology gates

An internal topology gate couples production success to details such as:

- how many tools the model called;
- whether it delegated one or three subtasks;
- which order the calls used;
- exact intermediate prose; or
- one expected agent-to-agent handoff sequence.

Those details are nondeterministic and change as prompts, models, and providers
evolve. An artifact gate asks stable questions instead:

- Does the artifact exist?
- Does it match the required schema?
- Does it contain required evidence?
- Which gaps remain?
- Is the phase accepted, partial, failed, or skipped?

This creates a stable macro contract while leaving the phase free to improve its
internal strategy.

## Run the reference

The application and tests are fully offline:

```bash
dotnet run --project src/Examples/AgentFramework/AutonomousPhasePipelineApp

dotnet test \
  src/Examples/AgentFramework/AutonomousPhasePipelineApp.Tests
```

The tests cover success, optional failure, required failure, correction,
cancellation during both macro fan-out and non-cancelable background work,
checkpoint restore, cross-run artifact isolation, and delivery replay.

## Current scope

The reference deliberately keeps its contracts and stores example-local. It
does not introduce a generic Foundry phase, artifact, manifest, or delivery API.

The in-memory artifact store, checkpoint manager, and delivery ledger prove the
same-process architecture. A process-restart deployment must persist all three
independently. Live-provider quality and cost evidence are separate from this
deterministic contract.
