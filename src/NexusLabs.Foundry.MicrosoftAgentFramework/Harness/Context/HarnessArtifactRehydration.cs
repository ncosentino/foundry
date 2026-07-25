using NexusLabs.Foundry.MicrosoftAgentFramework.Diagnostics;
using NexusLabs.Foundry.MicrosoftAgentFramework.Progress;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Context;

/// <summary>
/// Explicit rehydration mechanism: takes an explicit <see cref="HarnessArtifactRehydrationRequest"/>,
/// resolves it through <see cref="HarnessArtifactResolver"/>, and — only on a
/// <see cref="HarnessArtifactResolutionStatus.Resolved"/> outcome — returns a marked recoverable
/// context segment. There is no automatic/model-triggered policy here and no compaction: this
/// mechanism only answers "given this exact reference, can I get its body back, and how do I mark
/// that body so it's recoverable/evictable later?" — a caller elsewhere remains responsible for
/// deciding *when* to rehydrate.
/// </summary>
internal sealed class HarnessArtifactRehydration
{
    private readonly HarnessArtifactResolver _resolver;
    private readonly IProgressReporterAccessor? _progressReporterAccessor;

    /// <exception cref="ArgumentNullException"><paramref name="resolver"/> is <see langword="null"/>.</exception>
    internal HarnessArtifactRehydration(
        HarnessArtifactResolver resolver,
        IProgressReporterAccessor? progressReporterAccessor)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        _resolver = resolver;
        _progressReporterAccessor = progressReporterAccessor;
    }

    /// <summary>
    /// Resolves <paramref name="request"/>'s reference, produces a matching diagnostics snapshot
    /// and, only when resolution succeeds, a marked recoverable context segment carrying the exact
    /// resolved body. Never injects content for any other outcome. Reports exactly one
    /// <see cref="HarnessArtifactRehydrationDecisionEvent"/> for the resulting decision.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">
    /// The trusted execution binding no longer matches the current ambient execution context. No
    /// event is reported in this case, because no decision was made.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> was canceled. No event is reported in this case either.
    /// </exception>
    internal HarnessArtifactRehydrationResult Rehydrate(
        HarnessArtifactRehydrationRequest request,
        DateTimeOffset rehydratedAtUtc,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = Decide(request, rehydratedAtUtc, cancellationToken);
        Report(result, rehydratedAtUtc);
        return result;
    }

    private HarnessArtifactRehydrationResult Decide(
        HarnessArtifactRehydrationRequest request,
        DateTimeOffset rehydratedAtUtc,
        CancellationToken cancellationToken)
    {
        var resolution = _resolver.Resolve(
            request.Reference,
            request.MaximumRehydratedUtf8Bytes,
            cancellationToken);

        var diagnostics = BuildDiagnostics(resolution, request.MaximumRehydratedUtf8Bytes);

        if (resolution.Status != HarnessArtifactResolutionStatus.Resolved)
        {
            return HarnessArtifactRehydrationResult.NotResolved(resolution, diagnostics);
        }

        var segment = HarnessArtifactRecoverableContextSegment.Create(
            resolution.Reference,
            resolution.Content!,
            rehydratedAtUtc);

        return HarnessArtifactRehydrationResult.Resolved(resolution, segment, diagnostics);
    }

    /// <summary>
    /// Deterministically maps every <see cref="HarnessArtifactResolutionStatus"/> to its matching
    /// outcome/reason pair from <paramref name="resolution"/>'s already-structured fields — never by
    /// parsing <see cref="HarnessArtifactResolution.Evidence"/> prose.
    /// </summary>
    private static HarnessArtifactDiagnostics BuildDiagnostics(
        HarnessArtifactResolution resolution,
        int configuredBudgetBytes)
    {
        var (outcome, reason) = resolution.Status switch
        {
            HarnessArtifactResolutionStatus.Resolved =>
                (HarnessArtifactOutcomeCategory.Resolved, HarnessArtifactDecisionReason.DigestVerified),
            HarnessArtifactResolutionStatus.Stale =>
                (HarnessArtifactOutcomeCategory.Stale, HarnessArtifactDecisionReason.DigestMismatch),
            HarnessArtifactResolutionStatus.Missing =>
                (HarnessArtifactOutcomeCategory.Missing, HarnessArtifactDecisionReason.Missing),
            HarnessArtifactResolutionStatus.Unauthorized =>
                (HarnessArtifactOutcomeCategory.Unauthorized, HarnessArtifactDecisionReason.OwnerMismatch),
            HarnessArtifactResolutionStatus.OverBudget =>
                (HarnessArtifactOutcomeCategory.OverBudget, HarnessArtifactDecisionReason.BudgetExceeded),
            _ => throw new ArgumentOutOfRangeException(
                nameof(resolution), resolution.Status, "Unrecognized resolution status."),
        };

        return HarnessArtifactDiagnostics.ForRehydration(
            outcome,
            reason,
            resolution.ObservedContentByteSize,
            configuredBudgetBytes,
            resolution.Reference.ReferenceId);
    }

    private void Report(HarnessArtifactRehydrationResult result, DateTimeOffset rehydratedAtUtc)
    {
        if (_progressReporterAccessor is null)
        {
            return;
        }

        var reporter = _progressReporterAccessor.Current;
        reporter.Report(new HarnessArtifactRehydrationDecisionEvent(
            Timestamp: rehydratedAtUtc,
            WorkflowId: reporter.WorkflowId,
            AgentId: reporter.AgentId,
            ParentAgentId: (reporter as IProgressReporterContext)?.ParentAgentId,
            Depth: reporter.Depth,
            SequenceNumber: reporter.NextSequence(),
            Diagnostics: result.Diagnostics));
    }
}
