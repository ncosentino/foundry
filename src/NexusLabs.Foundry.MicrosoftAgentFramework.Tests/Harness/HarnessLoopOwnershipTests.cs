using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

using NexusLabs.Foundry.MicrosoftAgentFramework.Diagnostics;
using NexusLabs.Foundry.MicrosoftAgentFramework.Harness;
using NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Capabilities;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Tests.Harness;

public sealed class HarnessLoopOwnershipTests
{
    [Fact]
    public void Validate_ExistingFunctionInvokingClient_RejectsSecondLoop()
    {
        using var leaf = new HarnessScriptedChatClient("unused");
        using var existingLoop = leaf
            .AsBuilder()
            .UseFunctionInvocation(NullLoggerFactory.Instance)
            .Build();
        var profile = HarnessCompositionTestFixture.CreateProfile(
            HarnessToolLoopOwner.Harness,
            HarnessTelemetryOwner.Harness);

        var result = HarnessCompositionGuard.Validate(existingLoop, profile);

        Assert.Equal(
            HarnessCompositionGuardStatus.ExistingFunctionInvocationLoop,
            result.Status);
    }

    [Fact]
    public void Validate_NonExecutableProfile_FailsClosed()
    {
        using var chatClient = new HarnessScriptedChatClient("unused");
        var resolver = new HarnessCapabilityResolver();
        var profile = resolver.Resolve(
            new HarnessCapabilityResolutionRequest(
                "deferred",
                HarnessConstructionLane.SelectedProviders,
                HarnessCapabilityAcceptance.StableOnly,
                HarnessDeliveryPhase.G2,
                new HashSet<HarnessCapability> { HarnessCapability.FileMemory },
                new HashSet<HarnessProviderCapability>(),
                HarnessToolLoopOwner.Harness,
                HarnessTelemetryOwner.Harness));

        var result = HarnessCompositionGuard.Validate(chatClient, profile);

        Assert.Equal(
            HarnessCompositionGuardStatus.ProfileNotExecutable,
            result.Status);
    }

    [Fact]
    public void Validate_ExistingDiagnosticsFunctionInvokingClient_RejectsSecondLoop()
    {
        using var leaf = new HarnessScriptedChatClient("unused");
        using var existingLoop = new DiagnosticsFunctionInvokingChatClient(leaf);
        var profile = HarnessCompositionTestFixture.CreateProfile(
            HarnessToolLoopOwner.Harness,
            HarnessTelemetryOwner.Harness);

        var result = HarnessCompositionGuard.Validate(existingLoop, profile);

        Assert.Equal(
            HarnessCompositionGuardStatus.ExistingFunctionInvocationLoop,
            result.Status);
    }

    [Fact]
    public void Validate_ExistingMessageInjection_RejectsSecondInjector()
    {
        using var leaf = new HarnessScriptedChatClient("unused");
        using var existingInjection = leaf
            .AsBuilder()
            .UseMessageInjection()
            .Build();
        var profile = HarnessCompositionTestFixture.CreateProfile(
            HarnessToolLoopOwner.Harness,
            HarnessTelemetryOwner.Harness);

        var result = HarnessCompositionGuard.Validate(existingInjection, profile);

        Assert.Equal(
            HarnessCompositionGuardStatus.ExistingMessageInjection,
            result.Status);
    }

    [Fact]
    public void Validate_CompleteBundleLane_IsRejectedByG2Composition()
    {
        using var chatClient = new HarnessScriptedChatClient("unused");
        var resolver = new HarnessCapabilityResolver();
        var profile = resolver.Resolve(
            new HarnessCapabilityResolutionRequest(
                "bundle",
                HarnessConstructionLane.CompleteBundle,
                HarnessCapabilityAcceptance.StableOnly,
                HarnessDeliveryPhase.G6,
                new HashSet<HarnessCapability>(),
                new HashSet<HarnessProviderCapability>
                {
                    HarnessProviderCapability.HostedWebSearch,
                },
                HarnessToolLoopOwner.Harness,
                HarnessTelemetryOwner.Harness));

        var result = HarnessCompositionGuard.Validate(chatClient, profile);

        Assert.Equal(
            HarnessCompositionGuardStatus.UnsupportedConstructionLane,
            result.Status);
    }

    [Fact]
    public void Validate_LaterPhaseCapability_IsRejectedByG2Composition()
    {
        using var chatClient = new HarnessScriptedChatClient("unused");
        var resolver = new HarnessCapabilityResolver();
        var profile = resolver.Resolve(
            new HarnessCapabilityResolutionRequest(
                "g3",
                HarnessConstructionLane.SelectedProviders,
                HarnessCapabilityAcceptance.StableOnly,
                HarnessDeliveryPhase.G3,
                new HashSet<HarnessCapability>
                {
                    HarnessCapability.GeneratedTools,
                    HarnessCapability.FunctionInvocation,
                    HarnessCapability.MessageInjection,
                    HarnessCapability.OpenTelemetry,
                    HarnessCapability.PerServiceHistory,
                },
                new HashSet<HarnessProviderCapability>(),
                HarnessToolLoopOwner.Harness,
                HarnessTelemetryOwner.Harness));

        var result = HarnessCompositionGuard.Validate(chatClient, profile);

        Assert.Equal(
            HarnessCompositionGuardStatus.CapabilityOutsideCompositionPhase,
            result.Status);
    }
}
