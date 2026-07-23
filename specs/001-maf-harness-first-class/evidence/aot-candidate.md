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
- Native executable size: 10,528,256 bytes
- Native executable SHA-256:
  `9d8931a8fd6d48619aa7328c77d0e5d74e26400dd5ba27ce5e420135e7d6cfe3`

The probe verifies exactly one generated function named `Echo`, requests call ID
`probe-call`, and accepts exactly one matching `FunctionResultContent` before
reporting success.

The clean-rebuilt native binary also executed both lifecycle modes successfully:

- `--lifecycle`
- `--lifecycle --default-history`

```text
default_exit=0
lifecycle_exit=0
default_history_exit=0

TRACE:history.provide
TRACE:history.store:2:1
TRACE:function.invoker:Echo:probe-call
TRACE:history.provide
TRACE:history.store:1:1

TRACE:compaction:call=False:result=False:messages=2
TRACE:function.invoker:Echo:probe-call
TRACE:compaction:call=True:result=False:messages=3
```

The first local publish attempt reached native linking but could not find
`vswhere.exe` on `PATH`. Retrying in a single `vcvars64.bat`-initialized process
resolved the host-toolchain issue without changing source or suppressing a
warning.

## Disposition

Pass on Windows x64. The minimum generated-tool Harness profile publishes and
executes with reflection disabled. The existing Foundry Linux x64 AOT control
also publishes on the candidate graph; hosted Linux execution of the Harness
probe remains later hardening work rather than a G1 claim.
