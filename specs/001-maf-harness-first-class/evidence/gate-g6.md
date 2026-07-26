# Gate G6 Decision — Optional Microsoft Agent Framework Harness Bundle

## Decision

**PASS for the cumulative G6 optional-bundle implementation and public API
candidate.**

G6 delivers one separately referenced complete-bundle lane:

1. an optional production package and test project;
2. explicit mapping of supported `HarnessAgentOptions` and `ChatOptions`;
3. requested/effective/backing reporting for every tracked upstream dimension;
4. source-generated `AIFunction` ingress through the public generated provider;
5. one upstream function loop and one upstream OpenTelemetry owner;
6. Foundry agent/model/tool progress without a second loop, telemetry writer,
   metric writer, or diagnostics writer; and
7. ADR-0008, which records the durable package and ownership decision.

The neutral `NexusLabs.Foundry.MicrosoftAgentFramework` package does not
reference the optional bundle. Consumers that do not select the optional
package acquire no bundle dependency or behavior.

## ADR identity disposition

The immutable T080 text names
`docs/adr/adr-0007-optional-harness-bundle.md`. ADR-0007 had already been
allocated and accepted for experimental hybrid context compaction before G6
started. Reusing or rewriting that accepted identifier would corrupt decision
history. The optional-bundle decision is therefore recorded as
`docs/adr/adr-0008-optional-harness-bundle.md`.

## Evidence identity

| Slice | Commit / PR | Bundle tests | Core MAF tests | Disposition |
|---|---|---:|---:|---|
| G6.1 optional package and defaults | `1bd7793f` / PR #111 | 187 | 2,181 | Merged into `harness/g6-integration` |
| Generated-tool ingress (T075) | `e458a970` | 191 | 2,181 | Passed |
| Telemetry/progress composition (T076/T079) | `63bb57f6` | 203 | 2,181 | Passed; reviewed |
| API and streaming review fixes | `dae70326`, `8f0deda9` | **203** | **2,181** | All blockers resolved |
| G6.2 generated tools, telemetry, progress, and decision | `32f9854d` / PR #112 | **203** | **2,181** | Merged into `harness/g6-integration` |

- Final reviewed local G6.2 head: `8f0deda9` on `harness/g6-telemetry`.
- Final G6 implementation integration head: `32f9854d91b58820a329b5a1278e02de3da7dfb0`
  on `harness/g6-integration`, merged through
  [PR #112](https://github.com/ncosentino/foundry/pull/112).
- `dotnet build src\NexusLabs.Foundry.slnx`: 0 errors.
- Full package validation: 14 Foundry packages share one version; the optional
  bundle has the required direct dependencies on
  `NexusLabs.Foundry.MicrosoftAgentFramework` and
  `Microsoft.Agents.AI.Harness`.
- A targeted ADR-0008 drift audit classified the record as healthy: every
  verifiable dependency, ownership, generated-tool, and progress claim matches
  the current repository; all related ADR/gate references resolve; and no
  duplicate or broken decision relationship exists.
- All local .NET commands set
  `$env:NUGET_PACKAGES='G:\dev\caches\nuget\packages'`.
- Hosted PR #112 checks all passed:
  - [`build-test-pack`](https://github.com/ncosentino/foundry/actions/runs/30206628827/job/89805741050)
  - [`aot`](https://github.com/ncosentino/foundry/actions/runs/30206628827/job/89805741056)
  - [`docs`](https://github.com/ncosentino/foundry/actions/runs/30206628777/job/89805741072)

## Package and dependency disposition

```text
NexusLabs.Foundry.MicrosoftAgentFramework
  -> Microsoft.Agents.AI / MEAI / existing neutral dependencies
  -X-> NexusLabs.Foundry.MicrosoftAgentFramework.Harness
  -X-> Microsoft.Agents.AI.Harness

NexusLabs.Foundry.MicrosoftAgentFramework.Harness
  -> NexusLabs.Foundry.MicrosoftAgentFramework
       (public progress contracts only)
  -> Microsoft.Agents.AI.Harness
  -X-> NexusLabs.Needlr*
```

`HarnessPackageIsolationTests` verifies this direction through assembly
references, project references, and direct `.deps.json` library dependencies.
The package validator independently asserts both required NuGet dependencies.

The core reference was not used to add generated-tool discovery, selected
provider composition, metrics, diagnostics, or Needlr behavior. It exists only
because T079 reuses the public `IProgressReporterAccessor`,
`IProgressReporter`, and progress-event contracts.

## Public API candidate disposition

The candidate is **accepted for G6 integration** and remains prerelease pending
G9 release/API review.

Primary public types:

- `FoundryHarnessAgentFactory`
- `FoundryHarnessAgentConfiguration`
- `FoundryHarnessFeatureSelections`
- `FoundryHarnessEffectiveDefaults`
- `FoundryHarnessFeatureDisposition`
- requested/effective/backing/feature enums

The progress implementation types remain `internal`. The public configuration
contains one required nullable `IProgressReporterAccessor? ProgressAccessor`;
`null` disables Foundry progress, while a non-null accessor enables it. The
caller must establish an active reporter scope for events to reach a sink.

No compatibility shims were added for intermediate G6 candidate shapes because
the package has not been merged to the default branch or released. The
repository instruction prohibiting compatibility shims for former alpha APIs
continues to apply.

## Effective-default and backing disposition

| Dimension | Upstream behavior | Foundry candidate |
|---|---|---|
| Function invocation | Always installed | `AlwaysOnUnavoidable`; exactly one upstream loop |
| Message injection | Always installed | `AlwaysOnUnavoidable` |
| History persistence | Always installed | Upstream in-memory default or caller `ChatHistoryProvider`; backing reported |
| Harness instructions | Built-in text by default | `null` uses default, empty disables, text replaces; backing reported |
| Web search | Default-on | Explicit enable/disable; `web_search` collision rejected |
| File memory | Default-on | Explicit enable/disable; default or caller store reported |
| Agent skills | Default-on | Explicit enable/disable; default or caller source reported |
| Tool auto-approval | Default-on | Explicit enable/disable; default or caller options reported |
| Approval-not-required bypass | Default-on | Explicit enable/disable |
| Approval-response binding | Default-on | Explicit enable/disable |
| OpenTelemetry | Default-on | Explicit enable/disable and source name; upstream remains sole owner |
| Todo provider | Default-on | Explicit enable/disable |
| Agent-mode provider | Default-on | Explicit enable/disable; default or caller options reported |
| Compaction | Flag default is enabled, but inert without backing | Explicit opt-in requiring a caller strategy or both budgets |
| File access | Opt-in | Enabled only by caller store; options require a store |
| Additional context providers | Opt-in | Explicit caller list |
| Background agents | Opt-in upstream | Not exposed; reported limitation |
| Loop evaluators | Opt-in upstream | Not exposed; reported limitation |
| Foundry progress | Not an upstream default | Explicit nullable accessor; emits progress only, never OTel/metrics/diagnostics |

Every configuration property is required. Validation rejects null collections,
blank identities, invalid token/iteration values, ignored compaction inputs,
incoherent feature/backing combinations, caller tool duplicates, and collisions
with enabled built-in tool names.

The built-in name reservation is pinned to MAF 1.15:

- `web_search`
- `todos_*`
- `mode_*`
- `file_memory_*`
- `file_access_*`
- `load_skill`, `read_skill_resource`, `run_skill_script`

Runtime tests invoke each provider and verify the actual tool names reaching
`ChatOptions`. Caller-provided `AIContextProvider` instances may inject dynamic
tool names that cannot be enumerated before execution; those collisions remain
the caller's responsibility.

## Generated-tool ingress

The optional production package performs no reflection or generated-type
discovery. `HarnessBundleGeneratedToolsTests`:

1. loads the test assembly's real generator-emitted `IAIFunctionProvider`
   through `AgentFrameworkGeneratedBootstrap`;
2. resolves a generated function type through a caller-owned service provider;
3. supplies the resulting `AIFunction` through
   `FoundryHarnessAgentConfiguration.Tools`;
4. executes that function through the real upstream bundle loop; and
5. proves generated duplicates and generated/built-in collisions fail through
   the same generic tool validation as hand-authored functions.

Direct NativeAOT execution of a generated bundle application remains G7/T081.

## Loop, telemetry, and progress ordering

Ownership:

- Function loop: upstream `FunctionInvokingChatClient`
- Message injection: upstream `MessageInjectingChatClient`
- Agent telemetry: upstream `OpenTelemetryAgent`
- Model/tool telemetry: upstream `OpenTelemetryChatClient` and upstream tool
  instrumentation
- Foundry contribution: progress events only

Effective progress composition:

```text
FoundryHarnessProgressAgent (optional; progress only)
  -> upstream Harness agent decorators / OpenTelemetryAgent
    -> upstream ChatClientAgent
      -> upstream FunctionInvokingChatClient
        -> upstream message/history/context pipeline
          -> upstream OpenTelemetryChatClient
            -> FoundryHarnessProgressChatClient (optional; progress only)
              -> raw provider IChatClient
```

`FoundryHarnessTelemetryComposition` chains the existing
`FunctionInvokingChatClient.FunctionInvoker` to report tool progress. It never
constructs a second loop. The caller can replace that mutable delegate after
construction, but doing so also replaces the Foundry tool-progress hook and is
documented on the public configuration.

For one successful tool round, deterministic progress order is:

```text
AgentInvoked
LlmCallStarted
LlmCallCompleted
ToolCallStarted
ToolCallCompleted
LlmCallStarted
LlmCallCompleted
AgentCompleted
```

The matching OpenTelemetry fixture observes exactly one `invoke_agent`
operation, two `chat` operations, and one `execute_tool` operation using the
`gen_ai.operation.name` semantic-convention tag. Enabling Foundry progress does
not change those counts.

The progress tests also cover:

- progress disabled with an active scope;
- progress enabled without an active scope;
- model failure;
- tool failure converted by the upstream loop;
- normal streaming with token aggregation;
- early stream abandonment;
- inner enumerator acquisition failure;
- inner enumerator disposal failure; and
- two forced-overlap runs on one agent with isolated workflow/event streams.

Every started agent/model sequence receives exactly one terminal event. Genuine
enumeration and cleanup exceptions retain their original stack and propagate.
Early consumer abandonment reports deterministic failure events but does not
invent an exception for the caller.

## API-candidate review dispositions

Reviews were assigned disposable detached worktrees. One reviewer nevertheless
created an untracked root-level scratch probe (`test_iterator.cs`) in the live
worktree; it was detected, inspected, and deleted before commit. No reviewer
modified a tracked live-worktree file.

| Finding | Disposition |
|---|---|
| Missing upstream option mappings and backing truth | Fixed in G6.1; public config and backing report expanded |
| Incorrect claim that history reduction ignored `DisableCompaction` | Fixed; docs and validation now match decompiled/runtime 1.15 behavior |
| Caller tools could shadow built-in provider tools | Fixed; every in-scope built-in name is reserved and runtime-pinned |
| Opaque wrappers can hide existing loop/telemetry middleware | Accepted platform limit; best-effort contract documented, not a security boundary |
| `MaxContextWindowTokens` ignored with explicit strategy | Fixed; ignored input rejected |
| `MaxOutputTokens = 0` rejected despite upstream support | Fixed; non-negative values propagate and are runtime-pinned |
| Hidden progress lookup through `IServiceProvider` | Fixed; replaced by explicit nullable `ProgressAccessor` |
| Early stream abandonment omitted terminal events | Fixed and regression-tested |
| Enumerator acquisition could bypass cleanup/restoration | Fixed and regression-tested |
| `DisposeAsync` failure could be swallowed on abandonment | Fixed and regression-tested |
| Concurrent runs lacked a dedicated isolation test | Fixed with forced-overlap one-agent test |
| Removing intermediate candidate members was a compatibility break | Not applicable: package has not reached default branch or a release; no alpha shim added |

Final closing reviews:

- MAF specialist (`claude-opus-4.8`): no blockers.
- Code reviewer (`gpt-5.4`): no blockers after streaming fixes.
- Rubber duck (`gemini-3.1-pro-preview`): no blockers.

## Accepted limitations and deferred work

- Background agents and loop evaluators remain unexposed.
- Cancellation uses existing failure progress events because no cancellation
  event exists.
- The optional package acquires the whole neutral core dependency closure for
  a narrow progress-contract dependency. A new abstractions package is deferred
  unless G7/release evidence demonstrates a concrete deployment or trimming
  blocker.
- Effective defaults, built-in tool names, service discovery, and activity
  classification are version-pinned and must be re-proved on upgrade.
- Direct NativeAOT execution, deterministic scenario infrastructure, analyzer
  guidance, hosted comparisons, and release guidance remain G7-G9.

## Gate completion evidence

- G6.2 was published through PR #112 to `harness/g6-integration`.
- Hosted `build-test-pack`, AOT, and documentation checks passed.
- The merged implementation head is `32f9854d91b58820a329b5a1278e02de3da7dfb0`.
- ADR-0008 is accepted and its targeted drift audit is healthy.
- Every blocking API-candidate review finding is resolved.
