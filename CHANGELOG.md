# Changelog

All notable changes to Foundry will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project uses [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Repository-owned Foundry CI runner image source and trusted GHCR publication
  workflow with exact .NET SDKs, NativeAOT prerequisites, GitHub-hosted pull
  request validation, provenance, SBOM generation, and retained digest evidence.
- Digest-pinned `foundry-ci` PitCrew profile and portable exact-SDK setup action
  that skips downloads only when every required SDK is already installed.

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
