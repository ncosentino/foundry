# G1 Hosting and Hosting.OpenAI Satellite Compatibility

## Candidates

- `Microsoft.Agents.AI.Hosting`
  `1.15.0-preview.260722.1`
- `Microsoft.Agents.AI.Hosting.OpenAI`
  `1.15.0-alpha.260722.1`

Both remain isolated in
`NexusLabs.Foundry.MicrosoftAgentFramework.DevUI`.

## Source compatibility

The integration and example compile against the existing APIs:

- `AddOpenAIResponses()`
- `AddOpenAIConversations()`
- `MapOpenAIResponses()`
- `MapOpenAIConversations()`

The integration package and `DevUIApp` build successfully, and PR #71's hosted
pack validation passes.

## Risk

Hosting.OpenAI remains alpha and MAF 1.15 includes breaking changes in its
Responses protocol helpers and optional execution state. Foundry's existing
example compiles because it uses the stable registration and endpoint surface,
but G1 does not claim wire-protocol runtime parity.

## Disposition

Compile and package compatibility: **passed**.

Hosted OpenAI protocol/runtime smoke: **deferred** to the optional satellite
track. Failure of that alpha runtime path does not block the stable core Harness
graph.
