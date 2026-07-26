# Harness Analyzer Feasibility

## Decision

**APPROVE NO NEW HARNESS ANALYZER.**

No candidate misuse is simultaneously:

1. fully statically decidable with an acceptable false-positive and
   false-negative profile;
2. non-redundant with compiler, source-generator, runtime, package, or
   trim/NativeAOT enforcement; and
3. reachable enough to optional-bundle consumers to justify a permanent
   analyzer rule.

Issue #53 must therefore close with the no-analyzer disposition. T088-T090 do
not execute, no `FDRY` diagnostic ID is allocated, and
`AnalyzerReleases.Unshipped.md` remains unchanged.

## Existing enforcement layers

### Compiler

Every property on `FoundryHarnessAgentConfiguration` and
`FoundryHarnessFeatureSelections` is `required`. Missing configuration is a
compiler error, while nullable reference diagnostics force callers to
acknowledge optional upstream backings explicitly.

### Source generator and existing Foundry analyzers

The generator owns compile-time function metadata. Existing analyzers already
cover declarative, statically knowable mistakes such as missing function
descriptions, invalid generated function types, unresolved function groups,
and topology errors. These rules operate on attributes and symbols whose
meaning is available in one compilation.

### Runtime construction guards

`FoundryHarnessAgentFactory` validates complete runtime values before an agent
is constructed. It rejects invalid token budgets, ignored compaction inputs,
incoherent feature/backing combinations, duplicate and built-in-colliding tool
names, and discoverable pre-existing loop, message-injection, or OpenTelemetry
middleware.

`HarnessScenarioRunner` similarly resolves generated functions without a
reflection fallback and fails before construction when a declared type is
missing or distinct types produce duplicate tool names.

These checks see the actual `IChatClient`, `AITool` instances, generated
provider, options objects, and DI scope. A Roslyn analyzer does not.

### Package and architecture checks

Package validation and isolation tests enforce the selected-provider versus
complete-bundle dependency direction and the absence of Needlr from neutral
packages.

### Trim and NativeAOT tooling

The minimum supported Harness profile publishes with reflection disabled,
trim/AOT analyzers enabled, and IL trim/AOT warnings promoted to errors.
`AotHarnessApp` is then executed as a native binary. Roslyn cannot improve on
the linker and NativeAOT compiler's reachability evidence.

## Candidate matrix

| Candidate rule | Static decidability | Existing enforcement | False-positive / false-negative risk | Non-redundant? | Disposition |
|---|---|---|---|---|---|
| Missing required configuration values | Complete | C# required-member and nullability diagnostics | None | No | Reject |
| Compaction, budget, store, source, or feature coherence | Literal object initializers only | Complete fail-closed factory validation | High false-negative rate for variables, `with` expressions, DI, and configuration binding | No | Reject |
| Duplicate caller tools or collisions with upstream built-ins | Not complete; names are runtime `AITool.Name` values | Complete fail-closed factory validation over the final tool list | Near-total false negatives for generated, DI-composed, or provider-injected tools | No | Reject |
| Missing generated function registration | Only narrow literal `typeof(T)` cases | Generator diagnostics plus fail-closed generated-provider resolution | High false-negative rate for computed type lists; intent is not syntactically knowable | No | Reject |
| Prewrapped clients or duplicate loop/telemetry ownership | Not decidable through DI and middleware builders | Runtime `GetService` checks over the constructed client graph | Static rule cannot see the final middleware graph | No | Reject |
| Direct caller use of upstream `AsHarnessAgent` | Direct calls are syntactically visible | Direct upstream use is legitimate in compatibility probes and non-Foundry integrations | A blanket rule would reject supported upstream usage; a narrow policy belongs in generic banned-API tooling | No | Reject |
| Unsupported trim/NativeAOT path | Not a Roslyn-level reachability question | .NET trim and NativeAOT analyzers/compiler with warnings as errors | Any source approximation is weaker than IL reachability | No | Reject |
| Progress accessor supplied without an active scope | Runtime `AsyncLocal` state | Deliberate no-op behavior | Undecidable at the call site | Not statically enforceable | Reject |
| Post-construction replacement of `FunctionInvoker` | Temporal and alias-dependent | Documented runtime limitation | Flagging assignments would catch legitimate framework composition and miss indirect mutation | Not statically enforceable | Reject |
| Opaque wrapper that does not forward `GetService` | Not decidable | Documented caller contract; even runtime inspection cannot unwrap arbitrary implementations | Any static approximation is false-positive-hostile and incomplete | Not statically enforceable | Reject |
| Selected-provider / bundle package misuse | Not represented by one caller syntax | Project references, package validation, and isolation tests | A caller may legitimately reference either or both optional lanes | No | Reject |

## Strongest rejected candidates

### Configuration coherence

An analyzer could inspect a literal `FoundryHarnessAgentConfiguration`
initializer and report obvious contradictions. That rule would miss common
production shapes where values come from variables, options binding, helper
methods, record `with` expressions, or DI. The factory already rejects every
shape using the final runtime values on the first `Create` or
`DescribeEffectiveDefaults` call. A literal-only rule would add maintenance
cost while duplicating a deterministic failure for a small subset of callers.

### Scenario generated function types

A narrow rule could inspect a literal
`IHarnessScenario.GeneratedFunctionTypes` collection and check for
`[AgentFunctionGroup]`. The getter is arbitrary runtime code, so the rule would
have high false negatives. The scenario runner already rejects a missing
generated type before constructing the agent, and the source generator owns
the authoritative registration semantics.

### Progress and post-construction mutation

The only silent candidate behaviors are an absent active progress scope and a
caller replacing a mutable upstream invocation hook. Both depend on runtime
ambient or temporal state. They are precisely the cases Roslyn cannot decide
without broad, unreliable alias and whole-program analysis.

### Direct upstream bundle construction

Calling `AsHarnessAgent` directly is fully visible when it appears as a direct
invocation, but it is not universally a misuse. Foundry retains upstream
compatibility probes, and consumers may intentionally use MAF without the
Foundry optional package. If a repository later bans direct use by local
policy, generic banned-symbol tooling is a more accurate mechanism than a
permanent Foundry Harness diagnostic.

## Analyzer package reach

The optional Harness package references the neutral core and upstream Harness,
but does not carry the standalone Foundry analyzer package transitively.
Consumers opt into Foundry analyzers explicitly. A new Harness rule would
therefore have limited default reach even before considering its decidability
and redundancy problems.

## Review disposition

- Microsoft Agent Framework specialist review: approve none.
- Independent rubber-duck challenge: approve none.
- No candidate cleared the static-decidability and non-redundancy fence.

The no-analyzer result is deliberate evidence, not missing implementation.
Shipping a partial object-initializer rule merely to allocate a diagnostic ID
would violate the G7 requirement that no speculative analyzer ships.
