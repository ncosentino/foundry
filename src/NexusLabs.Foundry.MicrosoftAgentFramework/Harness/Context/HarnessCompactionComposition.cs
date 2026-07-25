using NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Capabilities;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Context;

/// <summary>
/// Installs at most one <see cref="HarnessHybridCompactionChatClient"/>, at the proven innermost
/// per-provider-call seam, wrapping a real provider <see cref="Microsoft.Extensions.AI.IChatClient"/>.
/// <see cref="HarnessProviderComposition"/> remains the sole selected-provider composition root: it
/// invokes this narrow composer internally, from within its own <see cref="HarnessProviderComposition.Compose"/>
/// call, against the exact same <see cref="HarnessCapabilityProfile"/>, <see cref="Microsoft.Extensions.AI.IChatClient"/>,
/// execution binding/accessor/session it itself received, and then builds the rest of the pipeline from
/// the chat client this type returns rather than from the caller's original client directly. This type
/// never constructs an <see cref="Microsoft.Agents.AI.AIAgent"/> or touches anything beyond the one chat
/// client it conditionally wraps, and is never a second, independently-invoked composition root.
/// </summary>
/// <remarks>
/// <para>
/// <strong>One coherent profile, fail-closed symmetry.</strong> Reuses the existing
/// <see cref="HarnessCapability.Compaction"/> registration on the same resolved
/// <see cref="HarnessCapabilityProfile"/> <see cref="HarnessProviderComposition"/> itself validates —
/// there is no second, independently-resolved profile anywhere in this flow. Enabling that capability
/// without supplying a <see cref="HarnessHybridProfile"/> is rejected
/// (<see cref="HarnessCompactionCompositionStatus.CapabilityEnabledWithoutProfile"/>); supplying a
/// profile while the capability is not enabled on that same resolved profile is rejected
/// (<see cref="HarnessCompactionCompositionStatus.ProfileSuppliedWithoutCapabilityEnabled"/>). Neither
/// case ever proceeds to construct or return a compaction-wrapped chat client, and both are surfaced by
/// <see cref="HarnessProviderComposition.Compose"/> as its own mapped failure status before any agent
/// construction proceeds.
/// </para>
/// <para>
/// <strong>No-op when absent.</strong> When the capability is not enabled and no profile is supplied,
/// <see cref="Compose"/> returns the caller's chat client completely unchanged
/// (<see cref="HarnessCompactionCompositionStatus.Disabled"/>) — the existing baseline pipeline is
/// preserved exactly, with no compaction node installed and no behavior change whatsoever.
/// </para>
/// <para>
/// <strong>Exactly one compactor, ever.</strong> <see cref="Compose"/> rejects
/// (<see cref="HarnessCompactionCompositionStatus.ExistingCompactionComponent"/>) if the supplied chat
/// client already exposes a <see cref="HarnessHybridCompactionChatClient"/> anywhere in its chain (via
/// <see cref="Microsoft.Extensions.AI.IChatClient.GetService(Type, object?)"/>'s standard
/// self-identify-then-delegate walk), so a caller can never end up with two competing compaction nodes
/// layered on the same request. This composer never installs any MAF default compactor/reducer itself,
/// and installing this node is the only compaction component this codebase ever activates for a given
/// composition — no competing default remains active alongside it.
/// </para>
/// <para>
/// <strong>Capability/profile symmetry is shared, not duplicated.</strong>
/// <see cref="HarnessProviderComposition"/> includes <see cref="HarnessCapability.Compaction"/> in the
/// supported-capability set its own <see cref="HarnessCompositionGuard"/> validates against only when a
/// <see cref="HarnessHybridProfile"/> was actually supplied for that same call, so the single resolved
/// profile a caller builds either carries both the Compaction capability and a
/// <see cref="HarnessHybridProfile"/>, or neither — never a mismatched pair silently tolerated by either
/// composer.
/// </para>
/// </remarks>
internal sealed class HarnessCompactionComposition
{
    /// <exception cref="ArgumentNullException">
    /// <paramref name="request"/>, or one of its required reference-typed members, is
    /// <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException"><see cref="HarnessCompactionCompositionRequest.SessionId"/> is empty or whitespace-only.</exception>
    internal HarnessCompactionCompositionResult Compose(HarnessCompactionCompositionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.ChatClient);
        ArgumentNullException.ThrowIfNull(request.Profile);
        ArgumentNullException.ThrowIfNull(request.ExecutionBinding);
        ArgumentNullException.ThrowIfNull(request.ExecutionContextAccessor);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.SessionId);

        if (!request.Profile.Capabilities.TryGetValue(HarnessCapability.Compaction, out var evidence))
        {
            return new HarnessCompactionCompositionResult(
                HarnessCompactionCompositionStatus.ProfileNotExecutable,
                ChatClient: null,
                Coordinator: null,
                "The supplied capability profile carries no Compaction capability evidence at all.");
        }

        var capabilityEnabled = evidence.EffectiveState == HarnessCapabilityState.Enabled;
        var hybridProfileSupplied = request.HybridProfile is not null;

        if (capabilityEnabled && !hybridProfileSupplied)
        {
            return new HarnessCompactionCompositionResult(
                HarnessCompactionCompositionStatus.CapabilityEnabledWithoutProfile,
                ChatClient: null,
                Coordinator: null,
                "The Compaction capability is enabled on the supplied profile but no " +
                $"{nameof(HarnessHybridProfile)} was supplied. Enabling the experimental capability " +
                "without the required explicit profile must fail closed before agent construction.");
        }

        if (!capabilityEnabled && hybridProfileSupplied)
        {
            return new HarnessCompactionCompositionResult(
                HarnessCompactionCompositionStatus.ProfileSuppliedWithoutCapabilityEnabled,
                ChatClient: null,
                Coordinator: null,
                $"A {nameof(HarnessHybridProfile)} was supplied but the Compaction capability is not " +
                "enabled on the supplied profile. Supplying the profile while the capability/profile " +
                "state is not experimental-enabled must fail closed before agent construction.");
        }

        if (!capabilityEnabled)
        {
            return new HarnessCompactionCompositionResult(
                HarnessCompactionCompositionStatus.Disabled,
                request.ChatClient,
                Coordinator: null,
                Detail: null);
        }

        if (!request.Profile.IsExecutable)
        {
            return new HarnessCompactionCompositionResult(
                HarnessCompactionCompositionStatus.ProfileNotExecutable,
                ChatClient: null,
                Coordinator: null,
                "The supplied capability profile is not executable.");
        }

        if (request.ChatClient.GetService(typeof(HarnessHybridCompactionChatClient)) is not null)
        {
            return new HarnessCompactionCompositionResult(
                HarnessCompactionCompositionStatus.ExistingCompactionComponent,
                ChatClient: null,
                Coordinator: null,
                $"The supplied chat client already contains a {nameof(HarnessHybridCompactionChatClient)}. " +
                "Exactly one hybrid compaction component may ever be installed.");
        }

        // One coordinator instance, shared by this composed chat client and, via
        // HarnessProviderComposition, the outer HarnessGuardedAgent it ends up wrapped beneath — never
        // a second, independently-constructed coordinator for the same composed pipeline.
        var runCoordinator = new HarnessCompactionRunCoordinator();
        var compactionClient = new HarnessHybridCompactionChatClient(
            request.ChatClient,
            request.HybridProfile!,
            request.ExecutionBinding,
            request.ExecutionContextAccessor,
            request.SessionId,
            runCoordinator);

        return new HarnessCompactionCompositionResult(
            HarnessCompactionCompositionStatus.Success,
            compactionClient,
            runCoordinator,
            Detail: null);
    }
}
