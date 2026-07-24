using Microsoft.Agents.AI;

using NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Capabilities;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness;

internal sealed record HarnessProviderCompositionResult(
    HarnessProviderCompositionStatus Status,
    AIAgent? Agent,
    HarnessCapabilityProfile Profile,
    string? Detail);
