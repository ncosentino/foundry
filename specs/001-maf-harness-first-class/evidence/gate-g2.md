# Gate G2 Decision

## Decision

**PASS FOR INTERNAL SELECTED-PROVIDER COMPOSITION FOUNDATION**

The MAF 1.15 / MEAI 10.6 selected-provider lane can advance to independently
gated stable capability slices. This gate approves only the internal capability
evidence, generated-tool ingress, per-execution trust binding, deterministic
composition, one-loop guard, aligned telemetry ownership, and safe
message-injection surface.

No public Foundry Harness API, complete Harness bundle, workspace provider,
approval policy, durable history contract, or hybrid context behavior is
approved by this gate.

## Evidence identity

- G1 mainline baseline:
  `be45298ae1dd627318d9c9f557f38f5b1d8a7192`
- G2 integration branch: `harness/g2-integration`
- Final reviewed G2 implementation head:
  `62680984e919f461af727ff4930a9d18a43b34cf`
- Completed implementation and review-fix PRs:
  [#76](https://github.com/ncosentino/foundry/pull/76),
  [#77](https://github.com/ncosentino/foundry/pull/77),
  [#79](https://github.com/ncosentino/foundry/pull/79), and
  [#81](https://github.com/ncosentino/foundry/pull/81)
- G2 decision leaf:
  [#80](https://github.com/ncosentino/foundry/pull/80)
- Final evidence refresh:
  [#82](https://github.com/ncosentino/foundry/pull/82)
- Leaf merge commits:
  `10f279ab93efc1bfef1aafc4293db26944bd7750`,
  `8ec072d6372c923e0aca6500c408183269f053a5`,
  `9c2d2541d4ded809b17adc353739ee5fc0fbb51c`,
  `e7f312a2d6febb68ee09bbb448a69c57f198eeae`, and
  `62680984e919f461af727ff4930a9d18a43b34cf`
- Cumulative local deterministic validation:
  47 Harness tests, 66 generated-wrapper tests, and all 1,616
  `NexusLabs.Foundry.MicrosoftAgentFramework.Tests` tests passed.
- Final cumulative hosted validation:
  [build/test/package](https://github.com/ncosentino/foundry/actions/runs/30081458071/job/89443772271),
  [standard NativeAOT](https://github.com/ncosentino/foundry/actions/runs/30081458071/job/89443772249),
  [Harness NativeAOT](https://github.com/ncosentino/foundry/actions/runs/30081458061/job/89443772066), and
  [documentation](https://github.com/ncosentino/foundry/actions/runs/30081458119/job/89443772649)

## Gate evidence

| Criterion | Result | Evidence |
|---|---|---|
| Coherent MAF/MEAI runtime graph | Pass | JIT tests and the hosted build execute the selected-provider code against MAF 1.15 and MEAI 10.6. Existing standard and Harness AOT probes validate the package and generated-tool paths. The G1 Microsoft.Extensions patch-level runtime risk is closed for this lane. |
| Versioned capability evidence | Pass | `HarnessCapabilityResolver` records schema version 1, the runtime MAF version, middleware order version `maf-1.15.0`, stability, provider requirements, trust, AOT, diagnostics, and delivery evidence for every known capability. |
| Later and complete-bundle capabilities remain gated | Pass | Profiles defer capabilities beyond their evidence and reject the complete-bundle lane before its later gate. Empty or partially enabled profiles are non-executable. |
| Generated-tool ingress | Pass | `HarnessGeneratedToolSource` consumes existing generated `IAIFunctionProvider` output and fails closed on missing or ambiguous generated metadata. All 66 generated-wrapper tests pass. |
| Trusted execution binding | Pass | The immutable user, orchestration, session, and workspace binding rejects missing, changed, or expired context before and after provider calls and immediately before tool execution. |
| Message-injection inner calls are guarded | Pass | Effective order is FICC -> message injection -> execution binding -> telemetry -> leaf. Deterministic streaming and non-streaming fixtures expire identity after the first call and prove no second provider call occurs. |
| Exactly one function loop | Pass | Existing standard or Foundry diagnostics FICC layers are rejected. `UseProvidedChatClientAsIs` prevents MAF from adding another default loop. |
| Exactly one aligned telemetry owner | Pass | Harness/Harness and Foundry/Foundry ownership sets pass. Mixed ownership and pre-instrumented input fail closed. One tool round records one Foundry tool metric and two Foundry model-call metrics. |
| Safe composed service surface | Pass | The guarded agent rejects all per-run `AgentRunOptions`, hides callable `IChatClient`, raw `ChatClientAgent`, and mutable function-loop services, and exposes only a binding-aware internal message injector. |
| Function-invocation services preserved | Pass | The Foundry diagnostics FICC receives the composition `IServiceProvider`; a generated-style test function observes it through `AIFunctionArguments.Services`. |
| Non-Azure deterministic execution | Pass | Scripted local `IChatClient` fixtures execute generated tools, message injection, telemetry, streaming, and ownership guards without Azure hosting or credentials. |
| Ordinary Foundry behavior unchanged | Pass | The non-adopter regression constructs and runs ordinary `AgentFactory` behavior without Harness composition. The full 1,616-test MAF project passes. |
| Workspace bridge boundary understood | Pass for feasibility only | `workspace-identity-feasibility.md` proves a per-execution, explicitly partial bridge is possible and records unsupported delete, generic search, CAS, mid-call cancellation, and ambient singleton semantics. Implementation remains G4 work. |
| Hosted compatibility | Pass with an explicit selected-composition AOT gap | G2 leaf PRs passed build/test/package and documentation. Existing standard and Harness NativeAOT jobs pass for the package and generated-tool paths, but do not directly invoke `HarnessProviderComposition`; the minimum selected-provider AOT app must close that gap before profile promotion. |

## Stop-condition evaluation

- A non-Azure scripted agent runs with generated tools: **yes**
- Capability availability and evidence are inspectable: **yes**
- Exactly one function loop is enforced: **yes**
- Exactly one aligned telemetry owner is enforced: **yes**
- Every provider and tool call crosses the trusted execution boundary: **yes**
- The G1 Microsoft.Extensions runtime risk has a validated migration path: **yes**
- Ordinary opt-out behavior remains unchanged: **yes**

The G2 stop condition is not triggered.

## Architecture disposition

1. **Consumption lanes:** retain the selected-provider lane in
   `NexusLabs.Foundry.MicrosoftAgentFramework`; keep the complete Harness bundle
   isolated and deferred.
2. **Capability contract:** retain the internal versioned evidence profile.
   It is evidence infrastructure, not an approved public API.
3. **Composition order:** for MAF 1.15, register FICC first, message injection
   second, execution binding third, and telemetry last so runtime order is
   FICC -> injection -> binding -> telemetry -> provider.
4. **Ownership:** support only Harness/Harness and Foundry/Foundry loop and
   telemetry ownership pairs. New mixed combinations require separate evidence.
5. **Identity:** construct the selected-provider agent per trusted execution.
   Do not select workspace or identity from model input, paths, restored state,
   or a reusable ambient singleton.
6. **Service discovery:** do not expose live chat middleware below the trust
   guard or forward caller-owned per-run options. Use narrow binding-aware
   surfaces for capabilities that require host access. Any safe run-time
   override requires explicit later evidence.
7. **Opt-in behavior:** ordinary `AgentFactory` and the existing iterative loop
   remain unchanged.

## Review dispositions

Independent MAF, MEAI, code-review, and rubber-duck passes produced the
following blocking findings, all adopted before this gate:

- preserve `IServiceProvider` in the Foundry-owned function loop;
- revalidate identity after non-streaming provider calls;
- reject caller-owned per-run agent options;
- hide callable chat middleware from the composed agent;
- place execution binding inside the message-injection service-call loop;
- exclude the mutable test accessor from Needlr reflection registration; and
- reject run-time capability mutation that bypasses profile selection.

One proposed direct generated-tool bypass was discarded after decompilation
showed `ChatClientAgent.ChatOptions` is internal in MAF 1.15 and not available
through the composed public surface.

Non-blocking streaming tool-call precision and binding-rejection diagnostics
are tracked in [#78](https://github.com/ncosentino/foundry/issues/78). Harness
compaction lifecycle clarification remains tracked in
[#73](https://github.com/ncosentino/foundry/issues/73).

## Constraints carried into later gates

1. **Stable selected-provider slices:** enable one capability slice at a time
   through the evidence profile. Do not expose the complete bundle or publish
   the current internal composition types.
2. **History and approvals:** restored history, forged approval responses,
   standing approval authorization, and exactly-once behavior require their own
   evidence before enablement.
3. **Workspace providers:** implement only the supported bridge subset from
   `workspace-identity-feasibility.md`; unsupported operations must be removed
   from profiles or fail explicitly.
4. **Hybrid context:** do not rely on stock Harness compaction for complete
   current tool-call/result pairs. Preserve the explicit Foundry hybrid
   envelope and offload gate.
5. **Upstream upgrades:** any MAF or MEAI change invalidates the recorded
   middleware-order claim until ordering, service discovery, DI flow, telemetry
   counts, and AOT are re-proven.
6. **Diagnostics:** MEAI may convert a binding exception at `FunctionInvoker`
   into a tool-error result. The chat-level binding guard must remain present
   so the run terminates before another provider call.
7. **Direct AOT proof:** the later minimum Harness AOT application must invoke
   selected-provider composition and execute a generated tool. Current AOT jobs
   do not prove reachability of the new G2 composition glue.

## API disposition

- Capability profile and composition types: internal candidate only.
- Binding-aware message injection: internal candidate only.
- Public Foundry Harness API: not approved.
- Complete-bundle package: not approved before its later gate.
- Package publication: none from this gate.
- Ordinary non-Harness behavior: unchanged.

## Next permitted work

Proceed with independently gated stable selected-provider slices for history,
todo and modes, approvals, skills, and provider-supported web search. Route
non-critical improvements outside those slices to follow-up issues rather than
expanding their acceptance scope.
