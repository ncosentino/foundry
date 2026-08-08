---
applyTo: "AGENTS.md,CLAUDE.md,.github/copilot-instructions.md,.github/instructions/**/*.instructions.md,.github/skills/review-changes/SKILL.md,.github/foundry-guidance*.json,scripts/guidance/**/*.ps1,docs/agent-guidance.md"
---

# Agent guidance system

- Keep `AGENTS.md` within 60 lines and 3,072 UTF-8 bytes. It owns only project
  identity, routing, and safeguards needed before a file is selected.
- `CLAUDE.md` is the one-line `@AGENTS.md` redirect, and Copilot instructions
  remain a minimal pointer.
- One instruction targets one coherent file population and contains only the
  minimum normative rule and failure prevention required for matching edits.
- Do not import other instructions or maintain live inventories in prose.
- Put architecture and rationale in docs, significant decisions in ADRs,
  multi-step procedures in skills, and deterministic behavior in scripts/tests.
- Run `scripts/guidance/Test-Guidance.ps1 -SelfTest` after changing this system.
