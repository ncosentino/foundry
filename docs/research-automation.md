---
description: Use Foundry's manually authorized, evidence-backed research issue workflow.
---

# Research Issue Automation

Foundry can publish a bounded applicability report for an open issue carrying the
`research-needed` label. The workflow is a manually authorized maintenance tool, not
automated CI.

Adding the label never invokes a model. The only live-model path is an explicit
`workflow_dispatch` by `ncosentino` after authorization for that specific run. Every
job runs on the `foundry-ci` PitCrew profile, and GitHub Copilot CLI is the only
permitted live-model process.

## Trust boundary

The workflow separates three responsibilities:

1. **Resolve** reads the open issue and the current default-branch revision. It rejects
   pull requests, closed issues, issues without `research-needed`, and unauthorized
   dispatches.
2. **Analyze** receives read-only repository and issue permissions plus the ephemeral
   Copilot request token. Trusted runtime code collects bounded evidence and gives
   Copilot a tool-free snapshot. It receives no issue-write credential.
3. **Apply** receives the validated proposal and issue-write permission, but no
   Copilot permission or inference credential. It revalidates the proposal digest,
   current issue state, configuration, and mutation policy before publication.

The proposal can update one marker-owned comment, add `research-completed`, and remove
`research-needed` in that order. It cannot close or assign issues, create issues,
change code, create branches or pull requests, establish relationships, or dispatch
another workflow.

Normal execution uses no Actions artifact, Actions cache, or Docker.

## Evidence scope

The pilot supports public GitHub URLs only. Repository-root URLs contribute the
linked repository README pinned to a commit. Direct GitHub file URLs contribute that
file at a resolved commit. Current Foundry evidence is limited to `README.md` and
`docs/README.md` when available at the trusted Foundry revision.

The report separates:

- verified facts;
- inferences;
- recommendations classified as `recommend`, `watch`, `drop`, or
  `already-covered`;
- counterevidence; and
- missing evidence.

Every substantive item must cite an evidence identifier. An adoption recommendation
requires both external and current-Foundry evidence; unsupported adoption is
downgraded to `watch`. Zero recommendations is a valid successful result.

This is an applicability assessment. It does not prove runtime behavior, performance,
NativeAOT compatibility, package resolution, or provider behavior. Those claims still
require separately authorized deterministic experiments or live probes.

## Runtime capability

The private `RepositoryAutomation.Tool` 0.8.4 package is not committed to this public
repository and is unavailable on GitHub-hosted runners. Each PitCrew host must provide
the operator-owned Docker volume `foundry-repository-automation`, mounted read-only at:

```text
/mnt/pitcrew-data/repository-automation
```

The volume contains only `RepositoryAutomation.Tool.0.8.4.nupkg`. The profile verifies
package SHA-256
`db11a63a6a697bf8dec02d5e67767f553946cc05b5db366ecb40b4d697197d8b`
before accepting workers.

Each analyze or apply job verifies the package again, copies it into the ephemeral
checkout, installs it into job-local storage, and verifies runtime version 0.8.4,
contract version 1, required module operations, and artifact-free transport
requirements. PitCrew 0.8.2 or later is required for the read-only volume contract.

## Activation

The workflow remains inert unless the repository variable
`REPOSITORY_AUTOMATION_RESEARCH_NEEDED_ENABLED` is exactly `manual`.

Before activation:

1. Provision and verify the read-only volume on every `foundry-ci` host.
2. Apply the reviewed `.pitcrew/runner-profile.json` without changing capacity,
   routing, registration identity, or the public runner image.
3. Confirm both `research-needed` and `research-completed` labels exist.
4. Set the activation variable to `manual`.

Each dispatch requires:

- an open issue number carrying `research-needed`;
- `confirm_live_llm: true`;
- a non-empty public authorization reason; and
- a maximum AI credit limit from 1 through 100.

Agents must not dispatch the workflow without explicit permission for that exact run.

## Publication and failure behavior

Successful analysis upserts one
`<!-- repository-automation.research-needed -->` comment. Rerunning the same issue
updates that comment instead of creating another. Because a successful run removes
the trigger label, an explicitly authorized idempotency rerun must first restore
`research-needed`.

Read, evidence, model, proposal, publication, or label failures retain
`research-needed`. `research-completed` is added only after the report is published,
and the trigger label is removed last.

Repository automation uses one shared repository-and-issue concurrency lane. Future
issue modules therefore serialize with research instead of racing over the same issue.
Runtime 0.8.4 also retries bounded transient GitHub reads and deterministic mutations,
including authoritative recovery for ambiguous issue/comment writes, without rerunning
model analysis.

Unset the activation variable to disable the workflow immediately. Removing the
workflow, configuration, policy, and public support scripts does not require runtime
state cleanup. The operator-owned volume and previously published issue comments have
separate lifecycles.
