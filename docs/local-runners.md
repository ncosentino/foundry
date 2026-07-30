# Local CI Runners

Foundry routes trusted Linux jobs to
[PitCrew](https://github.com/ncosentino/pitcrew) using its default
`general-purpose` profile:

```yaml
runs-on: [self-hosted, linux, x64, general-purpose]
```

Provision repository-scoped capacity from a PitCrew checkout:

```powershell
.\Setup-Runner.ps1 `
    -Repos https://github.com/ncosentino/foundry=2
```

The workflow uses the `CI_RUNNER` repository variable as PitCrew's manual
cloud fallback. Leave it unset for local ephemeral runners, or set it to
`ubuntu-latest` to route Linux jobs to GitHub-hosted runners.

Pull requests from forks always use `ubuntu-latest`; untrusted code must never
run on self-hosted infrastructure.

PitCrew workers are socketless Linux containers. Workloads requiring Docker,
service containers, Testcontainers, Windows, or macOS must remain on an
appropriate hosted or isolated runner profile.

Foundry also publishes a repository-owned worker image for a dedicated
`foundry-ci` profile. The image and profile are delivered in separate reviewed
changes because the profile must pin the real public manifest digest produced
after trusted publication. See [Repository-Owned Runner Image](runner-image.md)
for publication, activation, update, fallback, and rollback boundaries.

The approved profile is stored at `.pitcrew/runner-profile.json` and preserves
two repository workers. Until operator activation is complete, keep
`CI_RUNNER=ubuntu-latest` for the hosted fallback. Activation changes that
repository variable to `foundry-ci` only after the dedicated profile is online
and verified.
