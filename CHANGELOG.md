# Changelog

All notable changes to Foundry will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project uses [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Optional `NexusLabs.Foundry.MicrosoftAgentFramework.Harness` package that
  builds the official upstream `Microsoft.Agents.AI.Harness` complete-bundle
  pipeline from fully explicit configuration and reports requested-versus-
  effective bundle defaults. The package is opt-in: the core
  `NexusLabs.Foundry.MicrosoftAgentFramework` package never references it, and
  no other Foundry package acquires it transitively.
- Harness scenario testing surface (`IHarnessScenario`,
  `HarnessScenarioRunner`, and their context and result types) in
  `NexusLabs.Foundry.MicrosoftAgentFramework.Testing`, so Harness scenarios can
  be authored without referencing the optional bundle.
- Foundry workspace authority for Harness runs, including eager artifact
  offload, selective rehydration, and content-addressed artifact references.
- Harness diagnostics progress events for approvals, artifact offload and
  rehydration, and context composition and compaction.
- Public per-provider-call hybrid context compaction on the Harness bundle
  (`FoundryHarnessFeatureSelections.EnableHybridCompaction` and
  `FoundryHarnessAgentConfiguration.HybridCompactionOptions`). Unlike upstream
  compaction, it bounds the exact message set dispatched for every provider
  request including each intermediate tool round, and fails closed rather than
  forwarding over-budget context. See ADR-0011.
- Optional `NexusLabs.Foundry.MicrosoftAgentFramework.Workflows.Declarative` package
  that runs Microsoft Agent Framework declarative (YAML) workflows against
  Foundry-registered agents, with no dependency on a deployed Azure AI Foundry
  project. Includes deliberate document validation and a bridge from the upstream
  workflow event stream onto Foundry progress reporting. The package is excluded from
  the NativeAOT profile because its Power Fx and Power Platform dependencies are not
  AOT compatible. See `docs/declarative-workflows.md`.
- NativeAOT Harness profile with source-generated tools, no reflection fallback,
  and a published-and-executed native application in CI.
- Repository-owned Foundry CI runner image source and trusted GHCR publication
  workflow with exact .NET SDKs, NativeAOT prerequisites, GitHub-hosted pull
  request validation, provenance, SBOM generation, and retained digest evidence.
- Digest-pinned `foundry-ci` PitCrew profile and portable exact-SDK setup action
  that skips downloads only when every required SDK is already installed.

### Changed

- The Harness bundle owns the tool-invocation loop and OpenTelemetry for
  complete-bundle agents; Foundry emits no duplicate spans or metrics for that
  profile.
- `FoundryHarnessFeatureSelections` and `FoundryHarnessAgentConfiguration` each
  gained a required member for hybrid compaction, which is source-breaking for
  existing construction sites. Set `EnableHybridCompaction = false` and
  `HybridCompactionOptions = null` to keep prior behavior.
- The effective-defaults report now discloses that upstream compaction is
  evaluated once per agent turn rather than once per provider request, so it
  does not bound context inside a multi-round tool loop. Measured against
  `Microsoft.Agents.AI.Harness` 1.15.0 and 1.16.0 and tracked in
  [#73](https://github.com/ncosentino/foundry/issues/73).

### Deprecated

- Nothing. The iterative loop, plain Harness, and hybrid execution modes are all
  retained. See
  [ADR-0010](docs/adr/adr-0010-harness-execution-mode-retention.md).

### Migration

- **No compatibility shim exists.** The former
  `NexusLabs.Foundry.MicrosoftAgentFramework` alpha Harness APIs, the former
  `NexusLabs.Foundry.Copilot` alpha APIs, and the former
  `NexusLabs.Foundry.Needlr.SemanticKernel` alpha APIs are not shimmed. Update
  call sites directly.
- **Adopting the bundle is additive.** Existing Foundry MAF agents keep working
  unchanged. Add the optional Harness package only when you want the upstream
  complete bundle.
- **Selected-provider composition is internal and unsupported.** Public
  consumers choose plain Foundry MAF agents, the optional Harness bundle, or the
  Foundry iterative loop. Do not depend on the internal seam.
- **Shell is a separate opt-in package.** Upstream `HarnessAgentOptions` exposes
  no shell property and Foundry does not add one. Reference a shell package
  yourself and pass shell commands as ordinary tools or from a context provider.
- **The optional bundle and Harness testing APIs are prerelease.** They may
  change before a stable release.

## [0.1.0-alpha.1] - 2026-07-19

### Added

- Independent Foundry package family for agent orchestration, evaluation,
  observability, provider integrations, and deterministic testing.
- Dependency-injection-neutral Microsoft Agent Framework runtime and workflow
  packages.
- Optional Needlr integration packages for Microsoft Agent Framework and
  Semantic Kernel.
- Provider-neutral experiment execution with MEAI Reporting and Langfuse
  adapters.
- GitHub Copilot `IChatClient` integration.
- Source generators, analyzers, examples, and architecture decision records.
- NativeAOT validation, 30 compile-validated examples, and deterministic testing
  support for generated functions, agents, and workflows.
- Versioned API documentation with development, stable, and immutable release
  references.

### Changed

- AI and agentic package IDs and namespaces now use the
  `NexusLabs.Foundry.*` prefix.
- Needlr is an optional integration dependency rather than a dependency of
  Foundry's neutral runtime and provider packages.

[Unreleased]: https://github.com/ncosentino/foundry/compare/v0.1.0-alpha.1...HEAD
[0.1.0-alpha.1]: https://github.com/ncosentino/foundry/releases/tag/v0.1.0-alpha.1
