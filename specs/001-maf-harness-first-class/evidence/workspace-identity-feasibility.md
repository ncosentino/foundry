# G2 Workspace Identity and AgentFileStore Feasibility

## Decision

**FEASIBLE AS AN INTERNAL, EXPLICITLY PARTIAL BRIDGE**

Foundry can adapt an authorized `IWorkspace` to MAF 1.15 `AgentFileStore` for
ordinary write, existence, and bridge-bounded listing. Read requires an explicit
missing-file classifier. Search and directory creation are conditional
capabilities rather than generic guarantees. The bridge cannot honestly claim
complete semantic parity:

- `IWorkspace` has no delete operation;
- upstream ordinary writes carry no expected version/content and therefore
  cannot provide Foundry compare-exchange semantics;
- `IWorkspace` is synchronous, so an in-progress call cannot be interrupted;
- `AgentFileStore` carries no user, tenant, session, or workspace identity;
- `IWorkspace` listing and content operations have no built-in result-size or
  bounded-read guarantees;
- `IWorkspace` has no typed missing-file outcome or file-size metadata contract.

The bridge must remain internal until G4 proves the selected subset and explicit
failure mapping.

## Source contracts

### MAF `AgentFileStore`

MAF 1.15 exposes asynchronous methods for:

- `WriteAsync`
- `ReadAsync`
- `DeleteAsync`
- `ListChildrenAsync`
- `FileExistsAsync`
- `SearchAsync`
- `CreateDirectoryAsync`

Paths are implementation-root-relative, use forward slashes, and must not escape
the root. The abstract API has no compare-exchange input, caller identity, or
structured error union.

### Foundry `IWorkspace`

Foundry exposes synchronous:

- `TryReadFile`
- `TryWriteFile`
- `FileExists`
- `GetFilePaths`
- `ReadFileAsMemory`
- `ListDirectory`
- `TryCompareExchange`

Every path must use `WorkspacePath` canonicalization and
`WorkspacePath.PathComparer`. Parent traversal is rejected. Operation failures
use `WorkspaceResult<T>`, while invalid path arguments throw directly.

`IWorkspace` is intentionally not globally registered. A trusted orchestration
creates the authorized workspace instance and attaches it to
`IAgentExecutionContext`.

## Operation mapping

| `AgentFileStore` operation | Foundry mapping | Feasibility | Required behavior |
|---|---|---|---|
| `WriteAsync` | `TryWriteFile` | Supported ordinary write | Canonicalize first; check cancellation before and after; throw the explicit workspace failure; never claim CAS |
| `ReadAsync` | `TryReadFile` | Conditional | Return content on success; map missing to `null` only through an explicit workspace-specific missing-file classifier; surface every other failure |
| `DeleteAsync` | none | Unsupported | Fail explicitly; do not return a success-shaped `false` for an unsupported capability |
| `ListChildrenAsync` | derive from `GetFilePaths` | Supported with limits | Canonicalize directory; return direct children only; directories before files; deduplicate case-insensitively; enforce entry cap |
| `FileExistsAsync` | `FileExists` | Supported | Canonicalize and fail closed on invalid path |
| `SearchAsync` | `GetFilePaths` + `TryReadFile` | Unsupported generically | `IWorkspace` cannot inspect size or perform bounded reads before allocating full content; enable only through a separately proven bounded-search adapter |
| `CreateDirectoryAsync` | implicit directory model | Partial | A no-op is valid only for profiles that accept file-materialized directories; created empty directories are not observable through listing or search |

Profiles must remove or disable every tool/provider path that can invoke an
unsupported operation. Predictable runtime exceptions are a final fail-closed
guard, not the capability-selection mechanism presented to an LLM.

`ListChildrenAsync` must scan the complete `GetFilePaths()` result and filter it
in memory. An output cap does not bound that O(total workspace files) scan.
Large-workspace performance therefore remains an explicit G4 feasibility and
measurement concern.

## Compare-exchange boundary

`TryCompareExchange` remains available to Foundry-native callers and is not
weakened.

`AgentFileStore.WriteAsync(path, content)` supplies no expected content, version,
ETag, or conditional token. Mapping it to `TryCompareExchange` would require
inventing an expectation and would provide false concurrency guarantees.
Ordinary upstream writes therefore map only to `TryWriteFile`.

Hybrid artifact writes should use collision-resistant or content-addressed paths
so ordinary writes do not contend with mutable human-authored workspace files.

## Trusted identity binding

`AgentFileStore` methods contain no identity parameter. A path or restored
session value must never select a workspace.

Two internal construction models are possible:

| Model | Benefit | Risk |
|---|---|---|
| Per-execution store bound directly to one authorized `IWorkspace` and immutable execution identity | Strongest isolation; no ambient lookup | Requires per-execution agent/profile construction or safe provider rebinding |
| Store resolves `IAgentExecutionContextAccessor.Current` on every call | Supports reusable agent composition | AsyncLocal scope must flow; background/late calls must fail closed; a missing or changed context cannot fall back |

The initial bridge requires per-execution binding. Ambient resolution is not
approved by this feasibility gate. A later ambient design would require separate
evidence and must never use a singleton bridge. If reconsidered, every operation
must:

1. require a current trusted execution context;
2. obtain the already-authorized workspace from that context;
3. validate the expected immutable user/orchestration binding;
4. reject calls after scope completion or identity change;
5. ignore all workspace identity found in model input or restored session state.

Any ambient bridge must be scoped or transient, bind an immutable expected
execution identity at construction, and compare it with the ambient context on
every operation. Missing, changed, or late ambient context fails closed.

`UserId` is an audit identity, not an authorization decision by itself. The host
is responsible for constructing an `IWorkspace` whose capabilities are already
limited to that user/tenant.

## Session and isolation constraints

- One store instance must never retain a workspace selected by a prior session.
- Restored `AgentSession.StateBag` may restore provider state, but cannot restore
  or replace the host-authorized workspace.
- Background agents must receive an explicit trusted derived context or fail
  closed; ambient state must not silently flow beyond its intended lifetime.
- File memory and file access are separate capabilities. Enabling private memory
  does not authorize broad workspace access.
- `AgentFileStore` paths are workspace-relative logical paths, never operating
  system paths.

## Cancellation

The adapter methods are asynchronous because the upstream contract is
asynchronous; the underlying `IWorkspace` calls are synchronous.

The bridge can:

- call `ThrowIfCancellationRequested()` before canonicalization and before each
  synchronous operation;
- check again after the operation and between listing/search items;
- propagate cancellation through asynchronous loops.

The bridge cannot:

- interrupt an already-running synchronous workspace call;
- claim atomic cancellation after a write may already have completed;
- use `Task.Run` to create false interruption semantics.

Capability evidence must distinguish pre-call cancellation from mid-call
interruption.

G4 must decide whether a specific blocking workspace implementation needs a
bounded execution scheduler. If synchronous work is offloaded, concurrency must
be bounded and cancellation cannot be surfaced until the underlying operation
has completed; the operation is never abandoned while it may still mutate
state.

## Failure mapping

| Foundry outcome | Upstream behavior |
|---|---|
| Invalid path | Preserve `ArgumentNullException` / `ArgumentException` |
| Missing read | Return `null` only through a proven missing-file classifier |
| Missing existence check | Return `false` |
| Workspace operation failure | Surface the contained exception; do not return success-shaped data |
| Unsupported delete | Explicit `NotSupportedException` or a profile that does not expose delete-dependent tools |
| Entry limit exceeded | Explicit bounded-result failure, never silent truncation unless the public contract identifies truncation |
| Cancellation before operation | `OperationCanceledException` with the caller token |

The upstream interface cannot represent Foundry's result union. G4 must verify
which provider/tool callers preserve surfaced exceptions and whether unsupported
operations require capability removal instead of runtime failure.

`FileNotFoundException` is used by `InMemoryWorkspace` but is not guaranteed by
the `IWorkspace` interface. A generic bridge therefore requires an explicit
classifier or a future typed workspace failure contract.

## Security and abuse boundaries

- Re-run `WorkspacePath` canonicalization for every operation.
- Reject parent traversal, rooted/absolute paths, and root-equivalent file paths.
- Never use path strings as workspace/tenant selectors.
- Do not expose generic search until a workspace implementation can bound reads
  before allocating content; implementation-specific search still requires a
  regex timeout and file/content/result caps.
- Do not include file contents, user IDs, or tenant IDs in diagnostics.
- Record operation category, canonical path category, execution correlation,
  result category, and cancellation state.
- Do not infer operating-system sandboxing from read-only tools or approval
  policy.

## API and AOT impact

- `AgentFileStore` and custom store options are experimental (`MAAI001`).
- The bridge belongs in the neutral
  `NexusLabs.Foundry.MicrosoftAgentFramework` package only if it references no
  Needlr type.
- The candidate remains internal; no new public Foundry abstraction is required.
- The implementation can remain AOT-safe because it uses direct interfaces,
  explicit result mapping, and no reflection-based activation.

## Constraints for dependent work

### G2 T021/T025

- Define and test a fail-closed per-execution workspace binding.
- Test missing context, changed identity, opt-out behavior, and non-adopter
  behavior before composition.
- Keep ambient resolution unresolved unless separate evidence justifies it.

### G4 T041-T056

- Implement only the supported subset above.
- Add path, traversal, isolation, ordinary-write contention, cancellation,
  missing-classification, unsupported-delete, partial-directory, and bounded-list
  fixtures.
- Include empty-directory visibility and large-workspace listing-cost fixtures.
- Disable delete-, generic-search-, and empty-directory-dependent callers rather
  than exposing predictable runtime failures.
- Require a separately proven bounded-search adapter before enabling search.
- Keep Foundry `IWorkspace` authoritative and disable the default file-system
  memory store for the hybrid profile.

## Sources

- [MAF AgentFileStore 1.15.0](https://github.com/microsoft/agent-framework/blob/dotnet-1.15.0/dotnet/src/Microsoft.Agents.AI/Harness/FileStore/AgentFileStore.cs)
- [MAF FileSystemAgentFileStore 1.15.0](https://github.com/microsoft/agent-framework/blob/dotnet-1.15.0/dotnet/src/Microsoft.Agents.AI/Harness/FileStore/FileSystemAgentFileStore.cs)
- `src/NexusLabs.Foundry.MicrosoftAgentFramework/Workspace/IWorkspace.cs`
- `src/NexusLabs.Foundry.MicrosoftAgentFramework/Workspace/WorkspacePath.cs`
- `src/NexusLabs.Foundry.MicrosoftAgentFramework/Context/IAgentExecutionContext.cs`
- `src/NexusLabs.Foundry.MicrosoftAgentFramework/Context/AgentExecutionContext.cs`
