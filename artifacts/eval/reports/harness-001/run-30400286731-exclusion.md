# Excluded PitCrew Copilot Run 30400286731

**Workflow run:** [30400286731](https://github.com/ncosentino/foundry/actions/runs/30400286731)

**Source commit:** `c2ff372b8430f791f98f3e2d2e5bbb5703cf026f`

**Disposition:** Excluded; do not pool or use for a retention decision.

## What was valid

- The job ran on PitCrew runner
  `nick-zephyr-repo-c4bddcbda90-ac5cdd9c`.
- Runner labels were `self-hosted`, `linux`, `x64`, and `general-purpose`.
- The workflow used `copilot-requests: write`, `GITHUB_TOKEN`, model
  `gpt-5-mini`, and the GitHub Copilot Enterprise billing path.
- All 143 workflow checksum entries were independently verified with no missing,
  mismatched, or extra files.

## Why the run is excluded

The hosted driver still used the legacy raw `CopilotChatClient`. GitHub Actions
installation tokens are supported by the official Copilot CLI/SDK path, not by
that client's `/copilot_internal/v2/token` exchange. Every attempted arm failed
before model inference with HTTP 404 from the token-exchange endpoint.

The workflow scheduled all 24 batches and recorded 117 operational attempts, but
it produced no model completion suitable for deterministic evaluation. The
reported USD 2.28 value is the driver's conservative request-reservation
arithmetic over failed authentication attempts; it is not evidence of model
inference or an actual Copilot charge.

## Replacement criteria

A replacement run must:

1. use the official `GitHub.Copilot.SDK` runtime;
2. pass a declaration-only-tool provider probe before batch scheduling;
3. retain Foundry as the owner of tool execution;
4. run only on the required PitCrew labels; and
5. produce a checksum-valid complete paired artifact.
