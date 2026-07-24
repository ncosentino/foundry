# G1 Candidate Generated-Tool Harness NativeAOT Result

## Existing AOT control

[PR #71 hosted run 30043993631, attempt 2](https://github.com/ncosentino/foundry/actions/runs/30043993631)
republished the unchanged
`src/Examples/AgentFramework/AotAgentFrameworkApp/AotAgentFrameworkApp.csproj`
under MAF 1.15 and MEAI 10.6. The Linux x64 NativeAOT job passed.

## Harness probe constraints

`HarnessCompatibilityProbe` enables:

- `PublishAot=true`
- `IsAotCompatible=true`
- full trimming
- `IlcDisableReflection=true`
- AOT and trim analyzers
- IL trim and dynamic-code warnings as errors

Its generated `IAIFunctionProvider` supplies `ProbeFunctions.Echo` directly to
`HarnessAgentOptions.ChatOptions.Tools`. The deterministic chat client requests
the tool, Harness executes it, and the second round returns
`tool-result:aot`.

## Publish and execution

The Windows x64 publish ran in the installed Visual Studio native-tools
environment:

```powershell
dotnet publish src\Examples\AgentFramework\HarnessCompatibilityProbe\HarnessCompatibilityProbe.csproj `
  --configuration Release `
  --runtime win-x64 `
  --self-contained `
  --output <session-output>
```

- Restore: passed
- Managed build: passed with zero warnings and zero errors
- Native code generation: passed
- IL trim/AOT warnings: none
- Native execution exit code: 0
- Native output:
  `Microsoft.Agents.AI.HarnessAgent:foundry-harness-compatibility-probe:tool-result:aot`
- Native executable size: 10,543,104 bytes
- Native executable SHA-256:
  `db2376f1334bdf055dd0ec31ea5210148b62ddc4ecc3945de761d018e22db879`

The probe verifies exactly one generated function named `Echo`, requests call ID
`probe-call`, and accepts exactly one matching `FunctionResultContent` before
reporting success.

The clean-rebuilt native binary also executed both lifecycle modes successfully:

- `--lifecycle`
- `--lifecycle --default-history`
- `--approval`

```text
default_exit=0
lifecycle_exit=0
default_history_exit=0
approval_exit=0

TRACE:history.provide
TRACE:history.store:2:1
TRACE:function.invoker:Echo:probe-call
TRACE:history.provide
TRACE:history.store:1:1

TRACE:compaction:call=False:result=False:messages=2
TRACE:function.invoker:Echo:probe-call
TRACE:compaction:call=True:result=False:messages=3

APPROVAL:approved:request=probe-call:invocations=1:result=tool-result:aot
APPROVAL:rejected:request=probe-call:invocations=0:result=tool-result:Tool call invocation rejected. rejected
```

The first local publish attempt reached native linking but could not find
`vswhere.exe` on `PATH`. Retrying in a single `vcvars64.bat`-initialized process
resolved the host-toolchain issue without changing source or suppressing a
warning.

## Hosted Linux gate

`.github/workflows/harness-g1-aot.yml` is a path-filtered, GitHub-hosted gate
that publishes and executes the probe in all four modes on Linux x64. It runs
only when the probe, central package graph, neutral MAF core, or its own workflow
changes.

The workflow passed against implementation commit
`932cb5ec701a9aa9dc93c3315e8fc16eacede6c5`:

- [run 30055150937](https://github.com/ncosentino/foundry/actions/runs/30055150937)
- job: `harness-aot`
- runtime: Linux x64
- file type: stripped x86-64 ELF executable
- Build ID: `42a84cfa557ddd4780a955f30958abda5c34acdc`
- all default, custom-history, default-history, and approval modes: exit 0
- artifact: `harness-g1-aot-linux-x64`, ID `8582497812`
- artifact archive size: 4,232,216 bytes
- native executable size: 9,561,528 bytes
- native executable SHA-256:
  `420fabbf1514f7c828ffdaa39d5eb993229961d9b893ee21638765270bce10ae`

This workflow is G1 evidence infrastructure, not the final product profile.
G7 T086 must supersede it with the supported AOT Harness application, and G10
must remove or retain it explicitly.

## Disposition

Pass on Windows x64 and GitHub-hosted Linux x64. The minimum generated-tool
Harness profile publishes and executes with reflection disabled in all required
G1 modes. The existing Foundry Linux x64 AOT control also remains green.
