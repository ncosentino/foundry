---
title: "ADR-0017: Manual private-runtime research automation pilot"
status: "Accepted"
date: "2026-08-11"
authors: ["Nick Cosentino"]
tags: ["architecture", "decision", "automation", "research", "copilot", "pitcrew"]
supersedes: ""
superseded_by: ""
---

## Context and scope

Foundry repeatedly receives issues that identify an external repository, document, or
upstream capability but do not yet contain enough evidence for an implementation or
architecture decision. Existing `research-needed` and `research-completed` labels
record this lifecycle, but completing the research has required an ad hoc interactive
session.

An owner-neutral repository-automation runtime now provides bounded evidence
collection, strict research-decision parsing, marker-owned publication, deterministic
label transitions, and idempotent replay. Its source and package distribution are
still privately incubated. Publishing a public runtime contract before Foundry has
operating evidence would create a premature compatibility and support obligation.

Foundry also has a stronger live-model safeguard than the runtime's generic generated
adapter: automated triggers must never invoke a live LLM. A permitted run must use
Copilot CLI on `foundry-ci`, be manually dispatched by `ncosentino`, and have explicit
authorization for that specific run.

This decision governs a single-issue research pilot. It does not authorize scheduled,
label-triggered, repository-dispatch, parallel, or unattended model execution.

## Decision drivers

- Prove the runtime with a real downstream consumer before defining a public offering.
- Keep the private binary out of the public repository and public runner image.
- Preserve separate read-only analysis and deterministic issue-write boundaries.
- Make the workflow impossible to activate through an automated GitHub event.
- Keep ordinary CI, releases, builds, and contributor workflows independent of the
  private runtime.
- Retain a fast disable and rollback path.

## Decision

Foundry will pilot `RepositoryAutomation.Tool` 0.8.2 through an operator-owned,
read-only PitCrew volume.

The public repository contains only owner-neutral schemas, adapter scripts,
configuration, policy, workflow wiring, documentation, and deterministic contract
tests. The private package is not committed and is not included in the public
`foundry-runner` image.

The `foundry-ci` profile mounts one external volume named
`foundry-repository-automation` at
`/mnt/pitcrew-data/repository-automation`. Profile verification checks:

- the exact package SHA-256;
- the exact full-file integrity manifest;
- the installed command;
- runtime version 0.8.2; and
- contract version 1.

GitHub-hosted runtime initialization fails explicitly. Missing or incompatible
self-hosted capability fails before analysis.

The research workflow:

- declares only `workflow_dispatch`;
- remains inactive unless its activation variable is exactly `manual`;
- requires `ncosentino` as actor and triggering actor;
- requires a false-by-default confirmation, public reason, issue number, and bounded
  AI credit limit;
- runs every job exactly on `foundry-ci`;
- requires the open issue to carry `research-needed`;
- gives Copilot analysis read-only issue and content access without issue-write
  permission;
- gives deterministic apply issue-write access without Copilot permission or
  inference credentials; and
- permits only one marker-owned comment plus the two lifecycle label operations.

The first pilot remains limited to public GitHub evidence and one issue per manually
authorized run. Adding the label is queue state only and never starts a workflow.

Public extraction is reconsidered after supervised downstream evidence exists. At
minimum, the review must include a URL-only issue, a failure that preserves the
trigger state, and an explicitly authorized idempotent rerun. Every live execution
still requires separate permission.

## Alternatives considered

### Publish the runtime before piloting

This would provide immediate transparency and conventional package distribution. It
was rejected for the first pilot because there is not yet downstream evidence that
the current runtime, adapter, and module boundary is the contract Foundry needs.

### Commit the private package to Foundry

This would make hosted initialization simple, but the package would become a public,
opaque binary. It was rejected.

### Add the runtime to the public runner image

This would avoid an external volume but make the private binary retrievable from the
public image and couple every CI worker revision to the pilot. It was rejected.

### Download the package during the workflow

This would require a long-lived credential or private feed dependency inside the
public workflow. It was rejected in favor of operator-provisioned capability.

### Use one job for analysis and apply

This would fit the former single-job live-LLM validator, but the job token would hold
Copilot and issue-write permissions simultaneously. It was rejected; the executable
guardrail is generalized to support permission-separated jobs instead.

## Consequences

### Positive

- Foundry can test the proven research lifecycle without prematurely publishing the
  runtime.
- The private binary remains outside public Git and the public image.
- Automated triggers cannot spend AI credits.
- Model execution never receives issue-write permission.
- Failure retains a retryable labeled issue.
- Removing the activation variable disables the pilot immediately.

### Negative

- Every PitCrew host must provision and maintain the exact external volume.
- Public contributors cannot execute the private runtime locally.
- The pilot depends on a privately maintained package until the extraction review.
- Updating the runtime requires a reviewed package digest, volume update, and profile
  replay.

### Neutral

- Ordinary Foundry CI and releases do not use the private runtime.
- The generic runtime's changing parallel-trigger mechanics are outside this
  single-issue manual pilot.
- Deep source analysis, builds, package inspection, and live probes remain separate
  research activities.

## Confirmation

The decision is confirmed only when:

- deterministic contract and mutation tests pass;
- the profile is rolled out without changing capacity or routing;
- a manually authorized issue run publishes one cited report;
- failure and idempotency behavior match the documented lifecycle; and
- no Actions artifact, cache, Docker operation, automated trigger, code change,
  branch, or pull request is produced by research execution.

## References

- `.github/workflows/repository-automation-research-needed.yml`
- `.github/repository-automation/config-research-needed.json`
- `.github/repository-automation/policies/research-needed.json`
- `.pitcrew/runner-profile.json`
- `scripts/repository-automation/Test-FoundryResearchAutomation.ps1`
- [Research Issue Automation](../research-automation.md)
- [ADR-0009: Repository-owned CI runner image](adr-0009-repository-owned-ci-runner-image.md)
