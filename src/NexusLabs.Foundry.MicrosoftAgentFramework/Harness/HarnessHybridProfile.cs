using Microsoft.Extensions.AI;

using NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Context;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness;

/// <summary>
/// Explicit, experimental opt-in for hybrid context compaction. There is no default activation and no
/// optional parameter: every collaborator a compaction pass needs — the bounded preservation policy, the
/// selected upstream reducer, a host-authored structural classification/entry-id strategy, and the
/// session/snapshot integration hook — must be supplied explicitly to <see cref="Create"/>. A caller with
/// no opinion on one of these has no default to fall back to; that caller should not enable compaction at
/// all rather than construct a profile with a guessed collaborator.
/// </summary>
/// <remarks>
/// This profile is inert data — it holds no chat client, installs nothing on its own, and is never
/// consulted unless a caller explicitly supplies it as
/// <see cref="HarnessProviderCompositionRequest.HybridProfile"/> alongside the same
/// <see cref="HarnessProviderCompositionRequest.Profile"/> whose
/// <see cref="Capabilities.HarnessCapability.Compaction"/> capability is itself enabled — one coherent
/// capability profile and composition request, never a second, independently-resolved profile (see
/// <see cref="Context.HarnessCompactionComposition"/>'s fail-closed symmetry contract, invoked internally
/// by <see cref="HarnessProviderComposition"/>). Supplying this profile without the capability enabled, or
/// enabling the capability without supplying this profile, is rejected before any agent construction
/// proceeds.
/// </remarks>
internal sealed class HarnessHybridProfile
{
    private HarnessHybridProfile(
        HarnessHybridContextPolicy policy,
        IChatReducer upstreamReducer,
        IHarnessContextMessageClassifier classifier,
        HarnessContextSnapshotIntegration snapshotIntegration)
    {
        Policy = policy;
        UpstreamReducer = upstreamReducer;
        Classifier = classifier;
        SnapshotIntegration = snapshotIntegration;
    }

    /// <summary>The required, explicit bounded preservation policy governing every compaction pass.</summary>
    internal HarnessHybridContextPolicy Policy { get; }

    /// <summary>
    /// The required, caller-selected upstream <see cref="IChatReducer"/> — bridged into the
    /// <see cref="IHarnessContextReducer"/> abstraction by <see cref="HarnessUpstreamChatReducerAdapter"/>,
    /// never invoked directly against raw messages outside that bridge's structural guarantees.
    /// </summary>
    internal IChatReducer UpstreamReducer { get; }

    /// <summary>
    /// The required host-authored structural classification/entry-id strategy. There is no built-in
    /// default; see <see cref="IHarnessContextMessageClassifier"/>.
    /// </summary>
    internal IHarnessContextMessageClassifier Classifier { get; }

    /// <summary>
    /// The required, explicit session/snapshot integration hook invoked fresh for every provider call;
    /// see <see cref="HarnessContextSnapshotIntegration"/>.
    /// </summary>
    internal HarnessContextSnapshotIntegration SnapshotIntegration { get; }

    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    internal static HarnessHybridProfile Create(
        HarnessHybridContextPolicy policy,
        IChatReducer upstreamReducer,
        IHarnessContextMessageClassifier classifier,
        HarnessContextSnapshotIntegration snapshotIntegration)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(upstreamReducer);
        ArgumentNullException.ThrowIfNull(classifier);
        ArgumentNullException.ThrowIfNull(snapshotIntegration);

        return new HarnessHybridProfile(policy, upstreamReducer, classifier, snapshotIntegration);
    }

    /// <summary>
    /// Builds a profile using the default classification and snapshot strategies
    /// (<see cref="HarnessContentHashContextMessageClassifier"/> and
    /// <see cref="HarnessStaticContextSnapshotProvider"/>) for a caller that integrates neither live
    /// session state nor a custom structural classification. The policy and upstream reducer remain
    /// explicit because they encode the caller's own context budget and reduction choice, which have no
    /// defensible default.
    /// </summary>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    internal static HarnessHybridProfile CreateWithDefaultStrategies(
        HarnessHybridContextPolicy policy,
        IChatReducer upstreamReducer)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(upstreamReducer);

        return new HarnessHybridProfile(
            policy,
            upstreamReducer,
            new HarnessContentHashContextMessageClassifier(),
            baselineEntries => new HarnessStaticContextSnapshotProvider(baselineEntries));
    }
}
