---
description: Understand how Foundry builds, publishes, pins, activates, updates, and rolls back its repository-owned CI runner image.
---

# Repository-Owned Runner Image

Foundry owns a dedicated Linux amd64 worker image for trusted PitCrew jobs:

```text
ghcr.io/ncosentino/foundry-runner
```

The image prepares stable prerequisites once per reviewed image revision:

- .NET SDK 9.0.316;
- .NET SDK 10.0.302;
- NativeAOT compiler and runtime packages;
- the GitHub Actions runner runtime;
- PowerShell;
- Git; and
- the GitHub CLI.

Python and Node.js remain workflow-managed.

## Trust boundaries

Pull requests build and execute the candidate image only on GitHub-hosted
`ubuntu-24.04`. They do not receive package-write permission and cannot publish
the image.

A trusted push to `main` publishes the source-SHA tag:

```text
ghcr.io/ncosentino/foundry-runner:sha-<commit>
```

Publication includes provenance and an SBOM. The workflow records the resulting
manifest digest as both a job summary and a retained artifact.

The first trusted publication completed in
[workflow run 30495344544](https://github.com/ncosentino/foundry/actions/runs/30495344544):

```text
source SHA: 7aa13e31d4eda724f362fdadc446661ca28ca74a
manifest:   sha256:b03be39181c9cce46a680037262e4e2bf4eaeee1539d669a81543980f5f6d8e8
image:      ghcr.io/ncosentino/foundry-runner@sha256:b03be39181c9cce46a680037262e4e2bf4eaeee1539d669a81543980f5f6d8e8
```

The exact digest was also resolved successfully with a clean anonymous Docker
configuration.

The image build context is `.github/runner-images/foundry-ci/`. It contains no
repository source, credentials, runner registration token, or generated
workload output.

## Two-change bootstrap

The repository cannot safely commit a final PitCrew profile until the first
trusted publication produces its real public manifest digest.

1. Merge the image-contract pull request.
2. Wait for the trusted `main` publication.
3. Record the source SHA, immutable tag, and manifest digest.
4. Make the GHCR package public if its initial visibility is private.
5. Verify anonymous retrieval of the exact digest.
6. From this Foundry repository, commit a second pull request that pins that
   digest in `.pitcrew/runner-profile.json`.

Use a clean Docker client configuration to prove that the selected digest is
publicly readable without cached GHCR credentials:

```powershell
$originalDockerConfig = $env:DOCKER_CONFIG
$anonymousDockerConfig = Join-Path $env:TEMP "foundry-ghcr-anonymous"
try {
    New-Item -ItemType Directory -Path $anonymousDockerConfig -Force | Out-Null
    $env:DOCKER_CONFIG = $anonymousDockerConfig
    docker buildx imagetools inspect `
        ghcr.io/ncosentino/foundry-runner@sha256:<manifest-digest>
}
finally {
    $env:DOCKER_CONFIG = $originalDockerConfig
    Remove-Item -LiteralPath $anonymousDockerConfig -Recurse -Force
}
```

Record that output with the trusted publication run before pull request 2 is
treated as ready.

Mutable tags are never used as the deployed profile identity.

## Portable SDK setup

Affected workflows call the repository action:

```yaml
- name: Setup exact .NET SDKs
  uses: ./.github/actions/setup-dotnet
  with:
    global-json-files: |
      global.json
      .github/dotnet/sdk-9/global.json
```

The action validates every contract, inventories installed SDKs, and skips
installation only when the complete requested set is present. If any SDK is
missing, pinned `actions/setup-dotnet` installs the full set into
`$RUNNER_TEMP/foundry-dotnet`, preserving hosted portability without allowing
one SDK root to hide another.

## Activation boundary

Repository workflows do not install or update PitCrew. After the profile pull
request merges, an operator applies the profile from an approved PitCrew
checkout, verifies manager and worker convergence, then sets `CI_RUNNER` to the
dedicated profile label.

Apply the committed profile with the existing capacity:

```powershell
.\Setup-Runner.ps1 `
    -ProfilePath <foundry-checkout>\.pitcrew\runner-profile.json `
    -Repos https://github.com/ncosentino/foundry=2
```

Before changing routing, verify:

- the replacement manager reports the current manager contract;
- `observed-state.json` is fresh and reports `managerStatus: running`;
- the desired generation is accepted;
- the target image and image ID match the committed digest;
- two configured slots remain available; and
- a repository runner carrying label `foundry-ci` is online.

Then set `CI_RUNNER=foundry-ci`, run CI and Documentation from `main`, and prove
the exact SDK action reports `Setup performed: false` for every SDK baked into
the image. Required checks `build-test-pack`, `docs`, and `aot` must pass on the
activation SHA. Do not dispatch Release from a non-tag as a smoke test.

The existing general-purpose route remains intact until CI and Documentation
have passed on the dedicated profile. Fork pull requests remain GitHub-hosted,
and `CI_RUNNER=ubuntu-latest` remains the hosted fallback.

Only after the dedicated route is proven should the operator remove Foundry
from the general-purpose profile. First record that profile's non-secret
`desired-capacity.json`, `acknowledged-capacity.json`, and `observed-state.json`.
Then use the exact running profile and PitCrew's capacity-only removal path:

```powershell
.\Setup-Runner.ps1 `
    -Profile <general-purpose-profile> `
    -RemoveRepos https://github.com/ncosentino/foundry `
    -CapacityOnly
```

If the stored profile is autoscaled, replay its existing `-Autoscale`,
`-MinimumIdle`, and `-ScaleDownDelaySeconds` values unchanged. Verify the
manager container ID did not change and every unrelated repository/count
remains in desired and acknowledged state.

## Updating the image

Every update repeats the same sequence:

1. Review and merge an image or SDK contract change.
2. Publish a new source-SHA tag and immutable digest from `main`.
3. Review and merge the profile digest change.
4. Roll out one approved host.
5. Verify current and stale workers before continuing.

An `update.status` of `rolling` is valid while assigned stale workers finish
their current jobs.

## Rollback

Rollback restores a previously reviewed profile digest and replays the complete
PitCrew profile command. Do not use a mutable tag, broad container teardown,
Docker daemon restart, or forceful classic-runner deletion.
