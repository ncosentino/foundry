# Gate G3 Decision — Session-Continuity (History) Slice

## Decision

**PASS FOR INTERNAL SELECTED-PROVIDER SESSION-CONTINUITY SLICE**

The MAF 1.15 / MEAI 10.6 selected-provider lane can advance one stable
capability slice: per-service-call chat history persistence with in-memory,
serialized, and caller-supplied durable-provider modes. This gate approves
only that slice's internal composition, session envelope, and capability
coherence evidence.

No public Foundry Harness API, service-managed (service-issued conversation
identifier) history, workspace serialization/restoration, `ChatReducer`
compaction, or complete-bundle behavior is approved by this gate.

## Evidence identity

- G2 baseline: `57193a74bbfe2d9b383dacaefb7d9c237f73eea2`
  (`feat: establish Harness composition foundation (#83)`)
- G3 history branch: `harness/g3-history`
- Local deterministic validation (this slice):
  - `HarnessHistoryProviderTests` in isolation: 29 passed, 0 failed.
  - Full Harness filter (`FullyQualifiedName~Harness`): 76 passed, 0 failed
    (47 pre-existing G2 tests + 29 G3 history tests; no regressions).
  - Full `NexusLabs.Foundry.MicrosoftAgentFramework.Tests` project:
    1,645 passed, 0 failed.
- No hosted CI run was performed for this slice; no commit, push, or PR was
  made as part of this work.

## Gate evidence

| Criterion | Result | Evidence |
|---|---|---|
| G2 composition and runtime order remain unmodified | Pass | `HarnessCompositionGuard.Validate(chatClient, profile)` still rejects `PerServiceHistory` at the G2 lane (`HarnessLoopOwnershipTests.Validate_LaterPhaseCapability_IsRejectedByG2Composition`). `HarnessProviderComposition.Compose` remains the only selected-provider composition root; it opts into `PerServiceHistory` only when a coherent `HarnessHistoryProviderPlugin` is supplied and otherwise preserves the original G2 path unchanged. |
| Runtime order extended, not broken | Pass | `HarnessProviderComposition.Compose` now extends the same `ChatClientBuilder` chain in `.Use(...)` order FICC -> (optional message injection) -> `HarnessExecutionBindingChatClient` -> `.UsePerServiceCallChatHistoryPersistence()` -> telemetry when `HistoryProvider` is present, so trusted binding is validated before per-service history state is read or mutated, and every provider call still crosses the binding guard. |
| Composed upstream history, not reimplemented | Pass | The slice uses `ChatClientAgentOptions.ChatHistoryProvider`, `ChatClientAgentOptions.RequirePerServiceCallChatHistoryPersistence`, and `ChatClientBuilderExtensions.UsePerServiceCallChatHistoryPersistence` exactly as MAF 1.15 exposes them. No Foundry-owned `ChatHistoryProvider` implementation exists; `InMemoryChatHistoryProvider`/`InMemoryChatHistoryProviderOptions` (both upstream MAF types) back the `InMemory`/`Serialized` modes, and `DurableProvider` mode composes a caller-supplied `ChatHistoryProvider` instance as-is. |
| No compaction/reducer enabled | Pass | Neither `HarnessHistoryProviderPlugin` nor `HarnessGuardedAgent` reference `ChatReducer` or any compaction option; G5 remains untouched. |
| History session create/serialize/deserialize validate the trusted binding | Pass | When the history plugin is enabled, `HarnessGuardedAgent.CreateSessionCoreAsync`, `SerializeSessionCoreAsync`, and `DeserializeSessionCoreAsync` call `HarnessExecutionBinding.EnsureCurrent` before delegating to the base MAF implementation and again afterward. Profiles without history keep their prior raw MAF session lifecycle, verified by `Compose_NoHistoryPreservesExistingSessionLifecycleBehavior`. |
| Foundry-owned session envelope | Pass | `SerializeSessionCoreAsync` wraps the inner MAF session JSON in `HarnessSessionEnvelope` (schema version, user id, orchestration id, session id, persistence mode, provider state keys, inner session), serialized through the source-generated `HarnessSessionEnvelopeJsonContext`. No reflection-based `JsonSerializer` fallback is used anywhere in the envelope path. |
| Fail-closed restore on identity/session/mode/state-key mismatch | Pass | `DeserializeSessionCoreAsync` validates binding first (before the payload is even parsed), then validates schema version, user/orchestration identity, session id, persistence mode, and provider state keys, throwing a descriptive `InvalidOperationException` on any mismatch. Covered by six dedicated tampering tests (`DeserializeSession_MismatchedSessionId_FailsClosed`, `DeserializeSession_MismatchedUserIdentity_FailsClosed`, `DeserializeSession_SchemaVersionMismatch_FailsClosed`, `DeserializeSession_PersistenceModeMismatch_FailsClosed`, `DeserializeSession_ProviderStateKeyMismatch_FailsClosed`, `DeserializeSession_MalformedEnvelope_FailsClosed`). |
| Workspace never serialized or restored | Pass | `HarnessSessionEnvelope` has no workspace-shaped member; recursive envelope inspection rejects any workspace-shaped property. A stale agent binding fails if its ambient workspace changes, while a freshly composed agent with the same trusted user/orchestration/session identity restores serialized history under its own newly authorized workspace. Serialized state never selects either workspace. |
| Capability coherence reported | Pass | `HarnessCapabilityResolver` reports `HistoryPersistenceMode` on the profile. A selected `PerServiceHistory` capability without an explicit supported mode is `Deferred`; a requested mode without the capability is non-executable. `InMemory` is explicitly non-durable, `Serialized` requires caller-owned persistence, `DurableProvider` relies on a caller-supplied durable provider, and `ServiceManaged` remains `Deferred` until the selected-provider profile receives provider-specific evidence. |
| Fail-closed composition guard | Pass | `HarnessHistoryCompositionGuard` is now limited to profile/plugin coherence: it rejects history enabled without a plugin, a plugin when history is disabled, a requested mode that disagrees with the plugin's mode, and unsupported `NotApplicable`/`ServiceManaged` history modes. `HarnessCompositionGuard` still owns the selected-provider lane, executability, loop, message-injection, and telemetry checks, and `HarnessProviderComposition.Compose` maps the focused history guard to shared `HarnessProviderCompositionStatus` values. |
| Non-Azure deterministic execution | Pass | Scripted local `IChatClient` fixtures compose the history slice, run a generated tool, and persist/restore history without Azure hosting or credentials. |
| Strict G2 per-run options contract preserved | Pass | `HarnessGuardedAgent.EnsureSupported` is unchanged: any non-null `AgentRunOptions` on `RunAsync`/`RunStreamingAsync` is still rejected for every composed agent, including history-enabled agents produced by the shared composition root. |

## Stop-condition evaluation

- G2 runtime order and rejection of `PerServiceHistory` at G2 remain intact: **yes**
- Session create/serialize/deserialize are guarded by the trusted binding: **yes**
- Restored state fails closed on identity/session/mode/state-key mismatch: **yes**
- The active, host-authorized workspace is never serialized, restored, or bypassed: **yes**
- History composition reuses upstream MAF `ChatHistoryProvider`/`UsePerServiceCallChatHistoryPersistence` without reimplementation: **yes**
- `ChatReducer`/compaction remains out of scope (G5): **yes**
- Capability evidence distinguishes in-memory from durable/session persistence, and a capability/mode mismatch in either direction is non-executable: **yes**
- Ordinary G2 selected-provider behavior (no history requested) is unchanged: **yes**

The G3 history stop condition is not triggered.

## Supported and deferred modes

| Mode | Status | Rationale |
|---|---|---|
| `NotApplicable` | Supported (default/off) | No per-service history is requested; the G2 path is used unchanged (`HarnessProviderComposition` still passes `NotApplicable` and an empty state-key list into `HarnessGuardedAgent`). |
| `InMemory` | Supported | Backed by upstream `InMemoryChatHistoryProvider`. History lives only in the in-process `AgentSession`; explicitly non-durable across process restarts. |
| `Serialized` | Supported, caller-persisted | Same in-process `InMemoryChatHistoryProvider` backing as `InMemory`. The state is serializable, but Foundry does not make it durable; the caller must persist and restore the envelope. |
| `DurableProvider` | Supported | The caller supplies its own `ChatHistoryProvider` instance backed by storage it owns; Foundry composes it as-is and never reads or writes its durable backing store directly. |
| `ServiceManaged` | Deferred | MAF 1.15 supports service-stored conversation identifiers, but this slice has no approved provider-specific capability evidence. The resolver does not infer support from provider names or invent runtime negotiation. |

## State serialization limits

- The Foundry-owned `HarnessSessionEnvelope` (schema version 1) is the only
  thing Foundry serializes or restores. It carries: schema version, user id,
  orchestration id, session id, persistence mode, provider state keys, and the
  inner MAF session JSON element produced by `base.SerializeSessionCoreAsync`.
  The inner session JSON itself is treated as an opaque MAF-owned blob; Foundry
  does not inspect or reshape it beyond passing it through
  `HarnessSessionEnvelopeJsonContext` (source-generated, no reflection
  fallback).
- Restore is fail-closed on: execution binding not current (checked before the
  payload is even parsed), malformed/non-envelope JSON, schema version
  mismatch, user/orchestration identity mismatch, session id mismatch,
  persistence mode mismatch, and provider state key set mismatch (order- and
  case-sensitive comparison via `SequenceEqual`/`StringComparer.Ordinal`).
- **Known limitation**: MAF 1.15's `PerServiceCallChatHistoryPersistingChatClient`
  (the internal decorator installed by `UsePerServiceCallChatHistoryPersistence`)
  is `internal` to the MAF assembly and cannot be referenced by name from
  Foundry code. Unlike G2's guard, which can detect an existing
  `FunctionInvokingChatClient`/`MessageInjectingChatClient` already present on
  the raw chat client, the shared composition path cannot perform the
  equivalent "existing history-persistence decorator already present" check
  by type identity. This is an accepted, documented gap for this slice; it
  does not weaken the fail-closed binding/identity/mode checks, which do not
  depend on detecting that internal type.
- Provider-state restoration is exercised for a caller-held `DurableProvider`
  and for `Serialized` mode across two independently composed agents. Tests do
  not depend on `AIAgent.GetService<ChatHistoryProvider>()`; the internal plugin
  retains the configured provider reference for conformance evidence.

## Active-workspace authority

The currently host-authorized `IWorkspace` is never a member of
`HarnessSessionEnvelope` and is never read from or written to serialized
session state. A stale agent binding fails closed if the ambient workspace
changes. A newly composed agent may restore the same envelope under a different
workspace only after the host creates a fresh trusted binding with the same
user, orchestration, and session identity. The restoring agent's workspace is
therefore authoritative; serialized state can never select, restore, or
override it.

## AOT evidence status

`HarnessCapabilityResolver.Definitions` already recorded
`HarnessCapabilityAotStatus.Verified` for `HarnessCapability.PerServiceHistory`
as part of the G2 capability table. That status reflects the capability
*evidence infrastructure* (the resolver correctly reports the capability's
static AOT status), not a direct AOT execution proof of the new G3
history-plugin configuration path inside `HarnessProviderComposition`.
Consistent with the G2 gate's "Direct AOT proof" constraint, no new Harness
AOT application was added or run in this slice — the same gap G2 recorded
(current AOT jobs do not invoke `HarnessProviderComposition`) still applies
here and must be closed by the later minimum Harness AOT application before
profile promotion.

## API disposition

- History envelope, persistence-mode model, focused composition guard, and
  history-provider plugin types: internal candidate only, one type per file,
  no public members beyond what the assembly's existing `InternalsVisibleTo`
  already exposes to the test project.
- No `Microsoft.Extensions.DependencyInjection`/Needlr references were added.
- No optional parameters or default interface members were introduced.
- No G4 workspace provider, G5 compaction/reducer, approval, todo, skills, or
  web-search behavior was added or modified.
- Public Foundry Harness API: not approved.
- Package publication: none from this gate.
- Ordinary non-Harness and non-history-requesting G2 behavior: unchanged.

## Next permitted work

Proceed with the next independently gated stable selected-provider slice
(todo/agent-mode, tasks T032-T033, recorded separately as
`gate-g3-planning.md` when delivered) or tool approval, per the G3 task
sequence. `ServiceManaged` history persistence remains blocked pending
provider-specific Foundry evidence; do not infer support or add runtime
negotiation without a new evidence-backed gate decision.
