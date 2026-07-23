# G1 Candidate Package and Compatibility Probe

## Candidate identity

- Base commit: `bcda0d0` from `harness/g1-integration`
- Candidate work branch: `harness/g1-candidate`
- SDK: .NET `10.0.301`
- Solution projects: 54
- New project:
  `src/Examples/AgentFramework/HarnessCompatibilityProbe/HarnessCompatibilityProbe.csproj`

## Staged central versions

| Surface | Package | Baseline | Candidate |
|---|---|---:|---:|
| MAF core | `Microsoft.Agents.AI` | 1.3.0 | 1.15.0 |
| Harness | `Microsoft.Agents.AI.Harness` | absent | 1.15.0 |
| MAF workflows | `Microsoft.Agents.AI.Workflows` | 1.3.0 | 1.15.0 |
| MAF generators | `Microsoft.Agents.AI.Workflows.Generators` | 1.3.0 | 1.15.0 |
| DevUI | `Microsoft.Agents.AI.DevUI` | 1.3.0-preview.260423.1 | 1.15.0-preview.260722.1 |
| Hosting | `Microsoft.Agents.AI.Hosting` | 1.3.0-preview.260423.1 | 1.15.0-preview.260722.1 |
| Hosting OpenAI | `Microsoft.Agents.AI.Hosting.OpenAI` | 1.3.0-alpha.260423.1 | 1.15.0-alpha.260722.1 |
| MEAI | `Microsoft.Extensions.AI` | 10.5.0 | 10.6.0 |
| MEAI abstractions | `Microsoft.Extensions.AI.Abstractions` | 10.5.0 | 10.6.0 |
| MEAI OpenAI | `Microsoft.Extensions.AI.OpenAI` | 10.5.0 | 10.6.0 |
| Evaluation | `Microsoft.Extensions.AI.Evaluation` | 10.5.0 | 10.6.0 |
| Evaluation quality | `Microsoft.Extensions.AI.Evaluation.Quality` | 10.5.0 | 10.6.0 |
| Evaluation reporting | `Microsoft.Extensions.AI.Evaluation.Reporting` | 10.5.0 | 10.6.0 |

The probe directly references Harness, MAF core, Workflows,
Workflows.Generators, all three MEAI packages, and all three Evaluation packages.
Workflows.Generators is referenced as an analyzer.

## Restore and build

```powershell
dotnet restore src\NexusLabs.Foundry.slnx --verbosity minimal
dotnet build src\NexusLabs.Foundry.slnx --configuration Release --no-restore --no-incremental --nologo
dotnet run --project src\Examples\AgentFramework\HarnessCompatibilityProbe\HarnessCompatibilityProbe.csproj --configuration Release --no-build
```

- Restore: passed for all 54 projects with no NuGet warning or error.
- Probe build: passed with zero warnings and zero errors.
- Probe execution: passed and constructed
  `Microsoft.Agents.AI.HarnessAgent` named
  `foundry-harness-compatibility-probe`.
- Solution build: passed with zero errors and three existing generated-code
  `CS0162` warnings in the non-incremental local build.

The first probe build attempted to implement `Metadata` explicitly on
`IChatClient` and failed with `CS0539`. MEAI 10.6.0 does not expose that property
as an explicit interface member. The probe was corrected to the actual interface
without a compatibility shim.

## Resolved transitive lifts

The following command was run for the probe, MAF core, Workflows, DevUI,
Evaluation, Evaluation.Reporting, and existing NativeAOT projects:

```powershell
dotnet list <project> package --include-transitive
```

Audited project paths:

- `src/Examples/AgentFramework/HarnessCompatibilityProbe/HarnessCompatibilityProbe.csproj`
- `src/NexusLabs.Foundry.MicrosoftAgentFramework/NexusLabs.Foundry.MicrosoftAgentFramework.csproj`
- `src/NexusLabs.Foundry.MicrosoftAgentFramework.Workflows/NexusLabs.Foundry.MicrosoftAgentFramework.Workflows.csproj`
- `src/NexusLabs.Foundry.MicrosoftAgentFramework.DevUI/NexusLabs.Foundry.MicrosoftAgentFramework.DevUI.csproj`
- `src/NexusLabs.Foundry.Evaluation/NexusLabs.Foundry.Evaluation.csproj`
- `src/NexusLabs.Foundry.Evaluation.Reporting/NexusLabs.Foundry.Evaluation.Reporting.csproj`
- `src/Examples/AgentFramework/AotAgentFrameworkApp/AotAgentFrameworkApp.csproj`

| Package | Baseline resolution | Candidate resolution |
|---|---:|---:|
| `Microsoft.Agents.AI.Abstractions` | 1.3.0 | 1.15.0 |
| `Microsoft.Extensions.AI.Evaluation` from MAF-only consumers | 10.4.0 | 10.6.0 |
| `Microsoft.Extensions.DependencyInjection.Abstractions` | 10.0.6 | 10.0.9 |
| `Microsoft.Extensions.Logging.Abstractions` | 10.0.6 | 10.0.9 |
| `Microsoft.Extensions.Compliance.Abstractions` | 10.5.0 | 10.5.0 |
| `Microsoft.Extensions.VectorData.Abstractions` | 9.7.0 | 9.7.0 |
| `Microsoft.ML.Tokenizers` | 2.0.0 | 2.0.0 |
| `Microsoft.Extensions.FileSystemGlobbing` | absent | 10.0.6 |
| `OpenTelemetry.Api` | 1.15.3 | 1.15.3 |

On `net10.0`, DiagnosticSource, JSON, and Channels are supplied by the shared
framework rather than appearing as package dependencies in the resolved graph.

## Surface observations

1. The complete solution restores and compiles with the staged graph. The probe
   directly loads the referenced packages and proves basic Harness construction;
   it does not yet exercise Workflows, Evaluation behavior, or generated output.
2. Existing DevUI, Hosting, and Hosting.OpenAI projects compile on the 1.15
   preview/alpha satellite line; T011-T012 still own their independent
   compatibility dispositions.
3. Central direct DI and Logging implementation pins remain at 10.0.3 while
   required abstractions resolve to 10.0.9; DevUI also resolves additional
   Logging implementation packages at 10.0.1. Restore and build accept this
   mix, but it remains an unresolved binary/runtime risk for T013 rather than
   compatibility proof.
4. No neutral Foundry project directly references the Harness package. Harness
   remains isolated to the compatibility probe, while the planned central
   MAF/MEAI uplift affects all existing consumers.
5. This leaf proves restore, compile, and basic construction only. Targeted
   regression tests, NativeAOT publication/execution, and lifecycle seam tracing
   remain T007-T014.
6. The probe leaves default in-memory history and approval-wrapper construction
   untouched but does not invoke them. Default behavior inspection belongs to
   T013 and later capability-profile work.
