---
description: Understand Foundry's standard GitHub-hosted runner and public-repository billing boundary.
---

# GitHub-Hosted CI Runners

Foundry is a public repository. Every repository workflow uses a standard
GitHub-hosted runner with a literal label; there is no self-hosted route,
custom runner image, runner group, or repository-variable override.

GitHub's runner reference states that standard GitHub-hosted runners are free
and unlimited for public repositories. All current Foundry jobs are Linux jobs
and use `ubuntu-24.04`.

Foundry has no Windows workflow job today. If Windows-specific coverage is
added, it uses `windows-latest`. GitHub lists `windows-latest` in the Windows
row for standard public-repository runners, so it has the same free and
unlimited public-repository usage terms as `ubuntu-24.04`.

Larger runners are a different product and are always billed, including for
public repositories. Foundry's CI contract rejects unknown runner labels so a
workflow cannot silently move onto a larger or custom runner.

## Portable toolchain setup

GitHub-hosted VMs start clean. Workflows use
`.github/actions/setup-dotnet/action.yml` to validate the exact SDK contracts
in `global.json` and `.github/dotnet/sdk-9/global.json`, then install any
missing SDKs into `runner.temp`.

NativeAOT jobs install their Linux prerequisites in the workflow. This costs
startup time compared with a prebuilt image, but avoids maintaining runner
infrastructure for compute GitHub already provides without charge to public
repositories.

## Enforcement

Run the deterministic contract after changing workflows, runner labels, the
setup action, or SDK inputs:

```powershell
pwsh scripts/test-ci-contract.ps1 -SelfTest
```

The contract:

- allows only `ubuntu-24.04` and `windows-latest`;
- rejects self-hosted, custom, larger-runner, and variable-based routing;
- verifies every workflow states the public-repository runner terms;
- preserves required CI check names and release permissions; and
- validates exact, portable .NET SDK setup.

Repository workflows never invoke a live LLM. Live Copilot integration tests
are manual local operations requiring explicit opt-in and are rejected when
`GITHUB_ACTIONS=true`.

## GitHub references

- [GitHub-hosted runners reference](https://docs.github.com/en/actions/reference/runners/github-hosted-runners)
- [GitHub Actions billing](https://docs.github.com/en/billing/concepts/product-billing/github-actions)
