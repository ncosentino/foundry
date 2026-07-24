# G1 Pre-Uplift Package Baseline

## Baseline identity

- Repository commit: `06b04e6daec39c4a1fb57c3c94e7189fd7803ea0`
- SDK: .NET `10.0.301`
- Solution: `src/NexusLabs.Foundry.slnx`
- Solution projects: 53
- Hosted restore/build evidence:
  [CI run 30036664470](https://github.com/ncosentino/foundry/actions/runs/30036664470)
- Harness package present: no

The hosted run restored the complete solution successfully with no `NU` warning
or error. Its release build completed with eight existing compiler warnings and
zero errors. The run was triggered from branch commit
`2e1c96d3b54e455a6aa2f7d584c3248d29f176c1` and checked out merge tree
`d92417c66be90e7f05de0d447939a7b64ebdf5e4`. The resulting source tree is
equivalent to baseline squash commit
`06b04e6daec39c4a1fb57c3c94e7189fd7803ea0`; the only branch change was the
documentation-only task-count correction.

## Centrally pinned AI package graph

| Surface | Package | Baseline version |
|---|---|---:|
| MAF core | `Microsoft.Agents.AI` | 1.3.0 |
| MAF workflows | `Microsoft.Agents.AI.Workflows` | 1.3.0 |
| MAF generators | `Microsoft.Agents.AI.Workflows.Generators` | 1.3.0 |
| DevUI | `Microsoft.Agents.AI.DevUI` | 1.3.0-preview.260423.1 |
| Hosting | `Microsoft.Agents.AI.Hosting` | 1.3.0-preview.260423.1 |
| Hosting OpenAI | `Microsoft.Agents.AI.Hosting.OpenAI` | 1.3.0-alpha.260423.1 |
| MEAI | `Microsoft.Extensions.AI` | 10.5.0 |
| MEAI abstractions | `Microsoft.Extensions.AI.Abstractions` | 10.5.0 |
| MEAI OpenAI | `Microsoft.Extensions.AI.OpenAI` | 10.5.0 |
| Evaluation | `Microsoft.Extensions.AI.Evaluation` | 10.5.0 |
| Evaluation quality | `Microsoft.Extensions.AI.Evaluation.Quality` | 10.5.0 |
| Evaluation reporting | `Microsoft.Extensions.AI.Evaluation.Reporting` | 10.5.0 |
| OpenTelemetry API | `OpenTelemetry.Api` | 1.15.3 |

## Observed transitive graph

`dotnet list <project> package --include-transitive` was run for:

- `src/NexusLabs.Foundry.MicrosoftAgentFramework/NexusLabs.Foundry.MicrosoftAgentFramework.csproj`
- `src/NexusLabs.Foundry.MicrosoftAgentFramework.Workflows/NexusLabs.Foundry.MicrosoftAgentFramework.Workflows.csproj`
- `src/NexusLabs.Foundry.MicrosoftAgentFramework.DevUI/NexusLabs.Foundry.MicrosoftAgentFramework.DevUI.csproj`
- `src/NexusLabs.Foundry.MicrosoftAgentFramework.Generators/NexusLabs.Foundry.MicrosoftAgentFramework.Generators.csproj`
- `src/NexusLabs.Foundry.Evaluation/NexusLabs.Foundry.Evaluation.csproj`
- `src/NexusLabs.Foundry.Evaluation.Reporting/NexusLabs.Foundry.Evaluation.Reporting.csproj`
- `src/Examples/AgentFramework/AotAgentFrameworkApp/AotAgentFrameworkApp.csproj`

The table distinguishes packages directly referenced by the consuming project
from versions observed only transitively.

| Consuming surface | Relevant transitive resolution |
|---|---|
| MAF core and Workflows | Direct MAF core/Workflows 1.3.0 and OpenTelemetry.Api 1.15.3; transitive MAF.Abstractions 1.3.0, MEAI/MEAI.Abstractions 10.5.0, Evaluation 10.4.0, Compliance.Abstractions 10.5.0, VectorData.Abstractions 9.7.0, ML.Tokenizers 2.0.0, and DI/Logging abstractions 10.0.6 |
| DevUI | Direct DevUI/Hosting preview 1.3.0 and Hosting.OpenAI alpha 1.3.0; transitive MAF core/Workflows 1.3.0, MEAI/MEAI.Abstractions/MEAI.OpenAI 10.5.0, Evaluation 10.4.0, DI/Logging implementation packages 10.0.1, and abstractions 10.0.6 |
| Evaluation | Direct Evaluation and Quality 10.5.0; transitive MAF 1.3.0, MEAI 10.5.0, and DI/Logging abstractions 10.0.6 |
| Evaluation.Reporting | Direct Reporting 10.5.0; transitive Evaluation, Quality, and MEAI 10.5.0 plus MAF 1.3.0 |
| NativeAOT example | Direct MAF 1.3.0 and MEAI 10.5.0; transitive Workflows 1.3.0 and Evaluation 10.4.0 |
| Foundry generators | Direct Roslyn CSharp 4.11.0 and analyzers 3.3.4; transitive Roslyn Common 4.11.0 |

## Baseline implications

1. MAF 1.3.0 transitively resolves Evaluation 10.4.0 on projects that do not
   directly reference the centrally pinned 10.5.0 Evaluation package.
2. Harness is absent, so the candidate must add it without leaking that
   dependency into neutral packages.
3. DevUI and Hosting already form a separate preview/alpha satellite lane and
   must remain independently deferrable.
4. The candidate comparison must account for DI, Logging, DiagnosticSource,
   JSON, Channels, tokenization, and Evaluation floor changes introduced by
   MAF/Harness 1.15.0 and MEAI 10.6.0.

The official MAF 1.3.0 nuspec confirms Compliance.Abstractions 10.5.0,
VectorData.Abstractions 9.7.0, and ML.Tokenizers 2.0.0 were already present.
They were omitted from the original filtered `dotnet list` display and are not
candidate-only dependencies.
