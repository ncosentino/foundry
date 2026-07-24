namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Capabilities;

internal sealed record HarnessCapabilityProfile(
    int SchemaVersion,
    string ProfileId,
    HarnessConstructionLane Lane,
    string MafVersion,
    string MiddlewareOrderVersion,
    HarnessDeliveryPhase EvidenceThroughPhase,
    bool IsExecutable,
    IReadOnlyDictionary<HarnessCapability, HarnessCapabilityEvidence> Capabilities,
    HarnessToolLoopOwner ToolLoopOwner,
    HarnessTelemetryOwner TelemetryOwner,
    HarnessHistoryPersistenceMode HistoryPersistenceMode);
