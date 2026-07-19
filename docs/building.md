---
description: Build, test, pack, and validate the Foundry repository from source.
---

# Building from Source

Foundry targets .NET 10 and uses central package management.

## Build

```powershell
dotnet build src\NexusLabs.Foundry.slnx
```

## Test

```powershell
dotnet test src\NexusLabs.Foundry.slnx
```

Live-provider and integration scenarios require their documented credentials
or services. Standard CI excludes tests categorized as integration tests.

## Build documentation

```powershell
python -m pip install --requirement requirements-docs.txt
python -m mkdocs build --strict
```

## Validate packages

Pack production projects into `artifacts\packages`, then run:

```powershell
pwsh scripts\validate-packages.ps1
```

The validator checks the complete package set, shared versions, required
assets, retired package references, the neutral Needlr dependency boundary,
and analyzer/source-generator packaging.

## NativeAOT

CI publishes
`src\Examples\AgentFramework\AotAgentFrameworkApp\AotAgentFrameworkApp.csproj`
for `linux-x64` and verifies that the output is a native executable.
