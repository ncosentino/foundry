---
applyTo: ".github/workflows/*.{yml,yaml},.github/actions/**/*.{yml,yaml},global.json,.github/dotnet/**/global.json,scripts/{test-ci-contract,resolve-dotnet-sdk-contract}.ps1"
---

# CI, runner, and SDK trust boundaries

- Repository workflows must never invoke or configure a live LLM, including
  manual workflows, Copilot CLI, provider SDKs, and direct model APIs.
- Use only literal standard GitHub-hosted runner labels accepted by
  `scripts/test-ci-contract.ps1`. Linux jobs use `ubuntu-24.04`; Windows jobs
  use `windows-latest`.
- Standard GitHub-hosted runners, including `windows-latest`, are free and
  unlimited for this public repository. Larger runners are always billed and
  are prohibited unless a later accepted ADR changes this boundary.
- Do not add self-hosted routing, custom runner images, runner groups, or
  repository-variable runner overrides.
- SDK contracts use exact versions with roll-forward disabled and prereleases
  rejected. Workflows consume them through the repository setup action.
- Preserve required check names and release permissions already enforced by the
  CI contract script.
- Run `scripts/test-ci-contract.ps1 -SelfTest` after changing a workflow,
  runner label, setup action, or SDK input.
