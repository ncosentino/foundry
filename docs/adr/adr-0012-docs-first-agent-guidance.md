---
title: "ADR-0012: Docs-first agent guidance"
status: "Accepted"
date: "2026-08-08"
authors: ["Nick Cosentino"]
tags: ["architecture", "decision", "documentation", "agents"]
supersedes: ""
superseded_by: ""
---

# ADR-0012: Docs-first agent guidance

## Context and scope

Foundry already has a compact root `AGENTS.md`, a complete MkDocs navigation
map, accepted ADRs, and executable package, runner, SDK, and workflow
contracts. Its agent-guidance structure is incomplete: exact C# and package
rules live in the always-loaded root, no path-scoped instructions exist, root
redirects are missing, and there is no repository-local review procedure or
structural guidance gate.

The custom specialist agents also demonstrate the failure mode of maintained
inventories in prose. Their repository descriptions and package-version tables
can become stale independently of the manifests and source they describe.

This decision governs Foundry's contributor-facing agent entrypoints, scoped
instructions, guidance documentation, review procedure, and structural
validation. It does not change Foundry's product architecture, replace
existing engineering ADRs, adopt a generated guidance subtree, or define the
quality and roster of specialist agents.

## Decision drivers

- Keep always-loaded context small and stable.
- Deliver exact rules only when their file population is being edited.
- Preserve Foundry's existing documentation, ADR, CI, and validation systems.
- Give architecture, procedure, and deterministic enforcement distinct owners.
- Prevent live package and project inventories from drifting in prose.
- Preserve public-repository privacy and CI trust boundaries.
- Make guidance structure and context cost mechanically testable.

## Decision

Foundry adopts a project-owned, docs-first agent-guidance architecture.

`AGENTS.md` remains the root entrypoint with a hard budget of 60 lines and
3,072 UTF-8 bytes. It owns project identity, source routing, cross-cutting
architecture and privacy safeguards, and delivery routing. Exact C#, package,
CI, documentation, ADR, custom-agent, and guidance-system rules move to
path-scoped instructions under `.github/instructions/`.

`CLAUDE.md` is a one-line redirect to `AGENTS.md`.
`.github/copilot-instructions.md` is a minimal pointer to the same root.

`mkdocs.yml` remains the canonical documentation map. Foundry does not create a
parallel docs index or replace its current public documentation structure.
`docs/agent-guidance.md` explains the ownership model and authoring rules.
Accepted ADRs continue to preserve decision history and are superseded rather
than rewritten.

The project-owned `.github/foundry-guidance.json` contract declares root,
instruction, matched-context, documentation, ADR, and review wiring. It does
not establish a Genesis-managed subtree. Project guidance remains editable and
owned by Foundry.

The repository-local `.github/skills/review-changes` skill owns review
procedure only. It resolves the current diff, applicable instructions,
documentation and ADRs, repository-declared validation, and hosted evidence.
It does not maintain another standards checklist or a hardcoded path-to-test
table.

PowerShell guidance scripts follow the repository's existing executable
contract pattern. `scripts/guidance/Test-Guidance.ps1` validates budgets,
redirects, documentation reachability, ADR metadata and lifecycle,
instruction structure and matched context, and review wiring. Its self-test
uses controlled negative fixtures. CI runs this contract before expensive
build work.

Foundry creates no managed instruction subtree and no generated Claude mirror.
A future generated surface requires its own owner and evidence.

## Alternatives considered

### Keep the current root-only guidance

This preserves the smallest file count and the current compact root. It was
rejected because exact C# and package rules remain permanently loaded, no
matching-file delivery exists, and review and structure remain unenforced.

### Copy Genesis-managed generated-project guidance

This would reuse a complete template and managed instruction namespace. It was
rejected because Foundry has no exact generated-template provenance and its
guidance, docs, ADRs, CI, and specialist agents are project-owned. Managed
replacement would create the wrong ownership boundary.

### Put all standards in the review skill

This would keep recurring context small and centralize review. It was rejected
because the skill would become a second standards corpus and matching edits
would not receive exact rules until review time.

### Put all guidance in documentation

This would preserve human-readable rationale with almost no recurring context.
It was rejected because compliance would depend on an agent choosing the right
page before making a matching edit.

## Consequences

### Positive

- Root guidance remains below its budget and gains explicit privacy safeguards.
- C# files, package metadata, workflows, docs, ADRs, and agent definitions
  receive narrow rules when edited.
- Foundry preserves its complete MkDocs map and existing executable contracts.
- Review derives current validation and sources instead of duplicating them.
- Structural drift and excessive matching context fail deterministically.
- Stale package-version tables are removed from specialist-agent guidance.

### Negative

- Foundry owns additional instructions, scripts, a review skill, and a guidance
  contract.
- Guidance changes require maintaining representative paths and negative
  fixtures.
- Documentation and CI changes must preserve both product behavior and the
  guidance contract.

### Neutral

- Existing product ADRs and their reasoning remain unchanged.
- Complete builds, test suites, NativeAOT, platform, and credentialed evidence
  remain owned by existing CI.
- Specialist-agent roster and evaluation quality remain separate concerns.

## Confirmation

The decision is confirmed when:

- the root and redirects meet declared budgets;
- every instruction has valid frontmatter and representative matched context is
  below the hard ceiling;
- every maintained documentation page and ADR is reachable from `mkdocs.yml`;
- ADR lifecycle metadata is reciprocal and valid;
- the review skill resolves current instructions, docs, ADRs, validation, and
  hosted evidence;
- controlled negative fixtures fail for the intended structural reason; and
- CI runs the guidance contract before build, test, package, and NativeAOT work.

Exact prose is not the contract. The gate protects ownership, loading shape,
discovery, lifecycle, and deterministic wiring.

## References

- `AGENTS.md` is the always-loaded project entrypoint.
- `mkdocs.yml` is the complete public documentation navigation map.
- `scripts/test-runner-image.ps1` and
  `scripts/test-runner-profile.ps1` demonstrate Foundry's executable contract
  and negative-fixture pattern.
- ADR-0009 records the CI runner trust boundaries that scoped CI guidance must
  preserve.
