using NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Capabilities;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Tests.Harness;

public sealed class HarnessCapabilityProfileTests
{
    [Fact]
    public void Resolve_SelectedProviders_ReportsVersionedProfileAndOwners()
    {
        var resolver = new HarnessCapabilityResolver();

        var profile = resolver.Resolve(
            CreateRequest(
                HarnessConstructionLane.SelectedProviders,
                HarnessCapabilityAcceptance.StableOnly,
                HarnessDeliveryPhase.G2,
                [HarnessCapability.GeneratedTools, HarnessCapability.FunctionInvocation],
                []));

        Assert.Equal(1, profile.SchemaVersion);
        Assert.Equal("1.15.0", profile.MafVersion);
        Assert.Equal("maf-1.15.0", profile.MiddlewareOrderVersion);
        Assert.Equal(HarnessDeliveryPhase.G2, profile.EvidenceThroughPhase);
        Assert.True(profile.IsExecutable);
        Assert.Equal(HarnessConstructionLane.SelectedProviders, profile.Lane);
        Assert.Equal(HarnessToolLoopOwner.Harness, profile.ToolLoopOwner);
        Assert.Equal(HarnessTelemetryOwner.Foundry, profile.TelemetryOwner);
    }

    [Fact]
    public void Resolve_SelectedStableCapability_EnablesIt()
    {
        var resolver = new HarnessCapabilityResolver();

        var profile = resolver.Resolve(
            CreateRequest(
                HarnessConstructionLane.SelectedProviders,
                HarnessCapabilityAcceptance.StableOnly,
                HarnessDeliveryPhase.G2,
                [HarnessCapability.MessageInjection],
                []));

        var evidence = profile.Capabilities[HarnessCapability.MessageInjection];
        Assert.True(evidence.Requested);
        Assert.Equal(HarnessCapabilityState.Enabled, evidence.EffectiveState);
        Assert.Equal(HarnessCapabilityStability.Stable, evidence.Stability);
    }

    [Fact]
    public void Resolve_UnrequestedBundleDefault_SelectedLaneLeavesItDisabled()
    {
        var resolver = new HarnessCapabilityResolver();

        var profile = resolver.Resolve(
            CreateRequest(
                HarnessConstructionLane.SelectedProviders,
                HarnessCapabilityAcceptance.StableOnly,
                HarnessDeliveryPhase.G2,
                [HarnessCapability.GeneratedTools],
                []));

        var evidence = profile.Capabilities[HarnessCapability.FileMemory];
        Assert.True(evidence.DefaultEnabledInBundle);
        Assert.False(evidence.Requested);
        Assert.Equal(HarnessCapabilityState.Disabled, evidence.EffectiveState);
    }

    [Fact]
    public void Resolve_ExperimentalWithoutAcceptance_LeavesItDisabled()
    {
        var resolver = new HarnessCapabilityResolver();

        var profile = resolver.Resolve(
            CreateRequest(
                HarnessConstructionLane.SelectedProviders,
                HarnessCapabilityAcceptance.StableOnly,
                HarnessDeliveryPhase.G5,
                [HarnessCapability.Compaction],
                []));

        var evidence = profile.Capabilities[HarnessCapability.Compaction];
        Assert.True(evidence.Requested);
        Assert.Equal(HarnessCapabilityState.Disabled, evidence.EffectiveState);
        Assert.Contains("experimental", evidence.Rationale, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Resolve_ExperimentalWithAcceptance_EnablesIt()
    {
        var resolver = new HarnessCapabilityResolver();

        var profile = resolver.Resolve(
            CreateRequest(
                HarnessConstructionLane.SelectedProviders,
                HarnessCapabilityAcceptance.StableAndExperimental,
                HarnessDeliveryPhase.G5,
                [HarnessCapability.Compaction],
                []));

        Assert.Equal(
            HarnessCapabilityState.Enabled,
            profile.Capabilities[HarnessCapability.Compaction].EffectiveState);
    }

    [Fact]
    public void Resolve_ProviderDependentWithoutEvidence_DefersIt()
    {
        var resolver = new HarnessCapabilityResolver();

        var profile = resolver.Resolve(
            CreateRequest(
                HarnessConstructionLane.SelectedProviders,
                HarnessCapabilityAcceptance.StableOnly,
                HarnessDeliveryPhase.G3,
                [HarnessCapability.WebSearch],
                []));

        var evidence = profile.Capabilities[HarnessCapability.WebSearch];
        Assert.Equal(HarnessCapabilityState.Deferred, evidence.EffectiveState);
        Assert.Equal(
            HarnessProviderCapability.HostedWebSearch,
            evidence.RequiredProviderCapability);
    }

    [Fact]
    public void Resolve_ProviderDependentWithEvidence_EnablesIt()
    {
        var resolver = new HarnessCapabilityResolver();

        var profile = resolver.Resolve(
            CreateRequest(
                HarnessConstructionLane.SelectedProviders,
                HarnessCapabilityAcceptance.StableOnly,
                HarnessDeliveryPhase.G3,
                [HarnessCapability.WebSearch],
                [HarnessProviderCapability.HostedWebSearch]));

        Assert.Equal(
            HarnessCapabilityState.Enabled,
            profile.Capabilities[HarnessCapability.WebSearch].EffectiveState);
    }

    [Fact]
    public void Resolve_CompleteBundleBeforeG6_DefersBundleDefaults()
    {
        var resolver = new HarnessCapabilityResolver();

        var profile = resolver.Resolve(
            CreateRequest(
                HarnessConstructionLane.CompleteBundle,
                HarnessCapabilityAcceptance.StableOnly,
                HarnessDeliveryPhase.G2,
                [],
                [HarnessProviderCapability.HostedWebSearch]));

        Assert.Equal(
            HarnessCapabilityState.Deferred,
            profile.Capabilities[HarnessCapability.FileMemory].EffectiveState);
        Assert.Equal(
            HarnessCapabilityState.Deferred,
            profile.Capabilities[HarnessCapability.WebSearch].EffectiveState);
        Assert.Equal(
            HarnessCapabilityState.Disabled,
            profile.Capabilities[HarnessCapability.Compaction].EffectiveState);
        Assert.False(profile.IsExecutable);
    }

    [Fact]
    public void Resolve_CompleteBundleAtG6WithoutProviderEvidence_DefersWebSearch()
    {
        var resolver = new HarnessCapabilityResolver();

        var profile = resolver.Resolve(
            CreateRequest(
                HarnessConstructionLane.CompleteBundle,
                HarnessCapabilityAcceptance.StableOnly,
                HarnessDeliveryPhase.G6,
                [],
                []));

        Assert.Equal(
            HarnessCapabilityState.Enabled,
            profile.Capabilities[HarnessCapability.FileMemory].EffectiveState);
        Assert.Equal(
            HarnessCapabilityState.Deferred,
            profile.Capabilities[HarnessCapability.WebSearch].EffectiveState);
        Assert.False(profile.IsExecutable);
    }

    [Fact]
    public void Resolve_PostG2SelectedCapability_DefersUntilItsDeliveryPhase()
    {
        var resolver = new HarnessCapabilityResolver();

        var profile = resolver.Resolve(
            CreateRequest(
                HarnessConstructionLane.SelectedProviders,
                HarnessCapabilityAcceptance.StableOnly,
                HarnessDeliveryPhase.G2,
                [HarnessCapability.FileMemory],
                []));

        Assert.Equal(
            HarnessCapabilityState.Deferred,
            profile.Capabilities[HarnessCapability.FileMemory].EffectiveState);
        Assert.False(profile.IsExecutable);
    }

    [Fact]
    public void Resolve_SelectedProvidersWithoutCapabilities_IsNotExecutable()
    {
        var resolver = new HarnessCapabilityResolver();

        var profile = resolver.Resolve(
            CreateRequest(
                HarnessConstructionLane.SelectedProviders,
                HarnessCapabilityAcceptance.StableOnly,
                HarnessDeliveryPhase.G2,
                [],
                []));

        Assert.False(profile.IsExecutable);
    }

    private static HarnessCapabilityResolutionRequest CreateRequest(
        HarnessConstructionLane lane,
        HarnessCapabilityAcceptance acceptance,
        HarnessDeliveryPhase evidenceThroughPhase,
        IEnumerable<HarnessCapability> requestedCapabilities,
        IEnumerable<HarnessProviderCapability> providerCapabilities) =>
        new(
            ProfileId: "test-profile",
            Lane: lane,
            Acceptance: acceptance,
            EvidenceThroughPhase: evidenceThroughPhase,
            RequestedCapabilities: new HashSet<HarnessCapability>(requestedCapabilities),
            ProviderCapabilities: new HashSet<HarnessProviderCapability>(providerCapabilities),
            ToolLoopOwner: HarnessToolLoopOwner.Harness,
            TelemetryOwner: HarnessTelemetryOwner.Foundry,
            HistoryPersistenceMode: HarnessHistoryPersistenceMode.NotApplicable);
}
