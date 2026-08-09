---
applyTo: ".github/workflows/*.{yml,yaml},.github/actions/**/*.{yml,yaml},.github/runner-images/**/*,.pitcrew/runner-profile.json,global.json,.github/dotnet/**/global.json,scripts/{test-runner-image,test-runner-profile,resolve-dotnet-sdk-contract}.ps1"
---

# CI, runner, and SDK trust boundaries

- Automated triggers must never invoke a live LLM, directly or indirectly.
- A live-LLM workflow must be manual-only, run exactly on the `foundry-ci`
  PitCrew runner, require an explicit approval input, and reject every actor or
  triggering actor other than `ncosentino`.
- The only permitted live-LLM process in a workflow is GitHub Copilot CLI.
  Direct model APIs, provider SDKs, and live evaluation applications are
  prohibited even in manual workflows.
- Agents must not dispatch a permitted workflow without explicit user approval
  for that specific run.
- Untrusted fork pull requests must use GitHub-hosted infrastructure before any
  repository-variable runner override is considered.
- Runner-image validation and publication remain GitHub-hosted; pull requests
  must never receive the trusted publication path.
- SDK contracts use exact versions with roll-forward disabled and prereleases
  rejected. Workflows consume them through the repository setup action.
- Preserve required check names and release permissions already enforced by the
  runner contract scripts.
- Run the affected `scripts/test-runner-*.ps1 -SelfTest` contract after changing
  its runner, workflow, image, profile, or SDK inputs.
- Do not place repository source, credentials, registration data, or workload
  output in runner images or profiles.
