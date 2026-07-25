using NexusLabs.Foundry.MicrosoftAgentFramework.Diagnostics;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Context;

/// <summary>
/// Deterministically maps a <see cref="HarnessContextAssemblyResult"/> to the public, privacy-safe
/// <see cref="HarnessContextDiagnostics"/> snapshot carried by the compaction progress events —
/// never by parsing any string evidence. Per-category contributions are computed by summing the same
/// <see cref="IHarnessContextSizeEstimator"/> that governed the originating
/// <see cref="HarnessHybridContextPolicy"/> decision, over <see cref="HarnessContextAssemblyResult.FinalEntries"/>,
/// so the contribution total always agrees with <see cref="HarnessContextAssemblyResult.FinalEstimatedSize"/>.
/// </summary>
internal static class HarnessContextDiagnosticsFactory
{
    /// <exception cref="ArgumentNullException">
    /// <paramref name="result"/> or <paramref name="sizeEstimator"/> is <see langword="null"/>.
    /// </exception>
    internal static HarnessContextDiagnostics Create(
        HarnessContextAssemblyResult result, IHarnessContextSizeEstimator sizeEstimator, int triggerThreshold)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(sizeEstimator);

        var outcome = ToPublicOutcome(result.Outcome);
        var stages = result.Stages.Select(ToPublicStage).ToList();

        if (!result.IsSuccess)
        {
            return HarnessContextDiagnostics.ForTermination(
                outcome,
                sizeEstimator.MeasurementUnit,
                result.OriginalEstimatedSize,
                result.FinalEstimatedSize,
                triggerThreshold,
                result.HardLimit,
                result.AttemptCount,
                stages);
        }

        var categoryContributions = BuildCategoryContributions(result.FinalEntries!, sizeEstimator);

        return HarnessContextDiagnostics.ForSuccess(
            outcome,
            sizeEstimator.MeasurementUnit,
            result.OriginalEstimatedSize,
            result.FinalEstimatedSize,
            triggerThreshold,
            result.HardLimit,
            result.AttemptCount,
            stages,
            categoryContributions);
    }

    private static IReadOnlyList<HarnessContextCategoryContribution> BuildCategoryContributions(
        IReadOnlyList<HarnessContextEntry> finalEntries, IHarnessContextSizeEstimator sizeEstimator)
    {
        var sizeByCategory = new Dictionary<HarnessContextCategory, int>();
        var countByCategory = new Dictionary<HarnessContextCategory, int>();

        foreach (var entry in finalEntries)
        {
            var category = ToPublicCategory(entry.Kind);
            var size = sizeEstimator.EstimateSize(entry);
            if (size < 0)
            {
                throw new InvalidOperationException(
                    $"The configured size estimator returned a negative size ({size}) for a final entry " +
                    $"of category '{category}'. A size estimator must always return a non-negative value.");
            }

            sizeByCategory[category] = checked(sizeByCategory.GetValueOrDefault(category) + size);
            countByCategory[category] = checked(countByCategory.GetValueOrDefault(category) + 1);
        }

        return sizeByCategory
            .Select(pair => HarnessContextCategoryContribution.Create(pair.Key, pair.Value, countByCategory[pair.Key]))
            .OrderBy(contribution => contribution.Category)
            .ToList();
    }

    private static HarnessContextCompactionOutcome ToPublicOutcome(HarnessContextAssemblyOutcome outcome) => outcome switch
    {
        HarnessContextAssemblyOutcome.WithinLimit => HarnessContextCompactionOutcome.WithinLimit,
        HarnessContextAssemblyOutcome.Reduced => HarnessContextCompactionOutcome.Reduced,
        HarnessContextAssemblyOutcome.PreservationFallback => HarnessContextCompactionOutcome.PreservationFallback,
        HarnessContextAssemblyOutcome.Irreducible => HarnessContextCompactionOutcome.Irreducible,
        HarnessContextAssemblyOutcome.ConcurrentMutationLimit => HarnessContextCompactionOutcome.ConcurrentMutationLimit,
        _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "Unrecognized assembly outcome."),
    };

    private static HarnessContextAssemblyStageCategory ToPublicStage(HarnessContextAssemblyStage stage) => stage switch
    {
        HarnessContextAssemblyStage.SnapshotCaptured => HarnessContextAssemblyStageCategory.SnapshotCaptured,
        HarnessContextAssemblyStage.RecoverableBodyEviction => HarnessContextAssemblyStageCategory.RecoverableBodyEviction,
        HarnessContextAssemblyStage.ReducerAttempt => HarnessContextAssemblyStageCategory.ReducerAttempt,
        HarnessContextAssemblyStage.RestartedAfterMutation => HarnessContextAssemblyStageCategory.RestartedAfterMutation,
        HarnessContextAssemblyStage.DeterministicFallback => HarnessContextAssemblyStageCategory.DeterministicFallback,
        _ => throw new ArgumentOutOfRangeException(nameof(stage), stage, "Unrecognized assembly stage."),
    };

    private static HarnessContextCategory ToPublicCategory(HarnessContextEntryKind kind) => kind switch
    {
        HarnessContextEntryKind.SystemInstruction => HarnessContextCategory.SystemInstruction,
        HarnessContextEntryKind.AuthoritativeSessionState => HarnessContextCategory.AuthoritativeSessionState,
        HarnessContextEntryKind.ApprovalSecurityState => HarnessContextCategory.ApprovalSecurityState,
        HarnessContextEntryKind.ToolExchange => HarnessContextCategory.ToolExchange,
        HarnessContextEntryKind.ArtifactReference => HarnessContextCategory.ArtifactReference,
        HarnessContextEntryKind.ConversationalMessage => HarnessContextCategory.ConversationalMessage,
        HarnessContextEntryKind.Summary => HarnessContextCategory.Summary,
        HarnessContextEntryKind.RecoverableContextSegment => HarnessContextCategory.RecoverableContextSegment,
        HarnessContextEntryKind.OptionalContext => HarnessContextCategory.OptionalContext,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unrecognized context entry kind."),
    };
}
