namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Capabilities;

internal sealed record HarnessCapabilityResolutionRequest(
    string ProfileId,
    HarnessConstructionLane Lane,
    HarnessCapabilityAcceptance Acceptance,
    HarnessDeliveryPhase EvidenceThroughPhase,
    IReadOnlySet<HarnessCapability> RequestedCapabilities,
    IReadOnlySet<HarnessProviderCapability> ProviderCapabilities,
    HarnessToolLoopOwner ToolLoopOwner,
    HarnessTelemetryOwner TelemetryOwner,
    HarnessHistoryPersistenceMode HistoryPersistenceMode);
