# Implementation Versus Plan

**Task:** T127  
**Audited:** 2026-07-30  
**Delivered head:** `08a4fcb20671428245f5004cc0cf7b8acfdcc0a5` on `main`

This audit compares delivered code, packages, public APIs, capability profiles,
and retained or deleted overlaps against
`specs/001-maf-harness-first-class/plan.md`.

## Packages delivered versus planned

| Planned | Delivered | Variance |
|---|---|---|
| `NexusLabs.Foundry.MicrosoftAgentFramework` (Lane A: MAF core only) | Delivered; contains the internal selected-provider lane | None |
| `NexusLabs.Foundry.MicrosoftAgentFramework.Harness` (Lane B: bundle only) | Delivered; references only upstream Harness plus the neutral core | None |
| `NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Tests` (candidate) | Delivered | None |
| `NexusLabs.Foundry.MicrosoftAgentFramework.Analyzers` | Unchanged; no new Harness rule added | Planned as conditional; condition not met |
| `Examples/AgentFramework/HarnessHybridApp` | Delivered as non-packable contributor-only example | None |

The plan made analyzer and generator additions conditional on demonstrated
static value. Gate G7 recorded that no candidate was both statically decidable
and non-redundant, so none shipped. That is plan-conforming, not a variance.

## Public API versus planned promotion

Verified by reflection in `evidence/api-review.md`:

- the core package exposes no public type in a `Harness` namespace, and all 176
  Harness types there are `internal`, matching the plan's Lane A intent;
- the optional bundle exposes exactly nine public types, all in
  `...Harness.Bundle`; and
- the Testing package promotes six Harness scenario types.

No planned public surface is missing, and no unplanned surface was promoted.

## Capability profiles

The delivered `HarnessCapability` set matches the plan's stable and experimental
slices. Shell is absent by design under FR-060 and is now enforced by
`HarnessShellCompositionTests`.

AOT dispositions delivered in Gate G7 remain accurate: generated tools,
function invocation, and message injection are verified in the native profile,
while Todo, AgentMode, Skills, WebSearch, FileMemory, FileAccess, experimental
hybrid compaction, background agents, and loop evaluation remain AOT-unverified.

## Retained and deleted overlaps

`evidence/duplication-ledger.md` gives all eight ledger entries a release-bound
disposition. Nothing was deleted. DUP-001, DUP-002, DUP-006, and DUP-008 are
retained under the signed G8 decision; DUP-003, DUP-004, and DUP-007 were
resolved at G2/G7; DUP-005 is a permanent invariant.

This matches the plan's Decision 13 that no existing loop, workspace,
diagnostics, or middleware surface is removed during initial integration.

## Variances

| ID | Severity | Variance | Disposition |
|---|---|---|---|
| V1 | Non-critical | The Testing package description advertised only `IAgentScenario` and `AgentScenarioRunner` and omitted the Harness scenario surface this program added. | Fixed in this gate. |

No critical variance was found, so no architecture regroup is triggered.

## Deferred by design

The plan explicitly deferred these beyond initial delivery, and they remain
deferred rather than being silent gaps:

- stable promotion of the optional bundle and Harness testing APIs;
- any durable wire format for session or history state;
- AOT verification of the remaining capability set; and
- removal of any retained overlap, which requires a larger case population and
  workload-specific parity evidence.
