# G1 Candidate Core Agent and Workflow Results

## Selection

The candidate used the unchanged canonical command and selector recorded in
`test-baseline.md`:

```powershell
dotnet test src\NexusLabs.Foundry.MicrosoftAgentFramework.Tests\NexusLabs.Foundry.MicrosoftAgentFramework.Tests.csproj --configuration Release --filter "FullyQualifiedName~AgentFactoryTests|FullyQualifiedName~WorkflowFactoryTests|FullyQualifiedName~GraphWorkflowRuntimeTests|FullyQualifiedName~DiagnosticsFunctionInvokingChatClientTests|FullyQualifiedName~MafAgentDiagnosticsDeduplicationTests|FullyQualifiedName~AgentScenarioRunnerTests|FullyQualifiedName~ProgressIntegrationTests" --logger "console;verbosity=minimal" --nologo
```

| Graph | Passed | Failed | Skipped |
|---|---:|---:|---:|
| MAF 1.3 / MEAI 10.5 baseline | 102 | 0 | 0 |
| MAF 1.15 / MEAI 10.6 candidate | 102 | 0 | 0 |

The resolved test count did not change.

## Hosted full-project evidence

[PR #71 hosted run 30043993631, attempt 2](https://github.com/ncosentino/foundry/actions/runs/30043993631)
passed the complete `NexusLabs.Foundry.MicrosoftAgentFramework.Tests` project.
This is full-project smoke evidence rather than the canonical comparison
boundary:

| Graph | Passed | Failed |
|---|---:|---:|
| Baseline hosted run 30039044449 | 1,569 | 0 |
| Candidate hosted run 30043993631 | 1,569 | 0 |

## Group-chat diagnostic-order finding

The first hosted attempt exposed two assertions that treated
`RunWithDiagnosticsAsync()` stage-list order as round-robin invocation order.
MAF 1.15 designates participant executors as intermediate outputs, so diagnostic
stage collection order is not a turn-order contract.

The tests now give writer and reviewer agents distinct instructions and assert
the actual `IChatClient` invocation sequence:

```text
WRITER
REVIEWER
```

All 18 group-chat tests pass. No production ordering shim was added. A latent
dependency on MAF's participant-container enumeration remains a T013 lifecycle
and public-delta review item rather than an active regression.

## Disposition

Pass. Core agent, workflow, diagnostics, progress, and scenario contracts show
no unexplained regression on the canonical selection.
