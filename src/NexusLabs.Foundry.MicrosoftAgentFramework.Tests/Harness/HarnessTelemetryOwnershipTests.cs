using Microsoft.Extensions.AI;

using NexusLabs.Foundry.MicrosoftAgentFramework.Diagnostics;
using NexusLabs.Foundry.MicrosoftAgentFramework.Harness;
using NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Capabilities;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Tests.Harness;

public sealed class HarnessTelemetryOwnershipTests
{
    [Fact]
    public void Validate_HarnessOwnerWithExistingUpstreamTelemetry_RejectsDuplicate()
    {
        using var leaf = new HarnessScriptedChatClient("unused");
        using var instrumented = leaf.AsBuilder().UseOpenTelemetry().Build();

        var result = HarnessCompositionGuard.Validate(
            instrumented,
            HarnessCompositionTestFixture.CreateProfile(
                HarnessToolLoopOwner.Harness,
                HarnessTelemetryOwner.Harness));

        Assert.Equal(
            HarnessCompositionGuardStatus.TelemetryOwnerConflict,
            result.Status);
    }

    [Fact]
    public void Validate_HarnessOwnerWithFoundryTelemetry_RejectsDuplicate()
    {
        using var leaf = new HarnessScriptedChatClient("unused");
        using var metrics = new AgentMetrics();
        using var instrumented =
            HarnessCompositionTestFixture.WithFoundryTelemetry(leaf, metrics);

        var result = HarnessCompositionGuard.Validate(
            instrumented,
            HarnessCompositionTestFixture.CreateProfile(
                HarnessToolLoopOwner.Harness,
                HarnessTelemetryOwner.Harness));

        Assert.Equal(
            HarnessCompositionGuardStatus.TelemetryOwnerConflict,
            result.Status);
    }

    [Fact]
    public void Validate_FoundryOwnerWithUninstrumentedInput_IsValidForComposition()
    {
        using var leaf = new HarnessScriptedChatClient("unused");

        var result = HarnessCompositionGuard.Validate(
            leaf,
            HarnessCompositionTestFixture.CreateProfile(
                HarnessToolLoopOwner.Foundry,
                HarnessTelemetryOwner.Foundry));

        Assert.Equal(HarnessCompositionGuardStatus.Valid, result.Status);
    }

    [Fact]
    public void Validate_FoundryOwnerWithExistingFoundryTelemetry_RejectsDuplicate()
    {
        using var leaf = new HarnessScriptedChatClient("unused");
        using var metrics = new AgentMetrics();
        using var instrumented =
            HarnessCompositionTestFixture.WithFoundryTelemetry(leaf, metrics);

        var result = HarnessCompositionGuard.Validate(
            instrumented,
            HarnessCompositionTestFixture.CreateProfile(
                HarnessToolLoopOwner.Foundry,
                HarnessTelemetryOwner.Foundry));

        Assert.Equal(
            HarnessCompositionGuardStatus.TelemetryOwnerConflict,
            result.Status);
    }

    [Fact]
    public void Validate_MixedToolAndTelemetryOwners_AreRejected()
    {
        using var leaf = new HarnessScriptedChatClient("unused");

        var result = HarnessCompositionGuard.Validate(
            leaf,
            HarnessCompositionTestFixture.CreateProfile(
                HarnessToolLoopOwner.Foundry,
                HarnessTelemetryOwner.Harness));

        Assert.Equal(
            HarnessCompositionGuardStatus.UnsupportedOwnerCombination,
            result.Status);
    }
}
