# Foundry - AI Agent Instructions

Foundry is an AI and agentic application framework for .NET. It owns agent
orchestration, evaluation, provider integrations, observability, and testing.
Needlr is an optional dependency-injection integration, not the Foundry core.

## Sources of truth

- Use the README, `mkdocs.yml`, project documentation, and accepted ADRs for
  identity, architecture, and rationale.
- Path-scoped files under `.github/instructions/` own exact rules for matching
  edits.
- Code, manifests, schemas, tests, and workflows are executable truth.

## Architecture safeguards

- Keep neutral Foundry packages dependency-injection neutral. Optional Needlr
  and provider integrations must not leak into neutral package boundaries.
- Provider integrations depend on neutral Foundry abstractions, never the
  reverse.
- Keep Microsoft Agent Framework, Microsoft.Extensions.AI, Langfuse, Copilot,
  Semantic Kernel, and Needlr integrations independently replaceable.
- Generated neutral code must not require Needlr.
- Do not add compatibility shims for former unshipped alpha APIs.

## Operating safeguards

- Work from evidence and state material assumptions and tradeoffs.
- Record costly-to-reverse architecture decisions in `docs/adr/`; preserve
  accepted records and supersede them explicitly.
- Never commit credentials, tokens, live identifiers, or private environment
  values.
- Before publishing from this public repository, remove local filesystem,
  private-repository, and internal operational context from public artifacts.

## Delivery

- Use feature branches and pull requests to the default branch.
- Run targeted checks while iterating. Complete, hosted, platform, and broad
  evaluation evidence belongs to configured CI and PitCrew capacity.
- Before delivery, run `.github/skills/review-changes`.
