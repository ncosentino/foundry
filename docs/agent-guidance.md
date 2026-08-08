---
description: Understand where Foundry's agent guidance lives, how it is validated, and which source owns each kind of rule.
---

# Agent Guidance

Foundry uses layered, project-owned guidance. Each surface has one purpose so
agents receive exact rules when they matter without loading the complete
engineering handbook for every task.

## Authority by surface

| Surface | Responsibility |
| --- | --- |
| `AGENTS.md` | Project identity, source routing, cross-cutting safeguards, and delivery routing |
| `CLAUDE.md` and Copilot instructions | Minimal redirects to `AGENTS.md` |
| `.github/instructions/` | Exact recurring rules for one matching file population |
| `mkdocs.yml` and `docs/` | Documentation map, architecture, rationale, tradeoffs, and current behavior |
| `docs/adr/` | Accepted costly-to-reverse decisions and their lifecycle |
| `.github/skills/review-changes` | On-demand review procedure |
| Code, manifests, schemas, tests, and workflows | Executable truth and deterministic enforcement |

When sources disagree, investigate the executable contract first. Scoped
instructions own the exact rule for matching edits, accepted ADRs own
significant decisions, and maintained documentation owns architecture and
rationale.

## Root guidance

`AGENTS.md` is intentionally small because it loads before a file is selected.
It remains within 60 lines and 3,072 UTF-8 bytes and contains only guidance
that cannot reliably be scoped to one file population.

The root redirects do not carry another copy of project rules:

- `CLAUDE.md` contains only `@AGENTS.md`.
- `.github/copilot-instructions.md` points to `AGENTS.md`.

## Scoped instructions

Instructions use `applyTo` frontmatter and target one coherent population.
Their body contains the minimum normative rule and failure prevention required
for a matching edit.

Do not:

- import or depend on another instruction file;
- copy live package, project, workflow, or provider inventories into prose;
- place architecture history or lengthy rationale in recurring context; or
- broaden a glob merely to make a rule easier to find.

Use the resolver to inspect the exact context for one or more paths:

```powershell
pwsh scripts/guidance/Get-ApplicableInstructions.ps1 `
  -Path src/NexusLabs.Foundry.MicrosoftAgentFramework/AgentFactory.cs
```

The machine-readable contract in `.github/foundry-guidance.json` declares
budgets and representative paths. An individual instruction above 100 lines or
8 KiB requires explicit review evidence. Representative matching context
targets at most 300 lines or 16 KiB and must remain below 600 lines or 32 KiB.

## Documentation and ADRs

`mkdocs.yml` is the canonical documentation map. Every maintained Markdown
page under `docs/` is reachable from its navigation, and the strict MkDocs
build validates the public documentation site.

Documentation states current truth or an explicit target state. Accepted ADR
reasoning remains immutable decision evidence; material changes use a new ADR
with reciprocal supersession metadata.

## Review procedure

The repository-local `review-changes` skill:

1. resolves the actual diff;
2. loads instructions that match changed paths;
3. follows `mkdocs.yml`, relevant docs, and ADRs;
4. discovers validation from repository manifests, scripts, and workflows;
5. runs only targeted local checks;
6. leaves broad, hosted, platform, and credentialed evidence with CI; and
7. checks public artifacts for private or machine-specific context.

The skill owns procedure, not another copy of Foundry's standards.

## Structural validation

Run the guidance contract and its negative fixtures with:

```powershell
pwsh scripts/guidance/Test-Guidance.ps1 -SelfTest
```

The validator checks root budgets, redirects, docs navigation, ADR metadata and
lifecycle, instruction metadata and context size, review-skill wiring, and
representative negative cases. CI runs the same contract before expensive
build work.

## Content placement

Use these questions in order:

1. Can code, a schema, or a test enforce the behavior? Put it there.
2. Must the rule arrive for every matching edit? Put the minimum in an instruction.
3. Is it a multi-step procedure? Put it in a skill.
4. Is it architecture, rationale, or a tradeoff? Put it in documentation.
5. Is it a significant hard-to-reverse decision? Record an ADR.
6. Is it required before any file is selected? Only then use `AGENTS.md`.
