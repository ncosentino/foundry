namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Capabilities;

internal sealed record HarnessCapabilityEvidence(
    HarnessCapability Capability,
    string SourcePackage,
    string SourceVersion,
    HarnessCapabilityStability Stability,
    bool DefaultEnabledInBundle,
    bool Requested,
    HarnessCapabilityState EffectiveState,
    HarnessProviderCapability? RequiredProviderCapability,
    HarnessCapabilityTrustBoundary TrustBoundary,
    HarnessCapabilityAotStatus AotStatus,
    HarnessCapabilityDiagnosticsStatus DiagnosticsStatus,
    HarnessDeliveryPhase DeliveryPhase,
    string Rationale);
