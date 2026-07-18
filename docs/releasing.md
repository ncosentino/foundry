# Releasing Foundry

Foundry releases are tag-driven and publish all production
`NexusLabs.Foundry.*` packages together.

## Prepare a release

1. Confirm the default branch CI is green.
2. Move the relevant entries from `Unreleased` in `CHANGELOG.md` into a section
   named for the exact package version, for example:

   ```markdown
   ## [0.1.0-alpha.1] - 2026-07-18
   ```

3. Update `version.json` so its version matches the release version.
4. Create an annotated tag using the same version with a `v` prefix.

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
