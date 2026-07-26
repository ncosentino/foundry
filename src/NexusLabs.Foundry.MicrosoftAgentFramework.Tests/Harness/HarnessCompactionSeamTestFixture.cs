using Microsoft.Extensions.AI;

using NexusLabs.Foundry.MicrosoftAgentFramework.Harness;
using NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Capabilities;
using NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Context;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Tests.Harness;

/// <summary>
/// Deterministic <see cref="HarnessCapabilityProfile"/> and <see cref="HarnessHybridProfile"/> builders
/// for hybrid compaction composition tests, mirroring
/// <see cref="HarnessCompositionTestFixture.CreateProfile"/> but adding the
/// <see cref="HarnessCapability.Compaction"/> capability alongside the baseline capability set.
/// <see cref="CreateCompactionEnabledProfile"/> is the one coherent capability profile a test hands to
/// <see cref="HarnessProviderComposition.Compose"/> alongside a <see cref="HarnessHybridProfile"/> —
/// there is never a second, separately-resolved profile.
/// </summary>
internal static class HarnessCompactionSeamTestFixture
{
    /// <summary>
    /// Builds a fully explicit <see cref="HarnessHybridProfile"/> from the given bounded-preservation
    /// policy inputs, an upstream <see cref="IChatReducer"/> (an echo reducer when omitted), a
    /// content-derived <see cref="HarnessScriptedMessageClassifier"/>, and a
    /// <see cref="HarnessContextSnapshotIntegration"/> that hands each call a fresh
    /// <see cref="HarnessMutableContextSnapshotProvider"/> seeded from that call's own adapted baseline
    /// entries.
    /// </summary>
    internal static HarnessHybridProfile CreateHybridProfile(
        int hardLimit,
        int triggerMargin,
        int recentMessageRetentionCount,
        int maximumCompactionAttempts,
        IHarnessContextSizeEstimator sizeEstimator,
        IChatReducer? upstreamReducer = null) =>
        HarnessHybridProfile.Create(
            HarnessCompactionTestFixture.CreatePolicy(
                hardLimit, triggerMargin, recentMessageRetentionCount, maximumCompactionAttempts, sizeEstimator),
            upstreamReducer ?? HarnessScriptedUpstreamChatReducer.Echo(),
            new HarnessScriptedMessageClassifier(),
            baselineEntries => new HarnessMutableContextSnapshotProvider(baselineEntries));

    /// <summary>
    /// A profile requesting <see cref="HarnessCapability.Compaction"/> with
    /// <see cref="HarnessCapabilityAcceptance.StableAndExperimental"/> acceptance — the Compaction
    /// capability resolves to <see cref="HarnessCapabilityState.Enabled"/> on this profile.
    /// </summary>
    internal static HarnessCapabilityProfile CreateCompactionEnabledProfile(
        HarnessToolLoopOwner toolLoopOwner, HarnessTelemetryOwner telemetryOwner)
    {
        var resolver = new HarnessCapabilityResolver();
        return resolver.Resolve(
            new HarnessCapabilityResolutionRequest(
                ProfileId: "g5-compaction-test",
                Lane: HarnessConstructionLane.SelectedProviders,
                Acceptance: HarnessCapabilityAcceptance.StableAndExperimental,
                EvidenceThroughPhase: HarnessDeliveryPhase.G5,
                RequestedCapabilities: new HashSet<HarnessCapability>
                {
                    HarnessCapability.GeneratedTools,
                    HarnessCapability.FunctionInvocation,
                    HarnessCapability.MessageInjection,
                    HarnessCapability.OpenTelemetry,
                    HarnessCapability.Compaction,
                },
                ProviderCapabilities: new HashSet<HarnessProviderCapability>(),
                ToolLoopOwner: toolLoopOwner,
                TelemetryOwner: telemetryOwner,
                HistoryPersistenceMode: HarnessHistoryPersistenceMode.NotApplicable));
    }

    /// <summary>
    /// A profile requesting <see cref="HarnessCapability.Compaction"/> together with
    /// <see cref="HarnessCapability.PerServiceHistory"/> under the given persistence mode — the one
    /// coherent capability profile a session-integration test hands to
    /// <see cref="HarnessProviderComposition.Compose"/> alongside a <see cref="HarnessHybridProfile"/>
    /// to prove the transient recovered body never reaches the outer, per-service history persistence
    /// boundary while durable references survive it.
    /// </summary>
    internal static HarnessCapabilityProfile CreateCompactionEnabledHistoryProfile(
        HarnessToolLoopOwner toolLoopOwner,
        HarnessTelemetryOwner telemetryOwner,
        HarnessHistoryPersistenceMode historyPersistenceMode)
    {
        var resolver = new HarnessCapabilityResolver();
        return resolver.Resolve(
            new HarnessCapabilityResolutionRequest(
                ProfileId: "g5-compaction-history-test",
                Lane: HarnessConstructionLane.SelectedProviders,
                Acceptance: HarnessCapabilityAcceptance.StableAndExperimental,
                EvidenceThroughPhase: HarnessDeliveryPhase.G5,
                RequestedCapabilities: new HashSet<HarnessCapability>
                {
                    HarnessCapability.GeneratedTools,
                    HarnessCapability.FunctionInvocation,
                    HarnessCapability.MessageInjection,
                    HarnessCapability.OpenTelemetry,
                    HarnessCapability.PerServiceHistory,
                    HarnessCapability.Compaction,
                },
                ProviderCapabilities: new HashSet<HarnessProviderCapability>(),
                ToolLoopOwner: toolLoopOwner,
                TelemetryOwner: telemetryOwner,
                HistoryPersistenceMode: historyPersistenceMode));
    }

    /// <summary>
    /// A profile that requests <see cref="HarnessCapability.Compaction"/> but with only
    /// <see cref="HarnessCapabilityAcceptance.StableOnly"/> acceptance — the capability resolves to
    /// <see cref="HarnessCapabilityState.Disabled"/> because experimental acceptance was withheld,
    /// exercising the "requested but not accepted" fail-closed path distinctly from "never requested".
    /// </summary>
    internal static HarnessCapabilityProfile CreateCompactionRequestedButNotAcceptedProfile(
        HarnessToolLoopOwner toolLoopOwner, HarnessTelemetryOwner telemetryOwner)
    {
        var resolver = new HarnessCapabilityResolver();
        return resolver.Resolve(
            new HarnessCapabilityResolutionRequest(
                ProfileId: "g5-compaction-not-accepted-test",
                Lane: HarnessConstructionLane.SelectedProviders,
                Acceptance: HarnessCapabilityAcceptance.StableOnly,
                EvidenceThroughPhase: HarnessDeliveryPhase.G5,
                RequestedCapabilities: new HashSet<HarnessCapability>
                {
                    HarnessCapability.GeneratedTools,
                    HarnessCapability.FunctionInvocation,
                    HarnessCapability.MessageInjection,
                    HarnessCapability.OpenTelemetry,
                    HarnessCapability.Compaction,
                },
                ProviderCapabilities: new HashSet<HarnessProviderCapability>(),
                ToolLoopOwner: toolLoopOwner,
                TelemetryOwner: telemetryOwner,
                HistoryPersistenceMode: HarnessHistoryPersistenceMode.NotApplicable));
    }
}
