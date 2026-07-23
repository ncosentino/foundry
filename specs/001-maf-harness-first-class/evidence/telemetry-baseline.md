# G1 Pre-Uplift Telemetry Baseline

## Baseline identity

- Repository commit: `06b04e6daec39c4a1fb57c3c94e7189fd7803ea0`
- MAF: 1.3.0
- MEAI: 10.5.0
- Canonical core selection: 102 tests passed, zero failed or skipped
- Dedicated telemetry selection: 35 tests passed, zero failed or skipped

```powershell
dotnet test src\NexusLabs.Foundry.MicrosoftAgentFramework.Tests\NexusLabs.Foundry.MicrosoftAgentFramework.Tests.csproj --configuration Release --filter "FullyQualifiedName~MafAgentDiagnosticsDeduplicationTests|FullyQualifiedName~DiagnosticsChatClientMiddlewareGenAiHistogramComposesWithMeaiTests|FullyQualifiedName~ActivitySourceTracingTests|FullyQualifiedName~ChatCompletionActivityModeTests|FullyQualifiedName~DiagnosticsAgentRunMiddlewareStreamingTests|FullyQualifiedName~DiagnosticsFunctionInvokingChatClientTests" --logger "console;verbosity=minimal" --nologo
```

## Foundry-owned sources

| Source | Default name | Instruments or spans |
|---|---|---|
| Agent meter and activity source | `NexusLabs.Foundry.MicrosoftAgentFramework` | `agent.run.started`, `agent.run.completed`, `agent.run.duration`, `agent.tokens.used`, `agent.tool.completed`, `agent.tool.duration`, `agent.chat.duration` |
| Pipeline meter and activity source | `NexusLabs.Foundry.MicrosoftAgentFramework.Pipelines` | `pipeline.run.started`, `pipeline.run.completed`, `pipeline.run.duration`, `pipeline.stage.completed`, `pipeline.stage.duration`, `pipeline.stage.tokens`, `pipeline.stage.tool.failed` |
| Shared GenAI token meter | `Experimental.Microsoft.Extensions.AI` | `gen_ai.client.token.usage` |

The shared GenAI instrument uses MEAI 10.5.0's name, `Histogram<int>` type,
`{token}` unit, description, and explicit bucket boundaries. Its label set
includes token type, operation, request/response model, provider, and server
coordinates where available.

## Deterministic invariants

1. A single non-streaming or streaming model call produces exactly one
   `ChatCompletionDiagnostics` entry.
2. Aggregate token usage equals the sum of unique chat-completion entries.
3. Foundry diagnostics composed above MEAI OpenTelemetry emit exactly one input
   and one output sample, plus one cache-read and one reasoning sample when
   present.
4. The shared GenAI histogram keeps Foundry and MEAI instrument shape identical
   so the OpenTelemetry SDK treats their measurements as one stream.
5. Agent, tool, chat, pipeline, and stage telemetry remain Foundry-owned; the
   candidate Harness composition must not add a second owner for the same event.

## Observed activity and metric behavior

- The default Foundry chat-completion activity mode is `Always`.
- In `Always` mode, one `agent.chat` activity is emitted for one model call even
  when a parent `gen_ai.chat.completions.request` activity exists.
- In `EnrichParent` mode, that same parent suppresses the child `agent.chat`
  activity while retaining diagnostics and enriching the parent.
- One non-streaming or streaming model call produces one captured completion.
- The composed Foundry/MEAI token fixture records exactly four samples for one
  response with input, output, cache-read, and reasoning usage: one per type.
- A registered Foundry activity listener captures exactly one test operation
  with its configured tags; without a listener, no activity is allocated.

## Candidate comparison requirements

- Compare emitted instrument names, types, units, descriptions, bucket
  boundaries, and labels after the MEAI 10.6.0 uplift.
- Compare one-call and multi-tool-round diagnostic counts.
- Verify the MAF Harness OpenTelemetry wrapper is disabled or composed so only
  one effective owner records each agent, model, and tool event.
- Exercise upstream OpenTelemetry enabled and disabled against both Foundry
  `Always` and `EnrichParent` behavior; the baseline default is not itself proof
  of safe Harness composition.
- Account for MEAI 10.6.0's OpenTelemetry GenAI semantic-convention changes,
  including streaming, time-to-first-chunk, and reasoning-token attributes.

T013 records the upstream schema and composition delta. G2 T019 owns the
candidate telemetry-ownership and no-duplication tests once Foundry's Harness
composition seam exists.
