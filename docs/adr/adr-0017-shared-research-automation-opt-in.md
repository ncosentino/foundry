---
title: "ADR-0017: Shared research automation opt-in"
status: "Accepted"
date: "2026-08-15"
authors: ["Nick Cosentino"]
tags: ["architecture", "decision", "automation", "research", "copilot", "pitcrew"]
supersedes: ""
superseded_by: ""
---

## Context and scope

Foundry repeatedly receives issues that identify an external repository, document, or
upstream capability but do not yet contain enough evidence for an implementation or
architecture decision. The `research-needed` and `research-completed` labels record
that lifecycle, but completing the research has required an ad hoc interactive
session.

A shared repository-automation capability provides bounded evidence collection,
isolated Copilot analysis, closed proposal validation, deterministic issue mutation,
transport recovery, and idempotent replay. Foundry needs that behavior, but it should
not become an independent owner of the generic runtime, schemas, adapter helpers, or
research methodology.

Foundry also has a stricter live-model safeguard than the generic adapter: automated
triggers must never invoke a live LLM. A permitted run must use Copilot CLI on
`foundry-ci`, be manually dispatched by `ncosentino`, and have explicit authorization
for that specific run.

This decision governs Foundry's opt-in policy and temporary repository-local adapter.
It does not assign permanent ownership of repository automation to Foundry or
authorize scheduled, label-triggered, repository-dispatch, or unattended model
execution.

## Decision drivers

- Keep generic automation contracts and behavior under one shared owner.
- Keep Foundry's public repository limited to repository-specific intent and policy.
- Preserve trusted-revision, permission separation, bounded proposals, and
  idempotent mutation.
- Make automated AI-credit consumption structurally impossible.
- Keep the repository integration replaceable by a central producer without changing
  Foundry's module configuration or persisted issue identity.
- Retain fast disable and rollback paths.

## Decision

Foundry will opt into shared `research-needed` module version 1 through immutable
repository-automation distribution 0.8.4.

Foundry owns:

- the research issue form;
- the manual activation decision;
- lifecycle labels;
- bounded module configuration and mutation policy;
- the `foundry-ci` capability selection;
- local documentation; and
- a thin GitHub adapter while GitHub requires repository-local workflow YAML.

The shared distribution owns:

- contract schemas and conformance fixtures;
- runtime and Copilot initialization;
- work-request construction;
- proposal integrity and policy validation;
- deterministic mutation handlers;
- transport retries;
- module implementation and research methodology; and
- compatibility across runtime upgrades.

Foundry does not commit copies of those shared files. The `foundry-ci` profile mounts
one operator-owned, read-only distribution at:

```text
/mnt/pitcrew-data/repository-automation/v0.8.4
```

The profile verifies shared conformance, and analysis and apply verify the immutable
file manifest again. The consumer contract validates Foundry's configuration and
policy against the shared schemas; the runtime revalidates them before model
invocation or mutation.

The repository-local adapter declares only:

- `workflow_dispatch`;
- explicit authorization inputs and actor restrictions;
- one repository-and-issue concurrency lane;
- the `foundry-ci` runner capability;
- trusted default-branch and issue resolution;
- sparse checkout of Foundry configuration and policy;
- read-only analysis permissions;
- narrow issue-write apply permissions; and
- stable shared-distribution invocations.

Adding `research-needed` is queue state only. It never starts a model. Every live run
requires separate permission.

The durable target is an optional centrally operated GitHub App or equivalent
producer. It may receive installed-repository events and schedules, keep a bounded
dispatch and retry ledger, and emit the same versioned work request through a
repository-scoped installation token. Foundry continues to own policy, secrets,
authoritative state, execution, and mutation. Replacing the local trigger adapter must
not change module identity, configuration, markers, or idempotency.

## Alternatives considered

### Copy the shared runtime and adapter support into Foundry

This makes the repository self-contained, but every consumer would independently
maintain schemas, bootstrap scripts, security boundaries, and upgrades. It was
rejected.

### Commit only the private runtime package

This removes source duplication but publishes an opaque private-incubation package
from a public repository and still leaves Foundry owning adapter helpers. It was
rejected.

### Put the runtime in the public runner image

This couples every CI worker revision to an optional maintenance capability and makes
the private package retrievable from the public image. It was rejected.

### Defer all repository integration until a central producer exists

This has the cleanest eventual ownership boundary, but prevents a supervised
single-repository pilot. A thin adapter over the shared distribution provides
operating evidence while preserving a direct migration path, so complete deferral was
not selected.

### Combine analysis and apply in one job

This would reduce workflow YAML but give one token both inference and issue-write
authority. It was rejected.

## Consequences

### Positive

- Foundry carries only repository-specific policy and the unavoidable GitHub adapter.
- Shared fixes and runtime upgrades have one implementation owner.
- Model execution never receives issue-write permission.
- Automated triggers cannot spend AI credits.
- Transient GitHub failures can recover without repeating paid analysis.
- A central producer can replace local triggering without rewriting persisted state.

### Negative

- Every `foundry-ci` host must provision the exact shared distribution.
- Public contributors cannot execute the private-incubation distribution locally.
- The temporary workflow still repeats GitHub's permission-separated job topology.
- Distribution upgrades require a reviewed manifest, host rollout, and adapter pin.

### Neutral

- Ordinary Foundry builds and releases do not invoke repository automation.
- Deep source analysis, builds, package inspection, and live probes remain separately
  authorized research activities.

## Confirmation

The decision is confirmed only when:

- shared conformance validates the mounted distribution;
- Foundry configuration and policy validate against shared schemas;
- the profile rolls out without changing capacity or routing;
- a manually authorized issue run publishes one cited report;
- failure and idempotency behavior match the documented lifecycle; and
- research execution creates no artifact, cache, code change, branch, pull request,
  assignment, relationship, or downstream dispatch.

## References

- `.github/workflows/repository-automation-research-needed.yml`
- `.github/repository-automation/config-research-needed.json`
- `.github/repository-automation/policies/research-needed.json`
- `.pitcrew/runner-profile.json`
- `scripts/test-research-automation-policy.ps1`
- [Research Issue Automation](../research-automation.md)
- [ADR-0009: Repository-owned CI runner image](adr-0009-repository-owned-ci-runner-image.md)
