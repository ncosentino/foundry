namespace NexusLabs.Foundry.Evaluation.Experiments;

/// <summary>
/// Describes paired continuous X-minus-Y comparison evidence and its case-level bootstrap summary.
/// </summary>
public sealed record ExperimentPairedContinuousComparisonEvidence
{
    private const int MinimumBootstrapSampleCount = 4;
    private const int RequiredBootstrapResampleCount = 10000;

    private ExperimentPairedContinuousComparisonEvidence(
        string xLabel,
        string yLabel,
        IReadOnlyList<ExperimentPairedContinuousCaseMeasurement> cases,
        int totalCaseCount,
        int validPairCount,
        int droppedCaseCount,
        int nonComparableCaseCount,
        double? xMean,
        double? xMedian,
        double? yMean,
        double? yMedian,
        double? meanDifference,
        double? medianDifference,
        double? lowerBound,
        double? upperBound,
        double confidenceLevel,
        ulong bootstrapSeed,
        int bootstrapResampleCount,
        bool isInsufficientSample)
    {
        XLabel = xLabel;
        YLabel = yLabel;
        Cases = cases;
        TotalCaseCount = totalCaseCount;
        ValidPairCount = validPairCount;
        DroppedCaseCount = droppedCaseCount;
        NonComparableCaseCount = nonComparableCaseCount;
        XMean = xMean;
        XMedian = xMedian;
        YMean = yMean;
        YMedian = yMedian;
        MeanDifference = meanDifference;
        MedianDifference = medianDifference;
        LowerBound = lowerBound;
        UpperBound = upperBound;
        ConfidenceLevel = confidenceLevel;
        BootstrapSeed = bootstrapSeed;
        BootstrapResampleCount = bootstrapResampleCount;
        IsInsufficientSample = isInsufficientSample;
    }

    /// <summary>Gets the stable label for arm X.</summary>
    public string XLabel { get; }

    /// <summary>Gets the stable label for arm Y.</summary>
    public string YLabel { get; }

    /// <summary>Gets a defensive snapshot of the analyzed paired case measurements.</summary>
    public IReadOnlyList<ExperimentPairedContinuousCaseMeasurement> Cases { get; }

    /// <summary>Gets the total number of supplied case pairs before exclusions.</summary>
    public int TotalCaseCount { get; }

    /// <summary>Gets the number of valid case pairs included in the continuous comparison.</summary>
    public int ValidPairCount { get; }

    /// <summary>
    /// Gets the number of comparable case pairs dropped because they were not full-success or did
    /// not produce finite values for both arms.
    /// </summary>
    public int DroppedCaseCount { get; }

    /// <summary>Gets the number of case pairs excluded because the metric was non-comparable.</summary>
    public int NonComparableCaseCount { get; }

    /// <summary>
    /// Gets the mean of the valid X case-level values, when at least one valid pair remains.
    /// </summary>
    public double? XMean { get; }

    /// <summary>
    /// Gets the median of the valid X case-level values, when at least one valid pair remains.
    /// </summary>
    public double? XMedian { get; }

    /// <summary>
    /// Gets the mean of the valid Y case-level values, when at least one valid pair remains.
    /// </summary>
    public double? YMean { get; }

    /// <summary>
    /// Gets the median of the valid Y case-level values, when at least one valid pair remains.
    /// </summary>
    public double? YMedian { get; }

    /// <summary>
    /// Gets the mean X-minus-Y paired difference, when at least one valid pair remains.
    /// </summary>
    public double? MeanDifference { get; }

    /// <summary>
    /// Gets the median X-minus-Y paired difference, when at least one valid pair remains.
    /// </summary>
    public double? MedianDifference { get; }

    /// <summary>
    /// Gets the lower percentile-bootstrap bound for the mean paired difference when at least four
    /// valid pairs remain; otherwise <see langword="null"/>.
    /// </summary>
    public double? LowerBound { get; }

    /// <summary>
    /// Gets the upper percentile-bootstrap bound for the mean paired difference when at least four
    /// valid pairs remain; otherwise <see langword="null"/>.
    /// </summary>
    public double? UpperBound { get; }

    /// <summary>Gets the two-sided interval confidence level.</summary>
    public double ConfidenceLevel { get; }

    /// <summary>Gets the deterministic seed for the internal bootstrap RNG.</summary>
    public ulong BootstrapSeed { get; }

    /// <summary>Gets the exact number of bootstrap resamples.</summary>
    public int BootstrapResampleCount { get; }

    /// <summary>Gets whether fewer than four valid case pairs remained for the interval.</summary>
    public bool IsInsufficientSample { get; }

    /// <summary>
    /// Creates validated paired continuous X-minus-Y evidence.
    /// </summary>
    /// <param name="xLabel">The stable label for arm X.</param>
    /// <param name="yLabel">The stable label for arm Y.</param>
    /// <param name="cases">The paired continuous case measurements to analyze.</param>
    /// <param name="bootstrapSeed">
    /// The deterministic seed for the internal 10,000-resample case-level bootstrap.
    /// </param>
    /// <param name="confidenceLevel">The two-sided interval confidence level.</param>
    /// <returns>Validated paired continuous comparison evidence.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="cases"/> or one of its elements is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="xLabel"/> or <paramref name="yLabel"/> is blank, both labels are equal, or
    /// the paired case IDs are not unique.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="confidenceLevel"/> is not finite and strictly between zero and one.
    /// </exception>
    public static ExperimentPairedContinuousComparisonEvidence Create(
        string xLabel,
        string yLabel,
        IReadOnlyList<ExperimentPairedContinuousCaseMeasurement> cases,
        ulong bootstrapSeed,
        double confidenceLevel)
    {
        ValidateLabels(xLabel, yLabel);
        ValidateConfidenceLevel(confidenceLevel);
        var snapshot = SnapshotCases(cases);
        var totalCaseCount = snapshot.Count;
        var droppedCaseCount = 0;
        var nonComparableCaseCount = 0;
        var xValues = new List<double>(snapshot.Count);
        var yValues = new List<double>(snapshot.Count);
        var differences = new List<double>(snapshot.Count);
        foreach (var pair in snapshot)
        {
            if (!pair.IsComparable)
            {
                nonComparableCaseCount++;
                continue;
            }

            if (!TryGetValidMeasurementPair(pair, out var xValue, out var yValue))
            {
                droppedCaseCount++;
                continue;
            }

            xValues.Add(xValue);
            yValues.Add(yValue);
            differences.Add(xValue - yValue);
        }

        var validPairCount = differences.Count;
        var xMean = validPairCount > 0 ? CalculateMean(xValues) : (double?)null;
        var xMedian = validPairCount > 0 ? CalculateMedian(xValues) : (double?)null;
        var yMean = validPairCount > 0 ? CalculateMean(yValues) : (double?)null;
        var yMedian = validPairCount > 0 ? CalculateMedian(yValues) : (double?)null;
        var meanDifference = validPairCount > 0 ? CalculateMean(differences) : (double?)null;
        var medianDifference =
            validPairCount > 0
                ? CalculateMedian(differences)
                : (double?)null;
        double? lowerBound = null;
        double? upperBound = null;
        if (validPairCount >= MinimumBootstrapSampleCount)
        {
            (lowerBound, upperBound) = CalculateBootstrapInterval(
                differences,
                bootstrapSeed,
                confidenceLevel);
        }

        return new ExperimentPairedContinuousComparisonEvidence(
            xLabel,
            yLabel,
            snapshot,
            totalCaseCount,
            validPairCount,
            droppedCaseCount,
            nonComparableCaseCount,
            xMean,
            xMedian,
            yMean,
            yMedian,
            meanDifference,
            medianDifference,
            lowerBound,
            upperBound,
            confidenceLevel,
            bootstrapSeed,
            RequiredBootstrapResampleCount,
            validPairCount < MinimumBootstrapSampleCount);
    }

    private static (double LowerBound, double UpperBound) CalculateBootstrapInterval(
        IReadOnlyList<double> differences,
        ulong bootstrapSeed,
        double confidenceLevel)
    {
        var bootstrapMeans = new double[RequiredBootstrapResampleCount];
        var random = new ExperimentDeterministicRandom(bootstrapSeed);
        for (var resampleIndex = 0; resampleIndex < bootstrapMeans.Length; resampleIndex++)
        {
            var total = 0d;
            for (var sampleIndex = 0; sampleIndex < differences.Count; sampleIndex++)
            {
                total += differences[random.NextInt32(differences.Count)];
            }

            bootstrapMeans[resampleIndex] = total / differences.Count;
        }

        Array.Sort(bootstrapMeans);
        var alpha = (1d - confidenceLevel) / 2d;
        return (
            CalculateLinearPercentile(bootstrapMeans, alpha),
            CalculateLinearPercentile(bootstrapMeans, 1d - alpha));
    }

    private static double CalculateLinearPercentile(
        IReadOnlyList<double> sortedValues,
        double probability)
    {
        if (sortedValues.Count == 1)
        {
            return sortedValues[0];
        }

        var position = probability * (sortedValues.Count - 1);
        var lowerIndex = (int)Math.Floor(position);
        var upperIndex = (int)Math.Ceiling(position);
        if (lowerIndex == upperIndex)
        {
            return sortedValues[lowerIndex];
        }

        var weight = position - lowerIndex;
        return sortedValues[lowerIndex]
            + ((sortedValues[upperIndex] - sortedValues[lowerIndex]) * weight);
    }

    private static double CalculateMean(IReadOnlyList<double> values)
    {
        var sum = 0d;
        for (var index = 0; index < values.Count; index++)
        {
            sum += values[index];
        }

        return sum / values.Count;
    }

    private static double CalculateMedian(IReadOnlyList<double> values)
    {
        var snapshot = values.ToArray();
        Array.Sort(snapshot);
        var middleIndex = snapshot.Length / 2;
        return snapshot.Length % 2 == 0
            ? (snapshot[middleIndex - 1] + snapshot[middleIndex]) / 2d
            : snapshot[middleIndex];
    }

    private static IReadOnlyList<ExperimentPairedContinuousCaseMeasurement> SnapshotCases(
        IReadOnlyList<ExperimentPairedContinuousCaseMeasurement> cases)
    {
        ArgumentNullException.ThrowIfNull(cases);
        var seenCaseIds = new HashSet<string>(StringComparer.Ordinal);
        var snapshot = new ExperimentPairedContinuousCaseMeasurement[cases.Count];
        for (var index = 0; index < cases.Count; index++)
        {
            var pair = cases[index];
            ArgumentNullException.ThrowIfNull(pair);
            if (!seenCaseIds.Add(pair.CaseId))
            {
                throw new ArgumentException(
                    $"Paired case ID '{pair.CaseId}' appears more than once.",
                    nameof(cases));
            }

            snapshot[index] = new ExperimentPairedContinuousCaseMeasurement(
                pair.CaseId,
                pair.XValue,
                pair.XStatus,
                pair.YValue,
                pair.YStatus,
                pair.IsComparable);
        }

        return Array.AsReadOnly(snapshot);
    }

    private static bool TryGetValidMeasurementPair(
        ExperimentPairedContinuousCaseMeasurement pair,
        out double xValue,
        out double yValue)
    {
        xValue = default;
        yValue = default;
        if (pair.XStatus != ExperimentItemStatus.Succeeded
            || pair.YStatus != ExperimentItemStatus.Succeeded
            || !pair.XValue.HasValue
            || !pair.YValue.HasValue)
        {
            return false;
        }

        xValue = pair.XValue.Value;
        yValue = pair.YValue.Value;
        return double.IsFinite(xValue) && double.IsFinite(yValue);
    }

    private static void ValidateConfidenceLevel(double confidenceLevel)
    {
        if (!double.IsFinite(confidenceLevel)
            || confidenceLevel <= 0
            || confidenceLevel >= 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(confidenceLevel),
                confidenceLevel,
                "The confidence level must be finite and strictly between zero and one.");
        }
    }

    private static void ValidateLabels(string xLabel, string yLabel)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xLabel);
        ArgumentException.ThrowIfNullOrWhiteSpace(yLabel);
        if (StringComparer.Ordinal.Equals(xLabel, yLabel))
        {
            throw new ArgumentException(
                "The X and Y labels must be distinct.",
                nameof(yLabel));
        }
    }
}
