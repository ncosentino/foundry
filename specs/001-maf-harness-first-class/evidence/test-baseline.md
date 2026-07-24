# G1 Canonical Targeted Test Baseline

## Manifest identity

- Repository commit: `06b04e6daec39c4a1fb57c3c94e7189fd7803ea0`
- SDK: .NET `10.0.301`
- Configuration: `Release`
- Canonical manifest SHA-256:
  `1f704d71daf944c6233b5235855419f7f09335cd51ac7b291d2fa8291ba3bdc2`

The hash is calculated over the six command lines below, encoded as UTF-8
without a byte-order mark and joined with a single LF between lines.

## Canonical commands

```powershell
dotnet test src\NexusLabs.Foundry.MicrosoftAgentFramework.Tests\NexusLabs.Foundry.MicrosoftAgentFramework.Tests.csproj --configuration Release --filter "FullyQualifiedName~AgentFactoryTests|FullyQualifiedName~WorkflowFactoryTests|FullyQualifiedName~GraphWorkflowRuntimeTests|FullyQualifiedName~DiagnosticsFunctionInvokingChatClientTests|FullyQualifiedName~MafAgentDiagnosticsDeduplicationTests|FullyQualifiedName~AgentScenarioRunnerTests|FullyQualifiedName~ProgressIntegrationTests" --logger "console;verbosity=minimal" --nologo
dotnet test src\NexusLabs.Foundry.MicrosoftAgentFramework.GeneratedWrapper.Tests\NexusLabs.Foundry.MicrosoftAgentFramework.GeneratedWrapper.Tests.csproj --configuration Release --filter "FullyQualifiedName~AIFunctionWrapperEndToEndTests|FullyQualifiedName~ToolInvocationRunnerLimitToToolsTests" --logger "console;verbosity=minimal" --nologo
dotnet test src\NexusLabs.Foundry.MicrosoftAgentFramework.Generators.Tests\NexusLabs.Foundry.MicrosoftAgentFramework.Generators.Tests.csproj --configuration Release --filter "FullyQualifiedName~AgentFrameworkFunctionRegistryGeneratorTests|FullyQualifiedName~AsyncLocalScopedGeneratorTests" --logger "console;verbosity=minimal" --nologo
dotnet test src\NexusLabs.Foundry.MicrosoftAgentFramework.Analyzers.Tests\NexusLabs.Foundry.MicrosoftAgentFramework.Analyzers.Tests.csproj --configuration Release --filter "FullyQualifiedName~AgentTopologyAnalyzerTests|FullyQualifiedName~AgentFunctionTypesMiswiredAnalyzerTests|FullyQualifiedName~ToolResultToStringAnalyzerTests" --logger "console;verbosity=minimal" --nologo
dotnet test src\NexusLabs.Foundry.Evaluation.Tests\NexusLabs.Foundry.Evaluation.Tests.csproj --configuration Release --filter "FullyQualifiedName~ExperimentRunnerEvaluationTests|FullyQualifiedName~ExperimentRunnerSchedulingTests|FullyQualifiedName~AgentRunDiagnosticsEvaluationExtensionsTests|FullyQualifiedName~EvaluationCaptureChatClientTests|FullyQualifiedName~ToolCallTrajectoryEvaluatorTests" --logger "console;verbosity=minimal" --nologo
dotnet test src\NexusLabs.Foundry.Evaluation.Reporting.Tests\NexusLabs.Foundry.Evaluation.Reporting.Tests.csproj --configuration Release --filter "FullyQualifiedName~MeaiReportingExperimentAdapterTests" --logger "console;verbosity=minimal" --nologo
```

## Pre-uplift results

| Selection | Passed | Failed | Skipped |
|---|---:|---:|---:|
| Core agents, workflows, diagnostics, progress, and scenario testing | 102 | 0 | 0 |
| Generated-tool ingress | 60 | 0 | 0 |
| Source generators | 147 | 0 | 0 |
| Analyzers | 19 | 0 | 0 |
| Evaluation and experiment execution | 46 | 0 | 0 |
| Evaluation reporting | 15 | 0 | 0 |
| **Total** | **389** | **0** | **0** |

The complete repository test matrix also passed in
[CI run 30036664470](https://github.com/ncosentino/foundry/actions/runs/30036664470).
The six-command manifest is the comparison boundary for T007-T009; adding or
removing selectors requires a new manifest hash and an explicit explanation.
The candidate report must also record any resolved-test-count change because
the class-substring filters intentionally include newly added tests within the
selected contract classes.
