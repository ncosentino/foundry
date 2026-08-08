---
applyTo: ".github/workflows/*.{yml,yaml},.github/actions/**/*.{yml,yaml},.github/runner-images/**/*,.pitcrew/runner-profile.json,global.json,.github/dotnet/**/global.json,scripts/{test-runner-image,test-runner-profile,resolve-dotnet-sdk-contract}.ps1"
---

# CI, runner, and SDK trust boundaries

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
