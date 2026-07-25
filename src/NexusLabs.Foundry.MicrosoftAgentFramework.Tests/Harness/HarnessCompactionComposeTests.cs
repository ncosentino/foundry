using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

using NexusLabs.Foundry.MicrosoftAgentFramework.Context;
using NexusLabs.Foundry.MicrosoftAgentFramework.Harness;
using NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Capabilities;
using NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Context;
using NexusLabs.Foundry.MicrosoftAgentFramework.Workspace;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Tests.Harness;

public sealed class HarnessCompactionComposeTests
{
    [Fact]
    public void Compose_CapabilityNotEnabledAndNoProfile_ReturnsDisabledWithUnchangedChatClient()
    {
        using var services = HarnessCompositionTestFixture.CreateServices();
        var accessor = new AgentExecutionContextAccessor();
        var binding = HarnessCompositionTestFixture.CaptureBinding(accessor, out var scope);
        using (scope)
        {
            var chatClient = new HarnessCompactionObservingChatClient("Tool");
            var request = new HarnessCompactionCompositionRequest(
                chatClient,
                HarnessCompositionTestFixture.CreateProfile(
                    HarnessToolLoopOwner.Harness, HarnessTelemetryOwner.Harness),
                HybridProfile: null,
                binding,
                accessor,
                HarnessCompositionTestFixture.SessionId);

            var result = new HarnessCompactionComposition().Compose(request);

            Assert.Equal(HarnessCompactionCompositionStatus.Disabled, result.Status);
            Assert.Same(chatClient, result.ChatClient);
        }
    }

    [Fact]
    public void Compose_CapabilityEnabledWithoutProfile_FailsClosed()
    {
        using var services = HarnessCompositionTestFixture.CreateServices();
        var accessor = new AgentExecutionContextAccessor();
        var binding = HarnessCompositionTestFixture.CaptureBinding(accessor, out var scope);
        using (scope)
        {
            var chatClient = new HarnessCompactionObservingChatClient("Tool");
            var request = new HarnessCompactionCompositionRequest(
                chatClient,
                HarnessCompactionSeamTestFixture.CreateCompactionEnabledProfile(
                    HarnessToolLoopOwner.Harness, HarnessTelemetryOwner.Harness),
                HybridProfile: null,
                binding,
                accessor,
                HarnessCompositionTestFixture.SessionId);

            var result = new HarnessCompactionComposition().Compose(request);

            Assert.Equal(HarnessCompactionCompositionStatus.CapabilityEnabledWithoutProfile, result.Status);
            Assert.Null(result.ChatClient);
        }
    }

    [Fact]
    public void Compose_ProfileSuppliedWithoutCapabilityEnabled_FailsClosed()
    {
        using var services = HarnessCompositionTestFixture.CreateServices();
        var accessor = new AgentExecutionContextAccessor();
        var binding = HarnessCompositionTestFixture.CaptureBinding(accessor, out var scope);
        using (scope)
        {
            var chatClient = new HarnessCompactionObservingChatClient("Tool");
            var hybridProfile = HarnessCompactionSeamTestFixture.CreateHybridProfile(
                1000, 10, 5, 3, new HarnessConstantSizeContextEstimator(1));
            var request = new HarnessCompactionCompositionRequest(
                chatClient,
                HarnessCompositionTestFixture.CreateProfile(
                    HarnessToolLoopOwner.Harness, HarnessTelemetryOwner.Harness),
                hybridProfile,
                binding,
                accessor,
                HarnessCompositionTestFixture.SessionId);

            var result = new HarnessCompactionComposition().Compose(request);

            Assert.Equal(
                HarnessCompactionCompositionStatus.ProfileSuppliedWithoutCapabilityEnabled, result.Status);
            Assert.Null(result.ChatClient);
        }
    }

    [Fact]
    public void Compose_ProfileRequestedButNotAccepted_ProfileSuppliedFailsClosed()
    {
        using var services = HarnessCompositionTestFixture.CreateServices();
        var accessor = new AgentExecutionContextAccessor();
        var binding = HarnessCompositionTestFixture.CaptureBinding(accessor, out var scope);
        using (scope)
        {
            var chatClient = new HarnessCompactionObservingChatClient("Tool");
            var hybridProfile = HarnessCompactionSeamTestFixture.CreateHybridProfile(
                1000, 10, 5, 3, new HarnessConstantSizeContextEstimator(1));
            var request = new HarnessCompactionCompositionRequest(
                chatClient,
                HarnessCompactionSeamTestFixture.CreateCompactionRequestedButNotAcceptedProfile(
                    HarnessToolLoopOwner.Harness, HarnessTelemetryOwner.Harness),
                hybridProfile,
                binding,
                accessor,
                HarnessCompositionTestFixture.SessionId);

            var result = new HarnessCompactionComposition().Compose(request);

            Assert.Equal(
                HarnessCompactionCompositionStatus.ProfileSuppliedWithoutCapabilityEnabled, result.Status);
            Assert.Null(result.ChatClient);
        }
    }

    [Fact]
    public void Compose_EnabledWithProfile_WrapsExactlyOneHybridCompactionChatClient()
    {
        using var services = HarnessCompositionTestFixture.CreateServices();
        var accessor = new AgentExecutionContextAccessor();
        var binding = HarnessCompositionTestFixture.CaptureBinding(accessor, out var scope);
        using (scope)
        {
            var chatClient = new HarnessCompactionObservingChatClient("Tool");
            var hybridProfile = HarnessCompactionSeamTestFixture.CreateHybridProfile(
                1000, 10, 5, 3, new HarnessConstantSizeContextEstimator(1));
            var request = new HarnessCompactionCompositionRequest(
                chatClient,
                HarnessCompactionSeamTestFixture.CreateCompactionEnabledProfile(
                    HarnessToolLoopOwner.Harness, HarnessTelemetryOwner.Harness),
                hybridProfile,
                binding,
                accessor,
                HarnessCompositionTestFixture.SessionId);

            var result = new HarnessCompactionComposition().Compose(request);

            Assert.Equal(HarnessCompactionCompositionStatus.Success, result.Status);
            Assert.NotNull(result.ChatClient);
            Assert.IsType<HarnessHybridCompactionChatClient>(result.ChatClient);
            Assert.NotSame(chatClient, result.ChatClient);
        }
    }

    [Fact]
    public void Compose_AlreadyWrappedChatClient_RejectsExistingCompactionComponent()
    {
        using var services = HarnessCompositionTestFixture.CreateServices();
        var accessor = new AgentExecutionContextAccessor();
        var binding = HarnessCompositionTestFixture.CaptureBinding(accessor, out var scope);
        using (scope)
        {
            var chatClient = new HarnessCompactionObservingChatClient("Tool");
            var hybridProfile = HarnessCompactionSeamTestFixture.CreateHybridProfile(
                1000, 10, 5, 3, new HarnessConstantSizeContextEstimator(1));
            var enabledProfile = HarnessCompactionSeamTestFixture.CreateCompactionEnabledProfile(
                HarnessToolLoopOwner.Harness, HarnessTelemetryOwner.Harness);

            var firstResult = new HarnessCompactionComposition().Compose(
                new HarnessCompactionCompositionRequest(
                    chatClient, enabledProfile, hybridProfile, binding, accessor,
                    HarnessCompositionTestFixture.SessionId));
            Assert.Equal(HarnessCompactionCompositionStatus.Success, firstResult.Status);

            var secondResult = new HarnessCompactionComposition().Compose(
                new HarnessCompactionCompositionRequest(
                    firstResult.ChatClient!, enabledProfile, hybridProfile, binding, accessor,
                    HarnessCompositionTestFixture.SessionId));

            Assert.Equal(HarnessCompactionCompositionStatus.ExistingCompactionComponent, secondResult.Status);
            Assert.Null(secondResult.ChatClient);
        }
    }

    [Fact]
    public void Compose_ProfileNotExecutable_FailsClosed()
    {
        using var services = HarnessCompositionTestFixture.CreateServices();
        var accessor = new AgentExecutionContextAccessor();
        var binding = HarnessCompositionTestFixture.CaptureBinding(accessor, out var scope);
        using (scope)
        {
            var chatClient = new HarnessCompactionObservingChatClient("Tool");
            var hybridProfile = HarnessCompactionSeamTestFixture.CreateHybridProfile(
                1000, 10, 5, 3, new HarnessConstantSizeContextEstimator(1));
            var enabledProfile = HarnessCompactionSeamTestFixture.CreateCompactionEnabledProfile(
                HarnessToolLoopOwner.Harness, HarnessTelemetryOwner.Harness);
            var notExecutableProfile = enabledProfile with { IsExecutable = false };

            var result = new HarnessCompactionComposition().Compose(
                new HarnessCompactionCompositionRequest(
                    chatClient, notExecutableProfile, hybridProfile, binding, accessor,
                    HarnessCompositionTestFixture.SessionId));

            Assert.Equal(HarnessCompactionCompositionStatus.ProfileNotExecutable, result.Status);
            Assert.Null(result.ChatClient);
        }
    }
}
