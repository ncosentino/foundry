# Excluded Hosted Run 30270567078

Workflow run [30270567078](https://github.com/ncosentino/foundry/actions/runs/30270567078)
completed against commit `e8b1301f3c98b3116e106386dcdcba7e17a03a90`,
but it is excluded from all comparative evidence and is not pooled with
`run-30273935931`.

## Reason

The first driver version:

- did not globally pace GitHub Models requests;
- produced multiple HTTP 429 failures; and
- did not classify the provider SDK's 429 exception forms as retryable.

Those behaviors violated the pre-registered transient retry semantics. The run
therefore serves only as implementation-defect evidence.

The replacement run used:

- a frozen 4,000 ms global request interval;
- typed and wrapped 429/500/502/503/504 transient detection;
- one retry per arm/case/trial; and
- worst-case paced-time reservation before scheduling each batch.

Run `30273935931` had zero 429 terminal failures and is the sole authoritative
hosted comparison bundle.

