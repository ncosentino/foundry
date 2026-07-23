# Contract: Foundry Workspace to MAF File Store Bridge

## Purpose

Allow MAF file-memory and file-access providers to operate over Foundry
`IWorkspace` without making MAF the owner of bulk state.

## Required semantic mapping

| Concern | Required behavior |
|---|---|
| Paths | Use Foundry canonicalization and reject traversal |
| Identity | Bind all operations to trusted execution and workspace identity |
| Reads | Preserve explicit missing and failure results |
| Writes | Preserve explicit write outcomes and canonical actual path |
| Concurrency | Preserve Foundry compare-exchange for Foundry callers; do not claim upstream CAS when the upstream operation supplies no expected version |
| Listing | Return bounded canonical entries |
| Isolation | Prevent cross-session and cross-tenant access |
| Cancellation | Check before every synchronous workspace operation and propagate through any asynchronous upstream operation; do not claim interruption of an in-progress synchronous `IWorkspace` call |
| Diagnostics | Attribute operation, result, path category, and owner without leaking content |

## Authority rule

For the hybrid profile, Foundry workspace is the only authoritative bulk-content
store. The upstream default local file-memory location is disabled or replaced.

## Unsupported semantic handling

If an upstream file-store operation cannot preserve a required Foundry
workspace guarantee, the bridge returns explicit unsupported capability
evidence rather than silently weakening the guarantee.

`IWorkspace` is currently synchronous. The bridge may fail fast before an
operation when cancellation is requested, but it cannot guarantee mid-call
interruption without a separate asynchronous workspace evolution. That
limitation is reported in capability evidence rather than hidden.

Ordinary upstream writes map to documented ordinary workspace-write semantics.
The bridge MUST NOT fabricate compare-exchange guarantees for an upstream API
that has no version or expected-content input. Hybrid offload SHOULD use
content-addressed or otherwise collision-resistant artifact paths so ordinary
upstream writes do not weaken Foundry's concurrent-edit contract.

## Trust requirements

- Restored session state cannot select another workspace.
- Artifact references cannot contain unvalidated paths.
- File-access enablement is distinct from private file memory.
- Read-only approval policies do not imply operating-system isolation.
