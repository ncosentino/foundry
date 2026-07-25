using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

using NexusLabs.Foundry.MicrosoftAgentFramework.Context;
using NexusLabs.Foundry.MicrosoftAgentFramework.Harness;
using NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Capabilities;
using NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Context;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Tests.Harness;

/// <summary>
/// Integrated <see cref="HarnessProviderComposition"/> tests proving there is exactly one coherent
/// capability profile/composition request: a single <see cref="HarnessCapabilityProfile"/> paired with
/// an optional <see cref="HarnessHybridProfile"/> on the same <see cref="HarnessProviderCompositionRequest"/>,
/// resolved through exactly one <see cref="HarnessProviderComposition.Compose"/> call. There is never a
/// second, separately-resolved profile and never more than one composition root.
/// </summary>
public sealed class HarnessProviderCompositionCompactionTests
{
    [Fact]
    public async Task Compose_CompactionEnabledProfileWithHybridProfile_SucceedsAndInstallsSingleHybridCompactionSeam()
    {
        var invocationCount = 0;
        var function = AIFunctionFactory.Create(
            () =>
            {
                invocationCount++;
                return "tool-result";
            },
            "G2Tool");
        using var services = HarnessCompositionTestFixture.CreateServices();
        var accessor = new AgentExecutionContextAccessor();
        var binding = HarnessCompositionTestFixture.CaptureBinding(accessor, out var scope);
        using (scope)
        {
            var leaf = new HarnessCompactionObservingChatClient(function.Name);
            var reducer = new HarnessScriptedUpstreamChatReducer((messages, _) => Task.FromResult(messages));

            // triggerMargin deliberately equals hardLimit - 1 so every call is above the trigger
            // threshold and the reducer actually runs on every real-provider-facing round (mirrors
            // HarnessCompactionSeamTests.TwoRoundFicc_ReducerObservedOnEveryProviderRequest).
            var hybridProfile = HarnessCompactionSeamTestFixture.CreateHybridProfile(
                1000, 999, 5, 3, new HarnessConstantSizeContextEstimator(1), reducer);
            var profile = HarnessCompactionSeamTestFixture.CreateCompactionEnabledProfile(
                HarnessToolLoopOwner.Harness, HarnessTelemetryOwner.Harness);

            var request = HarnessCompositionTestFixture.CreateRequest(
                leaf,
                services,
                profile,
                HarnessCompositionTestFixture.CreateToolResolution(function),
                binding,
                accessor,
                hybridProfile);

            var result = new HarnessProviderComposition().Compose(request);

            // The single profile carrying Compaction alongside the ordinary selected capabilities
            // passes the shared composition guard: the capability/profile symmetry check never fails
            // closed when both a HybridProfile and an enabled Compaction capability are present
            // together.
            Assert.Equal(HarnessProviderCompositionStatus.Success, result.Status);
            var agent = Assert.IsAssignableFrom<AIAgent>(result.Agent);

            var response = await agent.RunAsync(
                "run", cancellationToken: TestContext.Current.CancellationToken);

            Assert.Equal("tool-result", response.GetText());
            Assert.Equal(1, invocationCount);

            // The reducer the HybridProfile carries is invoked on every real-provider-facing round
            // (initial call, tool-result call) — proof that exactly one hybrid compaction seam sits at
            // the innermost, real-provider-facing position in the composed pipeline, never merely at
            // the outer agent surface.
            Assert.Equal(2, leaf.CallCount);
            Assert.Equal(2, reducer.InvocationCount);
        }
    }

    [Fact]
    public async Task Compose_AbsentHybridProfileAndDisabledCompactionCapability_PreservesBaselineExactly()
    {
        var function = AIFunctionFactory.Create(() => "tool-result", "G2Tool");
        using var services = HarnessCompositionTestFixture.CreateServices();
        var accessor = new AgentExecutionContextAccessor();
        var binding = HarnessCompositionTestFixture.CaptureBinding(accessor, out var scope);
        using (scope)
        {
            var leaf = new HarnessScriptedChatClient(function.Name);

            // An ordinary profile never requesting Compaction, and no HybridProfile supplied: the
            // Compaction capability always resolves present-but-Disabled (see
            // HarnessCapabilityResolver), the narrow compaction composer returns Disabled with the
            // chat client unchanged, and the rest of the pipeline is built exactly as it was before
            // Compaction existed.
            var profile = HarnessCompositionTestFixture.CreateProfile(
                HarnessToolLoopOwner.Harness, HarnessTelemetryOwner.Harness);

            var request = HarnessCompositionTestFixture.CreateRequest(
                leaf,
                services,
                profile,
                HarnessCompositionTestFixture.CreateToolResolution(function),
                binding,
                accessor,
                hybridProfile: null);

            var result = new HarnessProviderComposition().Compose(request);

            Assert.Equal(HarnessProviderCompositionStatus.Success, result.Status);
            var agent = Assert.IsAssignableFrom<AIAgent>(result.Agent);

            var response = await agent.RunAsync(
                "run", cancellationToken: TestContext.Current.CancellationToken);
            Assert.Equal("tool-result", response.GetText());
            Assert.Equal(2, leaf.CallCount);
        }
    }

    [Fact]
    public void Compose_CompactionCapabilityEnabledWithoutHybridProfile_FailsClosedBeforeAgentConstruction()
    {
        var function = AIFunctionFactory.Create(() => "ok", "G2Tool");
        using var services = HarnessCompositionTestFixture.CreateServices();
        var accessor = new AgentExecutionContextAccessor();
        var binding = HarnessCompositionTestFixture.CaptureBinding(accessor, out var scope);
        using (scope)
        {
            var leaf = new HarnessCompactionObservingChatClient(function.Name);

            // Compaction is requested and enabled on the profile, but no HybridProfile is supplied on
            // the same request: capability/profile symmetry fails closed before any agent is built.
            var profile = HarnessCompactionSeamTestFixture.CreateCompactionEnabledProfile(
                HarnessToolLoopOwner.Harness, HarnessTelemetryOwner.Harness);

            var request = HarnessCompositionTestFixture.CreateRequest(
                leaf,
                services,
                profile,
                HarnessCompositionTestFixture.CreateToolResolution(function),
                binding,
                accessor,
                hybridProfile: null);

            var result = new HarnessProviderComposition().Compose(request);

            Assert.Equal(
                HarnessProviderCompositionStatus.CompactionCapabilityEnabledWithoutProfile, result.Status);
            Assert.Null(result.Agent);
            Assert.Equal(0, leaf.CallCount);
        }
    }

    [Fact]
    public void Compose_HybridProfileSuppliedWithoutCompactionCapabilityEnabled_FailsClosedBeforeAgentConstruction()
    {
        var function = AIFunctionFactory.Create(() => "ok", "G2Tool");
        using var services = HarnessCompositionTestFixture.CreateServices();
        var accessor = new AgentExecutionContextAccessor();
        var binding = HarnessCompositionTestFixture.CaptureBinding(accessor, out var scope);
        using (scope)
        {
            var leaf = new HarnessCompactionObservingChatClient(function.Name);
            var hybridProfile = HarnessCompactionSeamTestFixture.CreateHybridProfile(
                1000, 1, 5, 3, new HarnessConstantSizeContextEstimator(1));

            // A HybridProfile is supplied on the request, but the capability profile never enables
            // Compaction: capability/profile symmetry fails closed before any agent is built.
            var profile = HarnessCompositionTestFixture.CreateProfile(
                HarnessToolLoopOwner.Harness, HarnessTelemetryOwner.Harness);

            var request = HarnessCompositionTestFixture.CreateRequest(
                leaf,
                services,
                profile,
                HarnessCompositionTestFixture.CreateToolResolution(function),
                binding,
                accessor,
                hybridProfile);

            var result = new HarnessProviderComposition().Compose(request);

            Assert.Equal(
                HarnessProviderCompositionStatus.CompactionProfileSuppliedWithoutCapabilityEnabled,
                result.Status);
            Assert.Null(result.Agent);
            Assert.Equal(0, leaf.CallCount);
        }
    }

    [Fact]
    public void Compose_ExistingCompactionComponentOnChatClient_FailsClosed_NoDuplicateCompositionRoot()
    {
        var function = AIFunctionFactory.Create(() => "ok", "G2Tool");
        using var services = HarnessCompositionTestFixture.CreateServices();
        var accessor = new AgentExecutionContextAccessor();
        var binding = HarnessCompositionTestFixture.CaptureBinding(accessor, out var scope);
        using (scope)
        {
            var leaf = new HarnessCompactionObservingChatClient(function.Name);
            var hybridProfile = HarnessCompactionSeamTestFixture.CreateHybridProfile(
                1000, 1, 5, 3, new HarnessConstantSizeContextEstimator(1));
            var profile = HarnessCompactionSeamTestFixture.CreateCompactionEnabledProfile(
                HarnessToolLoopOwner.Harness, HarnessTelemetryOwner.Harness);

            // A chat client that already contains a compaction component — as if a prior composition
            // (or a caller-supplied client) already installed one — supplied directly as
            // request.ChatClient. HarnessProviderComposition must fail closed here instead of ever
            // installing a second, duplicate compaction root beneath the existing one.
            var existingCompactionClient = new HarnessHybridCompactionChatClient(
                leaf, hybridProfile, binding, accessor, HarnessCompositionTestFixture.SessionId, runCoordinator: null);

            var request = HarnessCompositionTestFixture.CreateRequest(
                existingCompactionClient,
                services,
                profile,
                HarnessCompositionTestFixture.CreateToolResolution(function),
                binding,
                accessor,
                hybridProfile);
            var result = new HarnessProviderComposition().Compose(request);

            Assert.Equal(
                HarnessProviderCompositionStatus.CompactionExistingComponent, result.Status);
            Assert.Null(result.Agent);
            Assert.Equal(0, leaf.CallCount);
        }
    }
}
