# Releasing Foundry

Foundry releases are tag-driven and publish all production
`NexusLabs.Foundry.*` packages together.

## Trusted publishing

NuGet.org publication uses GitHub OIDC through `NuGet/login@v1`; no long-lived
API key is stored in GitHub.

The NuGet trusted publishing policy is bound to:

- package owner `ncosentino`;
- repository `ncosentino/foundry`;
- workflow file `release.yml`;
- GitHub environment `release`.

The publish job requests `id-token: write` and exchanges the workflow identity
for a short-lived NuGet API key immediately before package publication.

## Prepare a release

1. Confirm the default branch CI is green.
2. Move the relevant entries from `Unreleased` in `CHANGELOG.md` into a section
   named for the exact package version, for example:

   ```markdown
   ## [0.1.0-alpha.1] - 2026-07-18
   ```

3. Update installation documentation and badges for the release version.
4. Update `version.json` so its version matches the release version.
5. Create an annotated tag using the same version with a `v` prefix.

Nerdbank.GitVersioning normalizes numeric prerelease identifiers for NuGet. For
example, tag `v0.1.0-alpha.1` publishes package version
`0.1.0-alpha-0001`.

## Release validation

The release workflow:

- validates the tag format and Nerdbank.GitVersioning package version;
- builds and tests the complete solution in Release configuration;
- packs the full Foundry package family;
- verifies package IDs, versions, contents, and the neutral-package dependency
  boundary;
- publishes packages to NuGet.org and GitHub Packages;
- creates a GitHub release from the matching changelog section.

The workflow fails before publication when the tag, version, changelog, or
package validation does not match.

Package publication is resumable rather than atomic. If a transient failure
occurs after some packages reach NuGet.org, rerun the workflow for the same tag;
`--skip-duplicate` preserves completed pushes and continues with the remaining
packages.
