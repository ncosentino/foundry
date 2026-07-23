# Contract: Harness Capability Profile

## Purpose

Describe the effective Harness-related behavior for one Foundry agent without
committing to a final public API.

## Inputs

- Selected chat client
- Generated Foundry tools
- Requested capabilities
- Stability acceptance
- Provider capability evidence
- Workspace and session configuration
- Diagnostics and telemetry ownership
- Trusted execution binding

## Required outputs

- Effective enabled capability set
- Source package and version per capability
- Stable, experimental, provider-dependent, unsupported, or deferred status
- Default-on behavior inherited from the complete bundle
- Explicit overrides and limitations
- One selected tool-invocation loop
- One telemetry ownership decision
- AOT support status
- Trust-boundary warnings

## Invariants

- Experimental capabilities are not enabled implicitly.
- A provider-dependent tool is not registered without provider capability
  evidence.
- The selected-provider lane does not require the complete Harness bundle.
- The complete bundle does not affect ordinary Foundry MAF consumers.
- Effective defaults are inspectable before a run.
- Invalid or conflicting package graphs fail before agent execution.

