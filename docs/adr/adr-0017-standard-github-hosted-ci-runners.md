---
title: "ADR-0017: Standard GitHub-hosted CI runners"
status: "Accepted"
date: "2026-08-21"
authors: ["Nick Cosentino"]
tags: ["architecture", "decision", "ci", "github-actions", "runners", "billing"]
supersedes: "adr-0009-repository-owned-ci-runner-image.md"
superseded_by: ""
---

# ADR-0017: Standard GitHub-hosted CI runners

## Context and scope

ADR-0009 introduced a repository-owned runner image and a self-hosted runner
profile to reduce repeated SDK and NativeAOT setup on ephemeral workers.
Foundry is a public repository, and GitHub documents standard GitHub-hosted
runners as free and unlimited for public repositories. Maintaining separate
runner capacity therefore adds infrastructure, routing, image-publication, and
trust-boundary complexity without reducing GitHub Actions runner charges.

GitHub's public-repository runner table explicitly includes both
`ubuntu-24.04` and `windows-latest`. Larger runners are a separate product and
are always billed, including for public repositories.

This decision governs repository workflow runner selection, portable toolchain
setup, and live-model execution boundaries. It does not prohibit developers
from running deterministic repository commands on their own machines.

## Decision drivers

- Use the free public-repository compute GitHub already provides.
- Make the runner class visible from each workflow without repository settings.
- Prevent a variable or custom label from silently selecting paid capacity.
- Preserve exact SDK, required-check, release-permission, and fork-isolation
  behavior.
- Keep live-model execution out of repository CI/CD.
- Make future Windows workflow selection unambiguous.

## Decision

Every Foundry workflow uses a literal standard GitHub-hosted runner label.
Linux jobs use `ubuntu-24.04`. A future Windows job uses `windows-latest`.
Both labels are in GitHub's standard public-repository runner table and inherit
the documented free and unlimited public-repository usage terms.

Foundry does not use:

- self-hosted runners or runner groups;
- repository-variable runner overrides;
- repository-owned runner images or profiles;
- GitHub-hosted larger runners; or
- repository workflows that invoke a live LLM.

The repository setup action remains responsible for validating and installing
the exact SDKs requested by each job. NativeAOT jobs install their Linux
prerequisites on the hosted VM.

`scripts/test-ci-contract.ps1` enforces the allowed runner labels, rejects the
removed routing mechanisms, preserves required check names and release
permissions, and exercises `windows-latest` as an accepted mutation. The
guidance contract rejects every live-LLM workflow. Live Copilot integration
tests may run only as explicitly approved local operations and skip when
`GITHUB_ACTIONS=true`.

## Alternatives considered

### Keep the self-hosted runner path

Prebuilt workers can reduce setup time and provide direct capacity control.
They also require external hosts, image and profile lifecycle management,
registration trust, routing variables, and operator recovery. That cost is not
justified while standard public-repository runners are free and unlimited.

### Keep a runner override with a hosted default

A repository variable would preserve deployment flexibility. It would also
hide the actual runner class from review and allow a settings change to route
jobs to custom or paid capacity without changing workflow source. Literal
labels provide a stronger and simpler contract.

### Use GitHub-hosted larger runners

Larger runners could shorten broad builds and NativeAOT publication. GitHub
states that larger runners are always billed, even for public repositories.
Foundry prefers the standard runner unless measured evidence later justifies a
separate accepted decision.

## Consequences

### Positive

- Standard Linux and Windows capacity is free and unlimited for this public
  repository.
- Pull requests, trusted pushes, documentation, and releases use the same
  GitHub-managed trust boundary.
- Runner selection is reviewable directly in workflow source.
- No custom runner image, profile, or host requires maintenance.

### Negative

- Clean hosted VMs reinstall missing SDKs and NativeAOT prerequisites.
- Jobs may take longer than equivalent prebuilt workers.
- Repository CI cannot host live-model evaluation.

### Neutral

- Current workflows remain Linux-only.
- Public package, artifact, cache, and release-asset policies remain separate
  from runner selection.
- Required check names and release publishing behavior remain unchanged.

## Confirmation

The decision is confirmed when:

- every workflow uses only `ubuntu-24.04` or `windows-latest`;
- the CI contract and guidance self-tests pass;
- the repository runner override is no longer used;
- the self-hosted profile, custom image, image workflow, and live-model
  workflow are absent; and
- required pull-request checks pass on GitHub-hosted runners.

## References

- [GitHub-hosted runners reference](https://docs.github.com/en/actions/reference/runners/github-hosted-runners)
- [GitHub Actions billing](https://docs.github.com/en/billing/concepts/product-billing/github-actions)
- `docs/github-hosted-runners.md`
- `scripts/test-ci-contract.ps1`
