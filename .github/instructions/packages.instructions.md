---
applyTo: "src/**/*.csproj,src/Directory.{Build,Packages}.props,src/Directory.Build.targets,scripts/validate-packages.ps1"
---

# Project and package contracts

- Manage dependency versions centrally in `src/Directory.Packages.props`.
- Neutral package IDs and namespaces begin with `NexusLabs.Foundry`.
- Needlr-specific code and packages use the `NexusLabs.Foundry.Needlr` prefix.
- Neutral packages must not depend on `NexusLabs.Needlr`.
- Keep preview provider dependencies out of stable neutral packages.
- When adding or removing a packable project, update the executable package
  contract in `scripts/validate-packages.ps1`.
- Preserve analyzer, source-generator, build asset, and runtime asset placement;
  validate the produced packages rather than inferring layout from project files.
