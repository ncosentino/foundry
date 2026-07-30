# Public API Review — Harness Delivery

**Task:** T124  
**Reviewed:** 2026-07-30  
**Scope:** every package touched by the Harness program.

## Method

Public surface was enumerated by reflection over the Release build
(`Assembly.GetExportedTypes()`), and XML documentation coverage was verified by
comparing each exported type and declared public member against the generated
`.xml` documentation file. Compiler-generated record members (`Equals`,
`GetHashCode`, `ToString`, `Deconstruct`, `PrintMembers`, `op_*`, `value__`) and
implicit constructors are excluded because the compiler emits them without
author-supplied signatures.

## Promotion summary

| Package | Public types | Harness-related promotion |
|---|---:|---|
| `NexusLabs.Foundry.MicrosoftAgentFramework` | 202 | Diagnostics and workspace contracts only |
| `NexusLabs.Foundry.MicrosoftAgentFramework.Harness` | 9 | Complete optional bundle surface |
| `NexusLabs.Foundry.MicrosoftAgentFramework.Testing` | 19 | Six Harness scenario types |

## Core package: internal by default

The core package exposes **no public type in a `Harness` namespace**. All 176
Harness types in that assembly are `internal`, which preserves the G6/G7
disposition that selected-provider composition is an unsupported internal seam.

The only publicly promoted Harness-named types are progress events in
`NexusLabs.Foundry.MicrosoftAgentFramework.Progress`:

- `HarnessApprovalRequestedEvent`, `HarnessApprovalApprovedEvent`,
  `HarnessApprovalRejectedEvent`, `HarnessApprovalStandingReauthorizedEvent`;
- `HarnessArtifactOffloadDecisionEvent`,
  `HarnessArtifactRehydrationDecisionEvent`;
- `HarnessContextCompactionStartedEvent`,
  `HarnessContextCompactionCompletedEvent`,
  `HarnessContextCompactionTerminatedEvent`; and
- `HarnessContextComposedEvent`.

These are deliberate promotions. They are diagnostics contracts consumed by
hosts and evaluation, not composition entry points, and they carry no
`Microsoft.Agents.AI.Harness` types in their signatures.

`Workspace` and `Context` public types (`IWorkspace`, `WorkspacePath`,
`IAgentExecutionContext`, and related result records) predate the Harness
program and remain intentionally public as the neutral authority surface.

## Optional bundle package: intentional and complete

All nine public types live in
`NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Bundle`:

| Type | Kind | Justification |
|---|---|---|
| `FoundryHarnessAgentConfiguration` | record | The only construction input; every property is `required`, so no default is hidden |
| `FoundryHarnessAgentFactory` | class | The only construction entry point |
| `FoundryHarnessEffectiveDefaults` | record | Requested-versus-effective reporting |
| `FoundryHarnessFeatureDisposition` | record | Per-feature disposition detail |
| `FoundryHarnessFeatureSelections` | record | Explicit feature opt-ins |
| `FoundryHarnessFeature` | enum | Feature identity |
| `FoundryHarnessFeatureRequestedState` | enum | Requested state |
| `FoundryHarnessFeatureEffectiveState` | enum | Effective state, including unavoidable |
| `FoundryHarnessFeatureBackingSelection` | enum | Upstream-default versus caller-supplied backing |

No internal candidate was found that should have been promoted, and no promoted
type is a leaked implementation detail. `FoundryHarnessAgentConfiguration` uses
`required` members and explicit `null` rather than optional parameters,
satisfying the constitution's API discipline rule.

## Testing package: six promoted scenario types

`IHarnessScenario`, `HarnessScenarioRunner`, `HarnessScenarioRunResult`,
`HarnessScenarioAgentContext`, `HarnessScenarioVerificationContext`, and
`HarnessScenarioToolResolutionException` are promoted so consumers can author
Harness scenarios without referencing the optional bundle from the shipping
Testing package.

## XML documentation findings

| Assembly | Public types | Undocumented author-written members |
|---|---:|---:|
| `...MicrosoftAgentFramework.Harness` | 9 | 0 |
| `...MicrosoftAgentFramework.Testing` | 19 | 7 (fixed) |

One real gap was found and fixed in this gate: the positional record
`ScenarioRunResult` in
`src/NexusLabs.Foundry.MicrosoftAgentFramework.Testing/AgentScenarioRunner.cs`
documented its type but none of its seven positional parameters. Each parameter
now has a `<param>` element.

The remaining reported "missing" entries were implicit record constructors,
which the compiler generates and which carry no author-supplied signature.

## Shell disposition

FR-060 is satisfied and now enforced by
`src/NexusLabs.Foundry.MicrosoftAgentFramework.Tests/Harness/HarnessShellCompositionTests.cs`:

- `HarnessCapability` exposes no shell toggle;
- no `.csproj`, `Directory.Build.*`, or `.targets` file in `src` references a
  shell package, which covers the shared MSBuild files where this repository
  centralizes most `PackageReference` items;
- `src/Directory.Packages.props` pins no shell package; and
- `docs/maf-harness.md` documents the absent `HarnessAgentOptions` shell
  property and the manual tool/context-provider composition path.

A separate test asserts that the scan set actually includes both the shared
`Directory.Build.props` and project files, so the boundary check cannot silently
degrade into a vacuous pass.

## Disposition

**PASS.** Every promoted public member is intentional and documented. The single
documentation gap found was corrected inside this gate rather than deferred.
