namespace NexusLabs.Foundry.Evaluation.Experiments;

/// <summary>
/// Describes paired binary X-minus-Y comparison evidence and its paired uncertainty summary.
/// </summary>
public sealed record ExperimentPairedBinaryComparisonEvidence
{
    private ExperimentPairedBinaryComparisonEvidence(
        string xLabel,
        string yLabel,
        IReadOnlyList<ExperimentPairedBinaryCaseOutcome> cases,
        int totalCaseCount,
        int validPairCount,
        int excludedCaseCount,
        int nonComparableCaseCount,
        int aCount,
        int bCount,
        int cCount,
        int dCount,
        int discordantCount,
        double? delta,
        double? exactTwoSidedMcNemarProbability,
        double? lowerBound,
        double? upperBound,
        double confidenceLevel,
        ExperimentUnknownSampleTreatment unknownSampleTreatment,
        bool isUnderpowered)
    {
        XLabel = xLabel;
        YLabel = yLabel;
        Cases = cases;
        TotalCaseCount = totalCaseCount;
        ValidPairCount = validPairCount;
        ExcludedCaseCount = excludedCaseCount;
        NonComparableCaseCount = nonComparableCaseCount;
        ACount = aCount;
        BCount = bCount;
        CCount = cCount;
        DCount = dCount;
        DiscordantCount = discordantCount;
        Delta = delta;
        ExactTwoSidedMcNemarProbability = exactTwoSidedMcNemarProbability;
        LowerBound = lowerBound;
        UpperBound = upperBound;
        ConfidenceLevel = confidenceLevel;
        UnknownSampleTreatment = unknownSampleTreatment;
        IsUnderpowered = isUnderpowered;
    }

    /// <summary>Gets the stable label for arm X.</summary>
    public string XLabel { get; }

    /// <summary>Gets the stable label for arm Y.</summary>
    public string YLabel { get; }

    /// <summary>Gets a defensive snapshot of the analyzed paired case outcomes.</summary>
    public IReadOnlyList<ExperimentPairedBinaryCaseOutcome> Cases { get; }

    /// <summary>Gets the total number of supplied case pairs before exclusions.</summary>
    public int TotalCaseCount { get; }

    /// <summary>
    /// Gets the number of comparable case pairs included in the paired X-minus-Y analysis.
    /// </summary>
    public int ValidPairCount { get; }

    /// <summary>
    /// Gets the number of comparable case pairs excluded because the configured treatment was
    /// <see cref="ExperimentUnknownSampleTreatment.Inconclusive"/> and one or both arms were
    /// unscorable.
    /// </summary>
    public int ExcludedCaseCount { get; }

    /// <summary>Gets the number of case pairs excluded because the metric was non-comparable.</summary>
    public int NonComparableCaseCount { get; }

    /// <summary>
    /// Gets the concordant success count where both X and Y succeeded in the paired 2×2 table.
    /// </summary>
    public int ACount { get; }

    /// <summary>
    /// Gets the discordant count where X succeeded and Y failed in the paired 2×2 table.
    /// </summary>
    public int BCount { get; }

    /// <summary>
    /// Gets the discordant count where X failed and Y succeeded in the paired 2×2 table.
    /// </summary>
    public int CCount { get; }

    /// <summary>
    /// Gets the concordant failure count where both X and Y failed in the paired 2×2 table.
    /// </summary>
    public int DCount { get; }

    /// <summary>Gets the total discordant count <c>b + c</c>.</summary>
    public int DiscordantCount { get; }

    /// <summary>
    /// Gets the observed X-minus-Y paired success difference <c>(b - c) / n</c>, when at least one
    /// comparable pair was analyzed.
    /// </summary>
    public double? Delta { get; }

    /// <summary>
    /// Gets the exact two-sided McNemar/binomial discordance probability, when at least one
    /// comparable pair was analyzed.
    /// </summary>
    public double? ExactTwoSidedMcNemarProbability { get; }

    /// <summary>
    /// Gets the lower bound of the paired Newcombe/MOVER-Wilson interval when at least one
    /// comparable pair was analyzed.
    /// </summary>
    public double? LowerBound { get; }

    /// <summary>
    /// Gets the upper bound of the paired Newcombe/MOVER-Wilson interval when at least one
    /// comparable pair was analyzed.
    /// </summary>
    public double? UpperBound { get; }

    /// <summary>Gets the two-sided interval confidence level.</summary>
    public double ConfidenceLevel { get; }

    /// <summary>Gets the configured treatment for comparable but unscorable case pairs.</summary>
    public ExperimentUnknownSampleTreatment UnknownSampleTreatment { get; }

    /// <summary>
    /// Gets whether the discordant sample is underpowered under the protocol's
    /// <c>discordant &lt; 25</c> rule.
    /// </summary>
    public bool IsUnderpowered { get; }

    /// <summary>
    /// Creates validated paired binary X-minus-Y evidence.
    /// </summary>
    /// <param name="xLabel">The stable label for arm X.</param>
    /// <param name="yLabel">The stable label for arm Y.</param>
    /// <param name="cases">The paired case outcomes to analyze.</param>
    /// <param name="unknownSampleTreatment">
    /// The treatment for comparable case pairs whose binary outcome is unscorable for one or both
    /// arms.
    /// </param>
    /// <param name="confidenceLevel">The two-sided interval confidence level.</param>
    /// <returns>Validated paired binary comparison evidence.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="cases"/> or one of its elements is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="xLabel"/> or <paramref name="yLabel"/> is blank, both labels are equal, or
    /// the paired case IDs are not unique.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="confidenceLevel"/> is not finite and strictly between zero and one, or
    /// <paramref name="unknownSampleTreatment"/> is not defined.
    /// </exception>
    public static ExperimentPairedBinaryComparisonEvidence Create(
        string xLabel,
        string yLabel,
        IReadOnlyList<ExperimentPairedBinaryCaseOutcome> cases,
        ExperimentUnknownSampleTreatment unknownSampleTreatment,
        double confidenceLevel)
    {
        ValidateLabels(xLabel, yLabel);
        ValidateConfidenceLevel(confidenceLevel);
        if (!Enum.IsDefined(unknownSampleTreatment))
        {
            throw new ArgumentOutOfRangeException(
                nameof(unknownSampleTreatment),
                unknownSampleTreatment,
                "The unknown sample treatment is not defined.");
        }

        var snapshot = SnapshotCases(cases);
        var totalCaseCount = snapshot.Count;
        var excludedCaseCount = 0;
        var nonComparableCaseCount = 0;
        var aCount = 0;
        var bCount = 0;
        var cCount = 0;
        var dCount = 0;
        foreach (var pair in snapshot)
        {
            if (!pair.IsComparable)
            {
                nonComparableCaseCount++;
                continue;
            }

            var xOutcome = GetComparableOutcome(pair.XStatus, pair.XOutcome);
            var yOutcome = GetComparableOutcome(pair.YStatus, pair.YOutcome);
            if (!xOutcome.HasValue || !yOutcome.HasValue)
            {
                if (unknownSampleTreatment == ExperimentUnknownSampleTreatment.Inconclusive)
                {
                    excludedCaseCount++;
                    continue;
                }

                xOutcome ??= false;
                yOutcome ??= false;
            }

            IncrementCellCounts(xOutcome.Value, yOutcome.Value, ref aCount, ref bCount, ref cCount, ref dCount);
        }

        var validPairCount = checked(aCount + bCount + cCount + dCount);
        var discordantCount = checked(bCount + cCount);
        double? delta = null;
        double? exactProbability = null;
        double? lowerBound = null;
        double? upperBound = null;
        if (validPairCount > 0)
        {
            delta = (double)(bCount - cCount) / validPairCount;
            exactProbability = CalculateExactTwoSidedMcNemarProbability(bCount, cCount);
            (lowerBound, upperBound) = CalculatePairedInterval(
                aCount,
                bCount,
                cCount,
                dCount,
                validPairCount,
                confidenceLevel);
        }

        return new ExperimentPairedBinaryComparisonEvidence(
            xLabel,
            yLabel,
            snapshot,
            totalCaseCount,
            validPairCount,
            excludedCaseCount,
            nonComparableCaseCount,
            aCount,
            bCount,
            cCount,
            dCount,
            discordantCount,
            delta,
            exactProbability,
            lowerBound,
            upperBound,
            confidenceLevel,
            unknownSampleTreatment,
            discordantCount < 25);
    }

    private static (double LowerBound, double UpperBound) CalculatePairedInterval(
        int aCount,
        int bCount,
        int cCount,
        int dCount,
        int validPairCount,
        double confidenceLevel)
    {
        var xSuccessCount = checked(aCount + bCount);
        var ySuccessCount = checked(aCount + cCount);
        var (xEstimate, xLower, xUpper) =
            ExperimentWilsonScoreCalculator.CalculateTwoSided(
                xSuccessCount,
                validPairCount,
                confidenceLevel);
        var (yEstimate, yLower, yUpper) =
            ExperimentWilsonScoreCalculator.CalculateTwoSided(
                ySuccessCount,
                validPairCount,
                confidenceLevel);

        // Use the observed table phi directly; continuity-adjusted positive-correlation variants
        // define a different interval than the paired Wilson/MOVER contract exposed here.
        var correlationDenominator = Math.Sqrt(
            (double)(aCount + bCount)
            * (aCount + cCount)
            * (cCount + dCount)
            * (bCount + dCount));
        var correlation = correlationDenominator == 0
            ? 0
            : (((double)aCount * dCount) - ((double)bCount * cCount)) / correlationDenominator;
        correlation = Math.Clamp(correlation, -1d, 1d);

        var delta = xEstimate - yEstimate;
        var lowerXDistance = xEstimate - xLower;
        var upperYDistance = yUpper - yEstimate;
        var upperXDistance = xUpper - xEstimate;
        var lowerYDistance = yEstimate - yLower;
        var lowerBound = delta - Math.Sqrt(
            Math.Max(
                0,
                Math.Pow(lowerXDistance, 2)
                + Math.Pow(upperYDistance, 2)
                - (2 * correlation * lowerXDistance * upperYDistance)));
        var upperBound = delta + Math.Sqrt(
            Math.Max(
                0,
                Math.Pow(upperXDistance, 2)
                + Math.Pow(lowerYDistance, 2)
                - (2 * correlation * upperXDistance * lowerYDistance)));
        return (
            Math.Clamp(lowerBound, -1d, 1d),
            Math.Clamp(upperBound, -1d, 1d));
    }

    private static double CalculateExactTwoSidedMcNemarProbability(int bCount, int cCount)
    {
        var discordantCount = checked(bCount + cCount);
        if (discordantCount == 0)
        {
            return 1;
        }

        var smallerTailCount = Math.Min(bCount, cCount);
        var term = Math.Pow(0.5d, discordantCount);
        var cumulative = term;
        for (var index = 1; index <= smallerTailCount; index++)
        {
            term *= (discordantCount - (index - 1)) / (double)index;
            cumulative += term;
        }

        return Math.Min(1d, 2d * cumulative);
    }

    private static bool? GetComparableOutcome(
        ExperimentItemStatus status,
        bool? outcome) =>
        status == ExperimentItemStatus.Succeeded
            ? outcome
            : null;

    private static void IncrementCellCounts(
        bool xOutcome,
        bool yOutcome,
        ref int aCount,
        ref int bCount,
        ref int cCount,
        ref int dCount)
    {
        if (xOutcome)
        {
            if (yOutcome)
            {
                aCount++;
            }
            else
            {
                bCount++;
            }
        }
        else if (yOutcome)
        {
            cCount++;
        }
        else
        {
            dCount++;
        }
    }

    private static IReadOnlyList<ExperimentPairedBinaryCaseOutcome> SnapshotCases(
        IReadOnlyList<ExperimentPairedBinaryCaseOutcome> cases)
    {
        ArgumentNullException.ThrowIfNull(cases);
        var seenCaseIds = new HashSet<string>(StringComparer.Ordinal);
        var snapshot = new ExperimentPairedBinaryCaseOutcome[cases.Count];
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

            snapshot[index] = new ExperimentPairedBinaryCaseOutcome(
                pair.CaseId,
                pair.XOutcome,
                pair.XStatus,
                pair.YOutcome,
                pair.YStatus,
                pair.IsComparable);
        }

        return Array.AsReadOnly(snapshot);
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
