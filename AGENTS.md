# Foundry - AI Agent Instructions

Foundry is an AI and agentic application framework for .NET. It owns agent
orchestration, evaluation, provider integrations, observability, and testing.
Needlr is an optional dependency-injection integration, not the Foundry core.

## Architecture

- Neutral Foundry packages must not reference `NexusLabs.Needlr`.
- Needlr-specific code belongs in packages named `NexusLabs.Foundry.Needlr.*`.
- Provider integrations depend on neutral Foundry abstractions, never the reverse.
- Microsoft Agent Framework, Microsoft.Extensions.AI, Langfuse, Copilot, and
  Semantic Kernel integrations remain independently replaceable.
- Source generators and analyzers may support neutral Foundry declarations and
  optional framework integrations, but generated neutral code must not require
  Needlr.

## Build and Test

```powershell
dotnet build src\NexusLabs.Foundry.slnx
dotnet test src\NexusLabs.Foundry.slnx
```

Use narrowly targeted local validation. Run broad evaluation workloads through
hosted CI rather than on a developer workstation.

## Code Conventions

- One type per C# file.
- Use file-scoped namespaces.
- Use `internal` unless consumers reference the type directly.
- Data carriers are records; services and mutable behavior are classes.
- Document every public type and member with XML documentation.
- Prefer interfaces and composition over inheritance and static state.
- Do not add optional parameters or default interface members. Use explicit
  overloads and required options records.
- Do not add compatibility shims for the former
  `NexusLabs.Foundry.MicrosoftAgentFramework`, `NexusLabs.Foundry.Copilot`, or
  `NexusLabs.Foundry.Needlr.SemanticKernel` alpha APIs.

## Package Conventions

- Neutral package IDs and namespaces begin with `NexusLabs.Foundry`.
- Needlr integration packages begin with `NexusLabs.Foundry.Needlr`.
- Diagnostic IDs use the `FDRY` prefix followed by a component code.
- Package versions are managed centrally in `src\Directory.Packages.props`.
- Keep provider preview dependencies isolated from stable neutral packages.

## Architecture Decision Records

Architecturally significant decisions belong in `docs\adr\` using the existing
`adr-NNNN-title-slug.md` convention. Preserve accepted records and supersede
them explicitly rather than rewriting their history.
