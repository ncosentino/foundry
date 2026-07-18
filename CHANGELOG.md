# Changelog

All notable changes to Foundry will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project uses [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

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

### Changed

- AI and agentic package IDs and namespaces now use the
  `NexusLabs.Foundry.*` prefix.
- Needlr is an optional integration dependency rather than a dependency of
  Foundry's neutral runtime and provider packages.

[Unreleased]: https://github.com/ncosentino/foundry/commits/main
