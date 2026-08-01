---
title: "ADR-0011: Public hybrid context compaction"
status: "Accepted"
date: "2026-08-01"
authors: ["Nick Cosentino"]
tags: ["architecture", "decision", "agent-framework", "harness", "context", "compaction", "api"]
supersedes: "adr-0007-experimental-hybrid-context-compaction.md"
superseded_by: ""
---

## Context and scope

ADR-0007 built a per-provider-call hybrid compaction seam and then deliberately
withheld it: every mechanism it introduced stayed `internal`, and it explicitly
declined to approve "a public Harness runtime configuration or composition API".
That was a defensible position while the mechanism was unproven. It is no longer
defensible, for two reasons that have both since been measured rather than
assumed.

First, the gap it fills is real and is not going to close upstream. A scripted,
offline reproduction of a two-round tool loop on the public complete-bundle path,
with compaction enabled and a strategy whose trigger always fires, consults that
strategy exactly **once**, against a two-message index representing the state
before the first round. The round that carries the `FunctionCallContent` and its
`FunctionResultContent` — the round that actually grows the conversation — is
never offered to the strategy at all. Identical numbers on
`Microsoft.Agents.AI.Harness` 1.15.0 and 1.16.0. The cause is structural rather
than a defect: upstream's `CompactionProvider` is an `AIContextProvider`, and
context providers are invoked once per agent turn, not once per provider request.
See issue #73.

Second, withholding the fix has a cost that ADR-0007 did not weigh. A caller who
enables compaction on the public bundle reasonably believes their context is
bounded. Inside a long single-turn tool loop — the case compaction is most often
reached for — it is not. Foundry shipped the component that does bound it, in the
same repository, and made it unreachable.

This decision governs what becomes publicly configurable, what stays internal,
and what Foundry must disclose about both compaction paths.

## Decision drivers

- Every mechanism ADR-0007 protected is still worth protecting; none of the
  reasons it gave for keeping the *internals* internal have weakened.
- The reason it gave for keeping the *capability* unreachable — that the
  mechanism was experimental — no longer distinguishes it from the rest of the
  repository, which is entirely pre-1.0.
- A public API that demands a host-authored message classifier and snapshot
  integration with no defaults is not a usable API; it is the internal contract
  with a `public` keyword applied.
- The two compaction paths differ in a way a caller cannot discover by reading
  either API, so the difference has to be stated where a caller is already
  looking rather than in a changelog.
- A silent limitation is worse than an absent feature: a caller who believes
  context is bounded and is wrong has no signal that anything is missing.

## Decision

**Hybrid compaction is a supported, publicly configurable capability of the
complete-bundle path.** `FoundryHarnessFeatureSelections.EnableHybridCompaction`
turns it on and `FoundryHarnessAgentConfiguration.HybridCompactionOptions`
configures it. Enabling one without the other fails closed before any agent is
constructed, matching how every other opt-in dimension on that configuration
behaves.

**The public surface is the options record, not the mechanism.** Exactly one new
public type is introduced: `FoundryHarnessHybridCompactionOptions`, carrying the
byte budget, the retention and attempt bounds, and the reducer whose output is
treated as a proposal. The assembler, verifier, policy, snapshot, classifier,
diagnostics, and coordinator types ADR-0007 introduced all remain `internal`.
The bundle package reaches them through an `InternalsVisibleTo` grant, the same
first-party mechanism already used for `NexusLabs.Foundry.MicrosoftAgentFramework.Workflows`.
This is what makes exposure a decision about *capability* rather than about
surface area: a caller gains the behavior without gaining a dependency on the
shape of the machinery, which stays free to change.

**Foundry supplies defaults for the two collaborators a bundle caller cannot
reasonably author.** The classifier defaults to deriving a stable entry identity
from a content hash and declining to override the adapter's structural
classification; the snapshot integration defaults to reporting exactly the
entries adapted for that one call. Neither is a guess: the adapter already
derives every classification that carries meaning, and the per-call provider has
no source that could introduce entries mid-assembly. The budget and the reducer
stay required, because those encode the caller's own context limit and reduction
choice, which have no defensible default. The selected-provider path continues
to supply all four collaborators explicitly.

**Trust binding becomes an explicit all-or-nothing choice rather than a
requirement.** The compaction node revalidates a trusted execution identity
around assembly because ADR-0006 made `IWorkspace` authoritative and that
authority must be defended. The complete-bundle path owns no workspace, so it
supplies no binding and the node performs no revalidation — there is nothing
there to defend. A single `HarnessCompactionTrustBinding` carries the identity so
a partially-supplied one is rejected outright, rather than allowing the
selected-provider guarantee to silently degrade to an unchecked one.

**Both compaction paths must disclose their limitations through the effective
defaults report.** `FoundryHarnessFeature.Compaction` now reports the measured
per-turn boundary and names the tracking issue. A new
`FoundryHarnessFeature.HybridCompaction` reports that it is Foundry-owned rather
than upstream, that budgets are UTF-8 bytes rather than provider tokens, and that
an irreducible context fails the request instead of being forwarded over budget.
Disclosure through the report rather than only through prose matters because the
report is machine-readable and already asserted by the AOT capability scenario.

**Neither path is enabled by default, and enabling both is permitted.** They
solve different problems — upstream bounds what the agent remembers across
turns, hybrid bounds what is dispatched for one call — and a caller who wants
both should not have to choose. They are independent rather than layered:
measurement over all four combinations confirms neither suppresses the other,
and enabling hybrid does not extend upstream's reach beyond the pre-tool-loop
state.

**Hybrid compaction is non-destructive to stored history.** It is installed
inner to the per-service-call history decorator, so history is persisted before
a reduction is applied and every call re-assembles from the full record. A
reduction therefore bounds one dispatch rather than permanently discarding
conversation. The cost is that reduction work is not cumulative and the stored
record still grows, which is a reason to enable upstream compaction alongside it
rather than instead of it.

## Consequences

- A bundle caller can bound context inside a tool loop, which #73 established
  was previously impossible through any public Foundry API.
- `FoundryHarnessFeatureSelections` and `FoundryHarnessAgentConfiguration` each
  gain a required member, which is a source-breaking change for every existing
  construction site. This is deliberate and consistent with those types' existing
  contract that a caller never silently inherits a default.
- The AOT capability scenario now constructs the compaction node, so trimming and
  native-compilation coverage extends to it.
- Foundry now owns a supported public capability whose position depends on the
  verified upstream middleware order. An upstream reordering becomes a
  correctness concern for this feature, not merely an internal detail.
- Budgets being byte-denominated rather than token-denominated is a real
  usability cost, and is disclosed rather than hidden. Removing it requires a
  tokenizer matched to the provider, which is not in scope here.

## Alternatives considered

**Leave it internal and document the upstream gap.** Cheapest, and honest about
the limitation, but it leaves a caller with a named problem and no remedy while
the remedy sits unreachable in the same package. Rejected.

**Make the whole compaction surface public.** Would expose roughly forty-seven
internal types, freeze the shape of machinery that is still moving, and hand
callers a classifier and snapshot contract they have no basis to implement.
Rejected as exposing surface area rather than capability.

**Wait for an upstream fix.** The behavior is unchanged across 1.15.0 and 1.16.0
and follows from the `AIContextProvider` contract rather than from a defect, so
there is no reason to expect it to change on a timeline worth blocking on.
Rejected, though #73 remains open to track any upstream movement.

**Enable hybrid compaction by default when budgets are supplied.** Would silently
change behavior for existing configurations and quietly reinterpret budgets from
tokens to bytes. Rejected in favor of an explicit opt-in.
