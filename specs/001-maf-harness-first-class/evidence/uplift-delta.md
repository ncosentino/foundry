# G1 MAF 1.15 / MEAI 10.6 Uplift and Lifecycle Delta

## Scope

This report combines source inspection with deterministic execution of
`HarnessCompatibilityProbe`. It records the package, public API, middleware,
history, compaction, message-injection, approval, and generated-tool seams that
G2-G5 may rely on.

## Executed lifecycle traces

### Custom history provider

```powershell
dotnet run --project src\Examples\AgentFramework\HarnessCompatibilityProbe\HarnessCompatibilityProbe.csproj --configuration Release --no-build -- --lifecycle
```

```text
context.provide
history.provide
compaction:call=False:result=False:messages=3
chat.call:1
history.seen
injected.seen
context.seen
history.store:2:1
context.store
function.invoker:Echo:probe-call
history.provide
chat.call:2
history.seen
context.seen
history.store:1:1
context.store
```

### Default in-memory history

```powershell
dotnet run --project src\Examples\AgentFramework\HarnessCompatibilityProbe\HarnessCompatibilityProbe.csproj --configuration Release --no-build -- --lifecycle --default-history
```

```text
context.provide
compaction:call=False:result=False:messages=2
chat.call:1
injected.seen
context.seen
context.store
function.invoker:Echo:probe-call
compaction:call=True:result=False:messages=3
chat.call:2
injected.seen
context.seen
context.store
```

Both runs return `tool-result:aot` with exit code 0.

## Proven runtime seams

### Generated tool ingress and raw-result transformation

- Generated `IAIFunctionProvider` output is accepted directly through
  `HarnessAgentOptions.ChatOptions.Tools`.
- `FunctionInvokingChatClient` is discoverable through
  `HarnessAgent.GetService<FunctionInvokingChatClient>()`.
- Its public `FunctionInvoker` delegate executes before MEAI constructs
  `FunctionResultContent`.
- The trace observes the exact generated tool and call ID:
  `function.invoker:Echo:probe-call`.

**Disposition:** A generic eager-offload seam exists before ordinary
`FunctionResultContent` construction. Harness options do not expose that seam
directly; Foundry must configure the discovered FICC instance or build the
selected-provider pipeline explicitly.

### Function loop and intermediate service calls

- The deterministic tool request causes exactly two model service calls.
- `MaximumIterationsPerRequest` maps directly to FICC's iteration limit.
- The generated tool result is sent into the second service call and returns the
  expected final response.

MEAI 10.6 FICC defaults recorded for G2 composition are:

| Setting | Default |
|---|---:|
| `MaximumIterationsPerRequest` | 40 |
| `MaximumConsecutiveErrorsPerRequest` | 3 |
| `AllowConcurrentInvocation` | `false` |
| `IncludeDetailedErrors` | `false` |
| `TerminateOnUnknownCalls` | `false` |

### Message injection

- `MessageInjectingChatClient` is discoverable from the constructed agent.
- A queued `injected-probe` message reaches the first service call.
- With the default history provider, the injected message is persisted and also
  appears on the second service call.

**Disposition:** The public queue seam is usable with an explicit
`AgentSession`. Injected content becomes history unless the selected history
provider filters it.

### Per-service-call history callbacks and default storage

With a custom `ChatHistoryProvider`:

- history is loaded before both model calls;
- request and response messages are stored after both model calls;
- the first store observes two request messages and one response;
- the second store observes one request message and one response.

This confirms `UsePerServiceCallChatHistoryPersistence()` loads and notifies the
provider inside the FICC loop. The trace provider deliberately discards stored
messages, so this run proves callback timing rather than durable round-trip
semantics.

With the default in-memory provider, the injected message is visible on both
service calls and the second reducer index contains the stored
`FunctionCallContent`. This proves the default provider round-trips session
history between service calls. Cross-process durability and custom-provider
session restoration remain G3.

### AI context providers

- The custom provider supplies context once at the outer agent invocation.
- Its merged instructions are visible on both service calls.
- result storage notification occurs after both service calls because
  per-service persistence notifies all providers.

**Disposition:** user context provision is outer-invocation scoped, while
provider result notifications are per service call.

## Compaction finding

Harness configures the same `CompactionStrategy` in two places when using its
default history provider:

1. `CompactionProvider` in the chat-client pipeline;
2. `strategy.AsChatReducer()` on `InMemoryChatHistoryProvider`.

The runtime trace proves:

- `CompactionProvider` is inside FICC and runs before the first model call;
- after that call, per-service persistence sets the local-history sentinel as
  the session conversation ID;
- `CompactionProvider` treats any non-empty conversation ID as remote-managed
  history and skips the intermediate second model call;
- with default history, the strategy runs a second time through the history
  reducer before the second history retrieval;
- that second index contains the stored `FunctionCallContent` but not the
  current `FunctionResultContent`.

**Disposition:** Harness 1.15 does not expose a compaction point that sees the
complete current tool-call/result pair before the next model request. The
history reducer can compact previously persisted history per service call, but
the current tool result is merged only after reduction. G5 therefore cannot
assume Harness's `CompactionProvider` alone provides per-tool-round hybrid
compaction. Upstream intent and documentation are tracked in
[issue #73](https://github.com/ncosentino/foundry/issues/73).

## Approval pipeline

- `ToolApprovalAgent` is discoverable on the default Harness agent.
- Harness source registers `ApprovalResponseBindingChatClient` as the outermost
  chat decorator above approval-not-required bypassing and FICC.
- `ApprovalNotRequiredFunctionBypassingChatClient` preserves non-approval calls
  when a response mixes approval-required and ordinary tools.
- `ApprovalRequiredAIFunction` is handled by FICC as a surfaced approval request
  rather than direct invocation.

The `--approval` probe executes two fresh-session flows:

```text
APPROVAL:approved:request=probe-call:invocations=1:result=tool-result:aot
APPROVAL:rejected:request=probe-call:invocations=0:result=tool-result:Tool call invocation rejected. rejected
```

This proves request surfacing, correlated response continuation, exactly-once
execution after approval, and no execution after rejection. The chat decorators
remain internal implementation types, so Foundry depends on their public
behavior rather than their concrete classes. Forged-response handling,
restored-session identity, and standing approvals remain G3.

## Telemetry ordering

Harness places OpenTelemetry below FICC in the chat-client pipeline and adds an
outer agent-level OpenTelemetry decorator unless disabled. The compatibility
probe disables both so G1's lifecycle counts are not contaminated by telemetry.

T013 records the MEAI 10.6 semantic-convention delta. G2 T019 owns one-effective
telemetry-owner and no-duplication tests after Foundry composition exists.

## Source-traced middleware order

Harness 1.15 builds the chat-client stack outer to inner as:

```text
ApprovalResponseBinding
  -> ApprovalNotRequiredFunctionBypassing
    -> FunctionInvokingChatClient
      -> MessageInjectingChatClient
        -> PerServiceCallChatHistoryPersistingChatClient
          -> AIContextProviderChatClient(CompactionProvider)
            -> OpenTelemetryChatClient
              -> caller IChatClient
```

Its agent decorator stack is:

```text
LoopAgent, when configured
  -> ToolApprovalAgent
    -> OpenTelemetryAgent
      -> ChatClientAgent
```

Concrete approval and per-service chat decorators are internal. Foundry can rely
on their public behavior and configured option surface, not their types.

## Public API and behavior delta

### New stable surface

- `Microsoft.Agents.AI.Harness` 1.15.0
- `HarnessAgent`
- `HarnessAgentOptions`
- `IChatClient.AsHarnessAgent(...)`
- stable history, todo, mode, skills, file-memory, and tool-approval providers in
  MAF core

### Experimental surface (`MAAI001`)

- compaction options and strategies;
- custom file stores and file access;
- loop evaluators;
- background agents.

### Material behavior changes

- file access is opt-in through `FileAccessStore`;
- approval responses are bound to surfaced requests;
- approval-not-required calls may be bypassed and retained when mixed with
  approval-required calls;
- group-chat participants are emitted as intermediate workflow outputs, so
  diagnostic stage collection order is not a turn-order contract;
- MEAI uses updated GenAI semantic conventions and stricter function-argument
  handling;
- `WebSearchToolResultContent.Results` became `Outputs`;
- Hosting.OpenAI remains alpha and has Responses protocol changes.

### Foundry surface

- no Foundry public API is added or removed in G1;
- existing ordinary agents retain their construction path;
- the Harness dependency remains isolated to the probe;
- source generators, analyzers, workflows, evaluation, diagnostics, testing,
  package validation, and existing NativeAOT compilation pass.

## Sources

- [HarnessAgent 1.15.0](https://github.com/microsoft/agent-framework/blob/dotnet-1.15.0/dotnet/src/Microsoft.Agents.AI.Harness/HarnessAgent.cs)
- [Per-service history persistence](https://github.com/microsoft/agent-framework/blob/dotnet-1.15.0/dotnet/src/Microsoft.Agents.AI/ChatClient/PerServiceCallChatHistoryPersistingChatClient.cs)
- [CompactionProvider](https://github.com/microsoft/agent-framework/blob/dotnet-1.15.0/dotnet/src/Microsoft.Agents.AI/Compaction/CompactionProvider.cs)
- [InMemoryChatHistoryProvider](https://github.com/microsoft/agent-framework/blob/dotnet-1.15.0/dotnet/src/Microsoft.Agents.AI.Abstractions/InMemoryChatHistoryProvider.cs)
- [MEAI FunctionInvokingChatClient 10.6.0](https://github.com/dotnet/extensions/blob/v10.6.0/src/Libraries/Microsoft.Extensions.AI/ChatCompletion/FunctionInvokingChatClient.cs)
