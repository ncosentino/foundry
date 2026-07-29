---
title: "ADR-0009: Repository-owned CI runner image"
status: "Accepted"
date: "2026-07-29"
authors: ["Nick Cosentino"]
tags: ["architecture", "decision", "ci", "github-actions", "pitcrew", "containers", "supply-chain"]
supersedes: ""
superseded_by: ""
---

## Context and scope

Foundry's trusted Linux workflows use ephemeral PitCrew workers, but each new
worker currently downloads the same .NET SDKs and installs the same NativeAOT
packages. Source, package, and AOT jobs require SDK 10.0.302. Documentation and
release-documentation jobs additionally require SDK 9.0.316. Repeating that
setup makes job startup dependent on mutable remote acquisition paths and
consumes substantial time and bandwidth.

The repository already preserves two important boundaries: fork pull requests
run on GitHub-hosted infrastructure, and the `CI_RUNNER` variable provides a
manual hosted fallback. The new image must improve trusted-worker startup
without weakening either boundary or allowing repository workflows to mutate a
PitCrew host.

This decision governs ownership, validation, publication, immutable identity,
and activation sequencing for the Foundry runner image. It does not activate a
runner profile, change `CI_RUNNER`, change branch protection, or move explicitly
hosted workflows onto self-hosted infrastructure.

## Decision drivers

- Every stable SDK and native prerequisite must be reviewable as repository
  source.
- Pull requests must validate the image without publishing it.
- Publication must occur only from trusted `main`.
- The deployed profile must eventually pin an immutable public manifest digest.
- Fork isolation, hosted fallback, required check names, and release credentials
  must remain unchanged.
- Runner images must not contain repository source, credentials, registration
  tokens, or generated workload output.
- Updating or rolling back the image must remain an explicit operator action.

## Decision

Foundry will own a Linux amd64 worker image at
`ghcr.io/ncosentino/foundry-runner`. The image extends the reviewed
PitCrew-compatible `myoung34/github-runner:ubuntu-noble` manifest by immutable
digest and installs:

- .NET SDK 9.0.316;
- .NET SDK 10.0.302;
- `clang`;
- `file`; and
- `zlib1g-dev`.

SDK archives are downloaded from Microsoft using exact versioned URLs and
verified against their published SHA-512 digests. The image's Docker build
context contains only the Dockerfile; no `COPY` or `ADD` instruction is
permitted. The base runner provides `Runner.Listener`, PowerShell, Git, and the
GitHub CLI, all of which are verified during the image build.

The `Runner Image` workflow runs its contract and candidate image on
GitHub-hosted `ubuntu-24.04`. Pull requests never publish. A trusted push to
`main` publishes only the source-SHA tag
`ghcr.io/ncosentino/foundry-runner:sha-<commit>`, with provenance and an SBOM,
and retains the manifest digest as workflow evidence.

Delivery uses two reviewed repository changes. The first establishes the image
and trusted publication. After that publication succeeds and anonymous access to the exact digest is
verified, a second change in this repository may pin that digest in the external
PitCrew profile manifest at `.pitcrew/runner-profile.json` and add portable
conditional SDK setup. The operator activates that profile only after the
second change merges.

The image workflow never changes `CI_RUNNER`, branch protection, runner
capacity, or host configuration. Update and rollback are performed by restoring
a reviewed profile digest and replaying the complete PitCrew setup command.

## Alternatives considered

### Keep installing the toolchain in every job

This retains maximum portability with no image lifecycle. It was rejected
because every ephemeral trusted worker repeatedly downloads hundreds of
megabytes of identical SDK archives and reinstalls unchanged NativeAOT
packages.

### Bake only SDK 10.0.302

This produces a smaller image. It was rejected because trusted documentation
jobs would still download both their missing SDK and supporting archives,
leaving a major workflow outside the repository-owned toolchain contract.

### Commit the profile before the image is published

This would allow both pieces to land together, but only by using a mutable tag
or guessing a future digest. It was rejected because the deployed identity must
be the real public manifest digest produced from trusted `main`.

### Publish from pull requests

This could make candidate images easier to inspect. It was rejected because
untrusted pull request code must not receive a deployable publication path or
package-write permission.

### Route all workflows to the specialized image immediately

This would shorten delivery. It was rejected because the image, public digest,
profile, and live workflow behavior need separate review and evidence. Hosted
fallback and explicitly hosted workflows remain independent.

## Consequences

### Positive

- Stable CI prerequisites are immutable and reviewable.
- Pull requests test the exact image construction without registry publication.
- Trusted publication produces provenance, an SBOM, and retained digest
  evidence.
- PitCrew activation can pin a public digest rather than a floating tag.
- Existing fork isolation and hosted fallback remain available.

### Negative

- The image is larger because it carries two SDK feature bands.
- Base image and SDK archive identities require periodic reviewed updates.
- Image publication and profile activation require two sequential changes and
  an operator step.

### Neutral

- Python and Node.js remain workflow-managed.
- Existing CI, documentation, release, and deployment routing is unchanged by
  the first pull request.
- The general-purpose PitCrew profile remains active until the dedicated
  profile is proven.

## Confirmation

The decision will be considered confirmed only after all staged evidence exists:

- mutation tests rejecting mutable or unpinned bases, floating SDK versions,
  source-copying Docker instructions, credential-bearing inputs, broad
  self-hosted image builds, and fork-routing regressions;
- GitHub-hosted image construction and execution on pull requests;
- trusted-main GHCR publication with provenance, SBOM, immutable digest, and
  retained publication evidence;
- anonymous retrieval of the selected public manifest digest; and
- later activation evidence proving the dedicated profile and hosted fallback.

## References

- `global.json` and `.github/dotnet/sdk-9/global.json` define exact SDK
  contracts.
- `.github/runner-images/foundry-ci/Dockerfile` defines the worker image.
- `.github/workflows/runner-image.yml` owns validation and publication.
- `scripts/test-runner-image.ps1` enforces the repository trust contract.
- `docs/runner-image.md` documents publication, activation, update, and
  rollback boundaries.
