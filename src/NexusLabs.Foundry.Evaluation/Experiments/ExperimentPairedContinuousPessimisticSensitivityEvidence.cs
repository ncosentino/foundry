namespace NexusLabs.Foundry.Evaluation.Experiments;

/// <summary>
/// Describes the protocol-required pessimistic sensitivity adjacent to a conditional paired continuous
/// comparison. Only fully scheduled, comparable cases are analyzed; scheduled failures remain in the
/// analysis through caller-supplied predeclared bound substitutions, while cases truncated by a global
/// scheduling cap are excluded without imputation.
/// </summary>
public sealed record ExperimentPairedContinuousPessimisticSensitivityEvidence
{
    private ExperimentPairedContinuousPessimisticSensitivityEvidence(
        IReadOnlyList<ExperimentPairedContinuousPessimisticCaseMeasurement> cases,
        int incompleteCaseCount,
        int nonComparableCaseCount,
        int xSubstitutionCount,
        int ySubstitutionCount,
        int asymmetricSubstitutionCaseCount,
        ExperimentPairedContinuousComparisonEvidence comparison)
    {
        Cases = cases;
        TotalCaseCount = cases.Count;
        IncompleteCaseCount = incompleteCaseCount;
        NonComparableCaseCount = nonComparableCaseCount;
        XSubstitutionCount = xSubstitutionCount;
        YSubstitutionCount = ySubstitutionCount;
        AsymmetricSubstitutionCaseCount = asymmetricSubstitutionCaseCount;
        Comparison = comparison;
    }

    /// <summary>Gets a defensive snapshot of every supplied sensitivity case.</summary>
    public IReadOnlyList<ExperimentPairedContinuousPessimisticCaseMeasurement> Cases { get; }

    /// <summary>Gets the total number of supplied cases before exclusions.</summary>
    public int TotalCaseCount { get; }

    /// <summary>Gets the number of cases excluded because all planned trial slots were not scheduled.</summary>
    public int IncompleteCaseCount { get; }

    /// <summary>Gets the number of fully scheduled cases excluded because the metric was non-comparable.</summary>
    public int NonComparableCaseCount { get; }

    /// <summary>Gets the number of analyzed cases whose X value used pessimistic substitution.</summary>
    public int XSubstitutionCount { get; }

    /// <summary>Gets the number of analyzed cases whose Y value used pessimistic substitution.</summary>
    public int YSubstitutionCount { get; }

    /// <summary>
    /// Gets the number of analyzed cases where exactly one arm used pessimistic substitution.
    /// </summary>
    public int AsymmetricSubstitutionCaseCount { get; }

    /// <summary>Gets the number of fully scheduled, comparable pairs included in the sensitivity.</summary>
    public int ValidPairCount => Comparison.ValidPairCount;

    /// <summary>
    /// Gets the paired descriptive and bootstrap evidence over the included pessimistic case values.
    /// </summary>
    public ExperimentPairedContinuousComparisonEvidence Comparison { get; }

    /// <summary>
    /// Creates validated pessimistic sensitivity evidence.
    /// </summary>
    /// <param name="xLabel">The stable label for arm X.</param>
    /// <param name="yLabel">The stable label for arm Y.</param>
    /// <param name="cases">The pessimistic case measurements to analyze.</param>
    /// <param name="bootstrapSeed">The deterministic seed for the 10,000-resample bootstrap.</param>
    /// <param name="confidenceLevel">The two-sided interval confidence level.</param>
    /// <returns>Validated pessimistic continuous sensitivity evidence.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="cases"/> or one of its elements is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// A case ID is duplicated, or one of the labels or case measurements is invalid.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="confidenceLevel"/> is not finite and strictly between zero and one.
    /// </exception>
    public static ExperimentPairedContinuousPessimisticSensitivityEvidence Create(
        string xLabel,
        string yLabel,
        IReadOnlyList<ExperimentPairedContinuousPessimisticCaseMeasurement> cases,
        ulong bootstrapSeed,
        double confidenceLevel)
    {
        var snapshot = SnapshotCases(cases);
        var included = new List<ExperimentPairedContinuousCaseMeasurement>(snapshot.Count);
        var incompleteCaseCount = 0;
        var nonComparableCaseCount = 0;
        var xSubstitutionCount = 0;
        var ySubstitutionCount = 0;
        var asymmetricSubstitutionCaseCount = 0;

        foreach (var pair in snapshot)
        {
            if (!pair.IsFullyScheduled)
            {
                incompleteCaseCount++;
                continue;
            }

            if (!pair.IsComparable)
            {
                nonComparableCaseCount++;
                continue;
            }

            included.Add(new ExperimentPairedContinuousCaseMeasurement(
                pair.CaseId,
                pair.XValue,
                ExperimentItemStatus.Succeeded,
                pair.YValue,
                ExperimentItemStatus.Succeeded,
                isComparable: true));

            if (pair.XUsedSubstitution)
            {
                xSubstitutionCount++;
            }

            if (pair.YUsedSubstitution)
            {
                ySubstitutionCount++;
            }

            if (pair.XUsedSubstitution != pair.YUsedSubstitution)
            {
                asymmetricSubstitutionCaseCount++;
            }
        }

        var comparison = ExperimentPairedContinuousComparisonEvidence.Create(
            xLabel,
            yLabel,
            included,
            bootstrapSeed,
            confidenceLevel);

        return new ExperimentPairedContinuousPessimisticSensitivityEvidence(
            snapshot,
            incompleteCaseCount,
            nonComparableCaseCount,
            xSubstitutionCount,
            ySubstitutionCount,
            asymmetricSubstitutionCaseCount,
            comparison);
    }

    private static IReadOnlyList<ExperimentPairedContinuousPessimisticCaseMeasurement> SnapshotCases(
        IReadOnlyList<ExperimentPairedContinuousPessimisticCaseMeasurement> cases)
    {
        ArgumentNullException.ThrowIfNull(cases);
        var seenCaseIds = new HashSet<string>(StringComparer.Ordinal);
        var snapshot = new ExperimentPairedContinuousPessimisticCaseMeasurement[cases.Count];
        for (var index = 0; index < cases.Count; index++)
        {
            var pair = cases[index];
            ArgumentNullException.ThrowIfNull(pair);
            if (!seenCaseIds.Add(pair.CaseId))
            {
                throw new ArgumentException(
                    $"Pessimistic sensitivity case ID '{pair.CaseId}' appears more than once.",
                    nameof(cases));
            }

            snapshot[index] = new ExperimentPairedContinuousPessimisticCaseMeasurement(
                pair.CaseId,
                pair.XValue,
                pair.XUsedSubstitution,
                pair.YValue,
                pair.YUsedSubstitution,
                pair.IsFullyScheduled,
                pair.IsComparable);
        }

        return Array.AsReadOnly(snapshot);
    }
}
