---
name: review-changes
description: >
  Review the current Foundry diff before commit, push, or pull-request delivery
  against applicable instructions, project docs and ADRs, repository-declared
  validation, and existing CI evidence.
---

# Review Foundry changes

This skill owns review procedure, not project standards. Current instructions,
docs, ADRs, manifests, tests, scripts, and workflows remain authoritative.

## Review boundary

- Judge changed lines and their direct invariant blast radius.
- Report pre-existing divergence separately and never include it in the verdict.
- Do not demand unrelated migration work because docs describe a target state.
- Do not invent findings for a clean diff.
- Review is read-only unless the user explicitly requests fixes.

## 1. Resolve the scope

Confirm the worktree and branch:

```powershell
git rev-parse --show-toplevel
git branch --show-current
git status --short
```

Use scope in this order:

1. Explicit refs, pull request, or paths supplied by the user.
2. All uncommitted changes: unstaged, staged, and untracked.
3. Otherwise `git merge-base origin/main HEAD` through `HEAD`.

Use `git --no-pager diff`, `git --no-pager diff --cached`, and full reads for
untracked files. For a pull request, confirm the actual base and head with
`gh pr view` and `gh pr diff`.

State the selected scope and changed files.

## 2. Resolve governing sources

Resolve applicable instructions:

```powershell
pwsh scripts/guidance/Get-ApplicableInstructions.ps1 -Path <changed-paths>
```

Read every returned instruction in full.

Read `.github/foundry-guidance.json`, then use its declared `mkdocs.yml` map,
documentation root, ADR root, and review wiring. Follow relevant links from
changed docs and matching instructions. Preserve accepted ADR reasoning and
reciprocal supersession metadata.

Project instructions may specialize broad root safeguards. Do not place a
project rule in a generated or externally managed surface.

## 3. Resolve validation

Inventory declared validation and build surfaces:

```powershell
pwsh scripts/guidance/Get-ValidationInventory.ps1
```

Inspect the returned solution, projects, MSBuild and SDK contracts,
documentation inputs, workflows, runner profile, and contract scripts before
choosing commands.

- Run only the smallest offline command covering the changed behavior.
- Use project/test selectors or equivalent repository scoping when available.
- Do not invent a command the repository does not declare or document.
- Do not run complete suites, NativeAOT matrices, live-provider tests,
  credentialed checks, or broad evaluation workloads on a workstation.
- Pull-request CI and declared PitCrew routing own complete, hosted, platform,
  and expensive evidence.
- For a pull request, inspect `gh pr checks` instead of reproducing hosted work.

Record every command/result and required check that was not run.

## 4. Review what gates do not prove

Read each changed file and inspect:

- correctness, failure handling, and deterministic behavior;
- neutral-core, optional-integration, and provider dependency direction;
- middleware, tool, workflow, and trust-boundary compatibility;
- public API, package, analyzer, generator, and NativeAOT consequences;
- manifest/schema and generated-output drift;
- tests or executable contracts missing for introduced behavior;
- documentation and instruction authority;
- credentials, untrusted inputs, destructive actions, and privacy; and
- public artifacts for local filesystem, private-repository, internal-service,
  credential, or private operational context.

Use the current governing source for the exact rule. These categories are not
a second standards checklist.

## 5. Reflect on guidance

Treat review as a bounded feedback loop, not a default instruction-edit trigger.

Recommend a guidance change only when review shows either:

- one significant misstep with material risk or impact; or
- repeated evidence of the same avoidable misstep.

The lesson must be generalizable, supported by concrete evidence, and assigned
to the correct owner. Do not propose guidance for one-off, speculative,
hyper-specific, or stylistic incidents, or when current guidance or executable
checks already cover it. Prefer deterministic enforcement in scripts/tests,
instructions for recurring exact rules, docs for rationale, skills for
procedures, and `AGENTS.md` only for safeguards needed before file selection.

Review remains read-only. Report no guidance change when the threshold is not
met; do not edit guidance automatically.

## 6. Report

Open with:

- `Scope:` reviewed range or paths;
- `Verdict:` `Approve`, `Approve with nits`, or `Request changes`;
- `Validation:` observed, passed, failed, and not-run evidence; and
- `Guidance reflection:` `no change warranted` or one evidence-backed candidate.

Group introduced findings by severity:

- **Blocker** - broken behavior, security/destructive risk, failing required
  validation, or violation of an accepted architecture/delivery boundary.
- **Major** - clear correctness or contract defect that should be fixed first.
- **Minor** - bounded maintainability, coverage, or guidance defect.
- **Nit** - optional polish only.

Every finding includes:

`severity - file:line - issue - governing source - concrete fix`

If there are no introduced findings, say so plainly. State uncertainty and
missing evidence instead of implying an unrun check passed.
