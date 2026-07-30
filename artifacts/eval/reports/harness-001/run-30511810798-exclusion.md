# Excluded Copilot SDK Probe Run 30511810798

**Workflow run:** [30511810798](https://github.com/ncosentino/foundry/actions/runs/30511810798)

**Source commit:** `f187e7cc50c4a3349bf6d635c073f30674444ccd`

**Disposition:** Excluded and not pooled

## Valid controls

- The job ran on the dedicated PitCrew `foundry-ci` profile.
- The preflight confirmed self-hosted execution, GitHub Copilot Enterprise
  billing, the workflow token, frozen pricing, and protocol caps.
- The official Copilot SDK returned the required declaration-only tool call.
- The full paired scheduler did not start.

## Exclusion reason

The second probe turn did not satisfy the original exact final-response
assertion. That probe disclosed the expected final token in the user prompt, so
it could not independently prove that the external tool result crossed the
stateless transcript boundary. Its single combined assertion also did not
distinguish a repeated tool call from a final-text mismatch.

The replacement probe:

1. generates an undisclosed per-run result token;
2. requires the provider to reproduce that external result;
3. rejects repeated tool calls separately; and
4. permits only transport whitespace around the exact result.

This run made only the two probe requests through the authorized GitHub Copilot
Enterprise path. It produced no arm trials and contributes no comparison
evidence.
