# Changelog

All notable changes to Foundry will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project uses [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- `DeclarativeWorkflowHandlers`, wiring the MCP tool and HTTP request handlers a
  declarative document may call out through. Both members are required, so a host
  states whether documents may reach those endpoints rather than defaulting into
  it. Foundry supplies no implementation of either: upstream's
  `DefaultMcpToolHandler` lives in `Microsoft.Agents.AI.Workflows.Declarative.Mcp`,
  and taking that dependency would put `ModelContextProtocol` in front of every
  declarative consumer. An `InvokeMcpTool` action without a wired handler is
  rejected while the workflow is built, so `ValidateDeclarativeWorkflow` reports
  it.
- `FoundryAgentAttribute.Name`, declaring the name an agent is published under
  independently of its class name. The published name is the key
  `IAgentFactory.CreateAgent(string)` resolves — including from a declarative
  workflow document — the value of `AIAgent.Name` and therefore the author of the
  messages the agent produces, the `gen_ai.agent.name` telemetry dimension, and the
  key a hosted agent is registered under for DevUI. All of those resolve it through
  one place, `FoundryAgentName`, rather than each deriving it from `Type` separately.
- `FDRYMAF031`, reporting two `[FoundryAgent]` classes that publish the same name at
  compile time rather than leaving it to `BuildAgentFactory()` at startup.
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

### Fixed

- `NexusLabs.Foundry.Copilot` now publishes cleanly with NativeAOT. Tool schemas
  stay as `JsonElement`, function arguments and known result shapes use
  source-generated `JsonTypeInfo`, and unknown CLR result types fall back to
  `ToString()` rather than reflection serialization. The new `AotCopilotApp`
  is published and executed in CI and covers a complete two-request tool loop
  with a dictionary-valued result.
- Corrected the Copilot comparison guidance after first-party integration
  research. `NexusLabs.Foundry.Copilot` is an `IChatClient`; Microsoft's package
  exposes a CLI-backed `AIAgent`, not another chat client. The two paths also
  have different model catalogs: the direct API accepts `gpt-4.1`, while the
  SDK runtime should use `auto` or a value returned by `ListModelsAsync()`.
- The Copilot client and runnable examples now default to `gpt-4.1` instead of
  the retired `claude-sonnet-4.5`, which the Copilot API rejects with
  `model_not_supported`. Callers that set `ChatOptions.ModelId` or
  `CopilotChatClientOptions.DefaultModel` explicitly are unchanged.
- Source-generated `[AgentFunction]` wrappers now honor MEAI's experimental
  `AIFunctionNameAttribute` and `AIParameterNameAttribute`, matching the
  reflection path's existing behavior. The published function name, JSON schema
  keys, required-property list, and invocation argument lookup now come from the
  declared contract rather than always from C# identifiers. `FDRYMAF032` rejects
  blank published names, `FDRYMAF033` rejects unconditional collisions, and
  `IAgentFactory` rejects cross-type collisions against the actual tool set an
  agent resolves. This is a behavioral break for an existing unscoped agent that
  receives two same-named tools: creating the agent or resolving its tools now
  throws instead of forwarding an ambiguous tool list to the model. Scope the
  agent's `FunctionTypes`/`FunctionGroups`, or publish a distinct name for one
  method.
- `NexusLabs.Foundry.MicrosoftAgentFramework` now references
  `Microsoft.Extensions.AI` and `Microsoft.Extensions.AI.Abstractions` directly
  instead of receiving them through `Microsoft.Agents.AI`. That dependency floors
  MEAI rather than pinning it, and central package management only applies to
  packages a project actually references, so the centrally managed version was
  silently ignored for the core package and everything downstream of it: the
  props file said 10.8.3 while the resolved assemblies were 10.7.0. Consumers of
  the published package were likewise resolving whatever the Agent Framework's
  floor happened to be rather than the version Foundry tests against.

### Changed

- Upgraded all Microsoft Agent Framework packages to 1.17.0, including the
  optional declarative MCP package and the corresponding DevUI/Hosting preview
  line. The Harness middleware-order pin moved to `maf-1.17.0` after the
  per-provider-call compaction, service-discovery, defaults, telemetry, and
  stream-lifecycle suites passed unchanged. MAF 1.17 also changes declarative
  invocation so an agent response carrying top-level `ErrorContent` fails the
  workflow before downstream actions run; Foundry now pins that behavior with
  a provider-neutral test.
- Upgraded Microsoft Agent Framework to 1.16.0 (from 1.15.0),
  Microsoft.Extensions.AI to 10.8.3 and its Evaluation packages to 10.8.0 (from
  10.6.0), and the OpenAI SDK to 2.12.0 (from 2.10.0). No Foundry source change
  was required; upstream shipped no breaking changes across this range. The
  Harness middleware-order pin moved to `maf-1.16.0` after re-proving the
  ordering ADR-0008 requires: the hybrid compaction node is still observed on
  every intermediate tool round and every message-injection-driven extra call.
  Foundry's existing workarounds all remain necessary — upstream compaction is
  still evaluated once per agent turn, `FunctionInvokingChatClient` still turns
  `FunctionInvoker` exceptions into tool-error results, and the declarative
  workflow package is still not NativeAOT compatible.
- Agents are addressed by their published name rather than always by their class
  name. Behaviour is unchanged for agents that do not declare
  `[FoundryAgent(Name = "...")]`, since the published name defaults to the class name.
  Declaring one *replaces* the class-name alias rather than adding a second alias, so
  `CreateAgent("<ClassName>")` stops resolving for that agent; the fully-qualified type
  name always resolves. Adding `Name` to an existing agent is therefore a breaking
  change for its own callers, by design — there is one published name, not two.
- A failed `IAgentFactory.CreateAgent(string)` lookup now lists the registered
  published names.
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
  `Microsoft.Agents.AI.Harness` 1.15.0, 1.16.0, and 1.17.0 and tracked in
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
