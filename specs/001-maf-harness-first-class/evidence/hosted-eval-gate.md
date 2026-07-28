# Hosted Evaluation Gate Evidence

**Task:** T118  
**Verified:** 2026-07-28
**Repository:** `ncosentino/foundry`

## Result

The `Harness Evaluation` workflow is not a required status check for the
protected `main` branch. It is advisory, dispatch/schedule-only, and cannot
block or authorize a merge.

Before the dedicated workflow reaches the default branch, the already
registered `CI` workflow exposes a manual `harness-evaluation-dispatch` bridge.
That job is also absent from required branch protection and runs only when its
explicit boolean input is true. When the bridge input is true, the normal
self-hosted build/AOT jobs are skipped so only the PitCrew evaluation
bridge runs.

## Branch-protection evidence

The GitHub branch-protection API reported strict required status checks:

- `build-test-pack`
- `docs`
- `aot`

`Harness Evaluation` and its `advisory-harness-evaluation` job are not in the
required status-check list. The `harness-evaluation-dispatch` bridge is also not
required. The repository rulesets endpoint returned no additional rulesets.

Commands used:

```powershell
gh api repos/ncosentino/foundry/branches/main/protection `
  --jq '.required_status_checks | {contexts, strict}'

gh api repos/ncosentino/foundry/rulesets `
  --jq '.[] | {id,name,target,enforcement,conditions,rules}'
```

Observed required-check payload:

```json
{
  "contexts": [
    "build-test-pack",
    "docs",
    "aot"
  ],
  "strict": true
}
```

## Non-gating workflow controls

`.github/workflows/harness-evaluation.yml`:

- has only `workflow_dispatch` and `schedule` triggers;
- runs only on PitCrew runners labeled
  `self-hosted`, `linux`, `x64`, and `general-purpose`;
- requests only `contents: read` and `copilot-requests: write`;
- uses the official `GitHub.Copilot.SDK` runtime with the workflow-scoped
  `GITHUB_TOKEN` explicitly supplied to the SDK;
- creates a fresh SDK session for every provider request and serializes the
  exact transcript produced by the active Foundry arm, so no hidden SDK session
  history crosses requests;
- exposes only declaration-only Foundry tools and rejects every unexpected SDK
  permission request;
- runs one declaration-only-tool provider probe before scheduling any paired
  batch; the probe verifies tool-call return, external tool-result replay, and
  final grounded text;
- contains no GitHub Models endpoint, permission, or hosted-runner fallback;
- uses a 60-minute job timeout and the pre-registered request, cost, duration,
  output-token, and concurrency caps;
- treats provider authentication and connectivity failures as infrastructure
  errors;
- uploads the immutable artifact bundle and writes the advisory summary under
  `if: always()`; and
- emits `CopilotBillingNotConfirmed` without making a model call when GitHub
  Copilot Enterprise billing is not explicitly affirmed.

The preflight makes no inference request. A live comparison can start only after
the job has been assigned to the exact PitCrew labels, the runner reports
`self-hosted`, and Copilot Enterprise billing has been explicitly affirmed. The
workflow remains advisory and does not become a branch-protection or stochastic
merge gate.
