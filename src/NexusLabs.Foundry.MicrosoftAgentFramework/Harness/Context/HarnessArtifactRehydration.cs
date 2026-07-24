namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Context;

/// <summary>
/// Explicit rehydration mechanism (T052): takes an explicit <see cref="HarnessArtifactRehydrationRequest"/>,
/// resolves it through <see cref="HarnessArtifactResolver"/>, and — only on a
/// <see cref="HarnessArtifactResolutionStatus.Resolved"/> outcome — returns a marked recoverable
/// context segment. There is no automatic/model-triggered policy here and no compaction: this is
/// exactly the G4 primitive answering "given this exact reference, can I get its body back, and how
/// do I mark that body so it's recoverable/evictable later?" (G5 remains responsible for deciding
/// *when* to rehydrate).
/// </summary>
internal sealed class HarnessArtifactRehydration
{
    private readonly HarnessArtifactResolver _resolver;

    /// <exception cref="ArgumentNullException"><paramref name="resolver"/> is <see langword="null"/>.</exception>
    internal HarnessArtifactRehydration(HarnessArtifactResolver resolver)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        _resolver = resolver;
    }

    /// <summary>
    /// Resolves <paramref name="request"/>'s reference and, only when resolution succeeds, produces a
    /// marked recoverable context segment carrying the exact resolved body. Never injects content
    /// for any other outcome.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">
    /// The trusted execution binding no longer matches the current ambient execution context.
    /// </exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
    internal HarnessArtifactRehydrationResult Rehydrate(
        HarnessArtifactRehydrationRequest request,
        DateTimeOffset rehydratedAtUtc,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var resolution = _resolver.Resolve(
            request.Reference,
            request.MaximumRehydratedUtf8Bytes,
            cancellationToken);

        if (resolution.Status != HarnessArtifactResolutionStatus.Resolved)
        {
            return HarnessArtifactRehydrationResult.NotResolved(resolution);
        }

        var segment = HarnessArtifactRecoverableContextSegment.Create(
            resolution.Reference,
            resolution.Content!,
            rehydratedAtUtc);

        return HarnessArtifactRehydrationResult.Resolved(resolution, segment);
    }
}
