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
6. Commit a second pull request that pins that digest in
   `.pitcrew/runner-profile.json`.

Mutable tags are never used as the deployed profile identity.

## Activation boundary

Repository workflows do not install or update PitCrew. After the profile pull
request merges, an operator applies the profile from an approved PitCrew
checkout, verifies manager and worker convergence, then sets `CI_RUNNER` to the
dedicated profile label.

The existing general-purpose route remains intact until CI and Documentation
have passed on the dedicated profile. Fork pull requests remain GitHub-hosted,
and `CI_RUNNER=ubuntu-latest` remains the hosted fallback.

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
