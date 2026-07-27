# Hosted Evaluation Gate Evidence

**Task:** T118  
**Verified:** 2026-07-27  
**Repository:** `ncosentino/foundry`

## Result

The `Harness Evaluation` workflow is not a required status check for the
protected `main` branch. It is advisory, dispatch/schedule-only, and cannot
block or authorize a merge.

## Branch-protection evidence

The GitHub branch-protection API reported strict required status checks:

- `build-test-pack`
- `docs`
- `aot`

`Harness Evaluation` and its `advisory-harness-evaluation` job are not in the
required status-check list. The repository rulesets endpoint returned no
additional rulesets.

Commands used:

```powershell
gh api repos/ncosentino/foundry/branches/main/protection `
  --jq '{required_status_checks: .required_status_checks, enforce_admins: .enforce_admins.enabled, required_pull_request_reviews: .required_pull_request_reviews}'

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
- runs on GitHub-hosted `ubuntu-latest`;
- requests only `contents: read` and `models: read`;
- uses a 60-minute job timeout and the pre-registered request, cost, duration,
  output-token, and concurrency caps;
- marks the provider preflight `continue-on-error`;
- uploads the immutable artifact bundle and writes the advisory summary under
  `if: always()`; and
- emits `InvalidInput` rather than a success-shaped comparison when paid GitHub
  Models capacity is not explicitly affirmed.

The workflow therefore supplies hosted evidence without becoming a branch
protection or stochastic merge gate.
