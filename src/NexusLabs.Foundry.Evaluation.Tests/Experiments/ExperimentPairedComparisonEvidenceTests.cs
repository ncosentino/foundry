using NexusLabs.Foundry.Evaluation.Experiments;

namespace NexusLabs.Foundry.Evaluation.Tests.Experiments;

public sealed class ExperimentPairedComparisonEvidenceTests
{
    private const double ConfidenceLevel = 0.95;

    [Fact]
    public void BinaryEvidence_Create_ComputesKnownMcNemarVector()
    {
        var evidence = ExperimentPairedComparisonEvidence.CreateBinary(
            "X",
            "Y",
            [
                new ExperimentPairedBinaryCaseOutcome(
                    "case-1",
                    xOutcome: true,
                    xStatus: ExperimentItemStatus.Succeeded,
                    yOutcome: true,
                    yStatus: ExperimentItemStatus.Succeeded,
                    isComparable: true),
                new ExperimentPairedBinaryCaseOutcome(
                    "case-2",
                    xOutcome: true,
                    xStatus: ExperimentItemStatus.Succeeded,
                    yOutcome: false,
                    yStatus: ExperimentItemStatus.Succeeded,
                    isComparable: true),
                new ExperimentPairedBinaryCaseOutcome(
                    "case-3",
                    xOutcome: true,
                    xStatus: ExperimentItemStatus.Succeeded,
                    yOutcome: false,
                    yStatus: ExperimentItemStatus.Succeeded,
                    isComparable: true),
                new ExperimentPairedBinaryCaseOutcome(
                    "case-4",
                    xOutcome: true,
                    xStatus: ExperimentItemStatus.Succeeded,
                    yOutcome: false,
                    yStatus: ExperimentItemStatus.Succeeded,
                    isComparable: true),
                new ExperimentPairedBinaryCaseOutcome(
                    "case-5",
                    xOutcome: true,
                    xStatus: ExperimentItemStatus.Succeeded,
                    yOutcome: false,
                    yStatus: ExperimentItemStatus.Succeeded,
                    isComparable: true),
                new ExperimentPairedBinaryCaseOutcome(
                    "case-6",
                    xOutcome: true,
                    xStatus: ExperimentItemStatus.Succeeded,
                    yOutcome: false,
                    yStatus: ExperimentItemStatus.Succeeded,
                    isComparable: true),
                new ExperimentPairedBinaryCaseOutcome(
                    "case-7",
                    xOutcome: false,
                    xStatus: ExperimentItemStatus.Succeeded,
                    yOutcome: false,
                    yStatus: ExperimentItemStatus.Succeeded,
                    isComparable: true),
                new ExperimentPairedBinaryCaseOutcome(
                    "case-8",
                    xOutcome: false,
                    xStatus: ExperimentItemStatus.Succeeded,
                    yOutcome: false,
                    yStatus: ExperimentItemStatus.Succeeded,
                    isComparable: true),
            ],
            ExperimentUnknownSampleTreatment.CountAsFailure,
            ConfidenceLevel);

        Assert.Equal("X", evidence.XLabel);
        Assert.Equal("Y", evidence.YLabel);
        Assert.Equal(ConfidenceLevel, evidence.ConfidenceLevel);
        Assert.Equal(8, evidence.TotalCaseCount);
        Assert.Equal(8, evidence.ValidPairCount);
        Assert.Equal(0, evidence.ExcludedCaseCount);
        Assert.Equal(0, evidence.NonComparableCaseCount);
        Assert.Equal(1, evidence.ACount);
        Assert.Equal(5, evidence.BCount);
        Assert.Equal(0, evidence.CCount);
        Assert.Equal(2, evidence.DCount);
        Assert.Equal(5, evidence.DiscordantCount);
        Assert.Equal(0.625, evidence.Delta!.Value);
        Assert.Equal(0.0625, evidence.ExactTwoSidedMcNemarProbability!.Value);
        Assert.Equal(0.1698454250318649, evidence.LowerBound!.Value, precision: 12);
        Assert.Equal(0.8631557142872454, evidence.UpperBound!.Value, precision: 12);
        Assert.True(evidence.IsUnderpowered);
    }

    [Fact]
    public void BinaryEvidence_NoDiscordance_ReportsZeroDeltaWithoutEquivalence()
    {
        var evidence = ExperimentPairedComparisonEvidence.CreateBinary(
            "X",
            "Y",
            [
                new ExperimentPairedBinaryCaseOutcome(
                    "case-1",
                    xOutcome: true,
                    xStatus: ExperimentItemStatus.Succeeded,
                    yOutcome: true,
                    yStatus: ExperimentItemStatus.Succeeded,
                    isComparable: true),
                new ExperimentPairedBinaryCaseOutcome(
                    "case-2",
                    xOutcome: true,
                    xStatus: ExperimentItemStatus.Succeeded,
                    yOutcome: true,
                    yStatus: ExperimentItemStatus.Succeeded,
                    isComparable: true),
                new ExperimentPairedBinaryCaseOutcome(
                    "case-3",
                    xOutcome: true,
                    xStatus: ExperimentItemStatus.Succeeded,
                    yOutcome: true,
                    yStatus: ExperimentItemStatus.Succeeded,
                    isComparable: true),
                new ExperimentPairedBinaryCaseOutcome(
                    "case-4",
                    xOutcome: true,
                    xStatus: ExperimentItemStatus.Succeeded,
                    yOutcome: true,
                    yStatus: ExperimentItemStatus.Succeeded,
                    isComparable: true),
                new ExperimentPairedBinaryCaseOutcome(
                    "case-5",
                    xOutcome: false,
                    xStatus: ExperimentItemStatus.Succeeded,
                    yOutcome: false,
                    yStatus: ExperimentItemStatus.Succeeded,
                    isComparable: true),
                new ExperimentPairedBinaryCaseOutcome(
                    "case-6",
                    xOutcome: false,
                    xStatus: ExperimentItemStatus.Succeeded,
                    yOutcome: false,
                    yStatus: ExperimentItemStatus.Succeeded,
                    isComparable: true),
                new ExperimentPairedBinaryCaseOutcome(
                    "case-7",
                    xOutcome: false,
                    xStatus: ExperimentItemStatus.Succeeded,
                    yOutcome: false,
                    yStatus: ExperimentItemStatus.Succeeded,
                    isComparable: true),
                new ExperimentPairedBinaryCaseOutcome(
                    "case-8",
                    xOutcome: false,
                    xStatus: ExperimentItemStatus.Succeeded,
                    yOutcome: false,
                    yStatus: ExperimentItemStatus.Succeeded,
                    isComparable: true),
            ],
            ExperimentUnknownSampleTreatment.CountAsFailure,
            ConfidenceLevel);

        Assert.Equal(8, evidence.ValidPairCount);
        Assert.Equal(4, evidence.ACount);
        Assert.Equal(0, evidence.BCount);
        Assert.Equal(0, evidence.CCount);
        Assert.Equal(4, evidence.DCount);
        Assert.Equal(0, evidence.DiscordantCount);
        Assert.Equal(0, evidence.Delta!.Value);
        Assert.Equal(1, evidence.ExactTwoSidedMcNemarProbability!.Value);
        Assert.Equal(-0.3244075652372696, evidence.LowerBound!.Value, precision: 12);
        Assert.Equal(0.3244075652372696, evidence.UpperBound!.Value, precision: 12);
        Assert.True(evidence.IsUnderpowered);
    }

    [Fact]
    public void BinaryEvidence_PerfectGain_ReportsUnitDelta()
    {
        var evidence = ExperimentPairedComparisonEvidence.CreateBinary(
            "X",
            "Y",
            [
                new ExperimentPairedBinaryCaseOutcome(
                    "case-1",
                    xOutcome: true,
                    xStatus: ExperimentItemStatus.Succeeded,
                    yOutcome: false,
                    yStatus: ExperimentItemStatus.Succeeded,
                    isComparable: true),
                new ExperimentPairedBinaryCaseOutcome(
                    "case-2",
                    xOutcome: true,
                    xStatus: ExperimentItemStatus.Succeeded,
                    yOutcome: false,
                    yStatus: ExperimentItemStatus.Succeeded,
                    isComparable: true),
                new ExperimentPairedBinaryCaseOutcome(
                    "case-3",
                    xOutcome: true,
                    xStatus: ExperimentItemStatus.Succeeded,
                    yOutcome: false,
                    yStatus: ExperimentItemStatus.Succeeded,
                    isComparable: true),
                new ExperimentPairedBinaryCaseOutcome(
                    "case-4",
                    xOutcome: true,
                    xStatus: ExperimentItemStatus.Succeeded,
                    yOutcome: false,
                    yStatus: ExperimentItemStatus.Succeeded,
                    isComparable: true),
            ],
            ExperimentUnknownSampleTreatment.CountAsFailure,
            ConfidenceLevel);

        Assert.Equal(4, evidence.ValidPairCount);
        Assert.Equal(0, evidence.ACount);
        Assert.Equal(4, evidence.BCount);
        Assert.Equal(0, evidence.CCount);
        Assert.Equal(0, evidence.DCount);
        Assert.Equal(1, evidence.Delta!.Value);
        Assert.Equal(0.125, evidence.ExactTwoSidedMcNemarProbability!.Value);
        Assert.Equal(0.3071897344337656, evidence.LowerBound!.Value, precision: 12);
        Assert.Equal(1, evidence.UpperBound!.Value);
    }

    [Fact]
    public void BinaryEvidence_EmptyInput_ProducesNullStatistics()
    {
        var evidence = ExperimentPairedComparisonEvidence.CreateBinary(
            "X",
            "Y",
            [],
            ExperimentUnknownSampleTreatment.Inconclusive,
            ConfidenceLevel);

        Assert.Empty(evidence.Cases);
        Assert.Equal(0, evidence.TotalCaseCount);
        Assert.Equal(0, evidence.ValidPairCount);
        Assert.Equal(0, evidence.ExcludedCaseCount);
        Assert.Equal(0, evidence.NonComparableCaseCount);
        Assert.Equal(0, evidence.ACount);
        Assert.Equal(0, evidence.BCount);
        Assert.Equal(0, evidence.CCount);
        Assert.Equal(0, evidence.DCount);
        Assert.Equal(0, evidence.DiscordantCount);
        Assert.Null(evidence.Delta);
        Assert.Null(evidence.ExactTwoSidedMcNemarProbability);
        Assert.Null(evidence.LowerBound);
        Assert.Null(evidence.UpperBound);
        Assert.True(evidence.IsUnderpowered);
    }

    [Fact]
    public void BinaryEvidence_InconclusiveTreatment_ExcludesComparableUnscorableCases()
    {
        var cases = new[]
        {
            new ExperimentPairedBinaryCaseOutcome(
                "case-1",
                xOutcome: true,
                xStatus: ExperimentItemStatus.Succeeded,
                yOutcome: true,
                yStatus: ExperimentItemStatus.Succeeded,
                isComparable: true),
            new ExperimentPairedBinaryCaseOutcome(
                "case-2",
                xOutcome: true,
                xStatus: ExperimentItemStatus.Succeeded,
                yOutcome: false,
                yStatus: ExperimentItemStatus.Succeeded,
                isComparable: true),
            new ExperimentPairedBinaryCaseOutcome(
                "case-3",
                xOutcome: null,
                xStatus: ExperimentItemStatus.ExecutionFailed,
                yOutcome: true,
                yStatus: ExperimentItemStatus.Succeeded,
                isComparable: true),
            new ExperimentPairedBinaryCaseOutcome(
                "case-4",
                xOutcome: false,
                xStatus: ExperimentItemStatus.Succeeded,
                yOutcome: false,
                yStatus: ExperimentItemStatus.Succeeded,
                isComparable: true),
        };

        var inconclusive = ExperimentPairedComparisonEvidence.CreateBinary(
            "X",
            "Y",
            cases,
            ExperimentUnknownSampleTreatment.Inconclusive,
            ConfidenceLevel);
        var pessimistic = ExperimentPairedComparisonEvidence.CreateBinary(
            "X",
            "Y",
            cases,
            ExperimentUnknownSampleTreatment.CountAsFailure,
            ConfidenceLevel);

        Assert.Equal(4, inconclusive.TotalCaseCount);
        Assert.Equal(3, inconclusive.ValidPairCount);
        Assert.Equal(1, inconclusive.ExcludedCaseCount);
        Assert.Equal(0, inconclusive.NonComparableCaseCount);
        Assert.Equal(1, inconclusive.ACount);
        Assert.Equal(1, inconclusive.BCount);
        Assert.Equal(0, inconclusive.CCount);
        Assert.Equal(1, inconclusive.DCount);
        Assert.Equal(1d / 3d, inconclusive.Delta!.Value);

        Assert.Equal(4, pessimistic.ValidPairCount);
        Assert.Equal(0, pessimistic.ExcludedCaseCount);
        Assert.Equal(1, pessimistic.ACount);
        Assert.Equal(1, pessimistic.BCount);
        Assert.Equal(1, pessimistic.CCount);
        Assert.Equal(1, pessimistic.DCount);
        Assert.Equal(0, pessimistic.Delta!.Value);
    }

    [Fact]
    public void BinaryEvidence_NonComparableCases_AreExcludedFromPairedCounts()
    {
        var evidence = ExperimentPairedComparisonEvidence.CreateBinary(
            "X",
            "Y",
            [
                new ExperimentPairedBinaryCaseOutcome(
                    "case-1",
                    xOutcome: true,
                    xStatus: ExperimentItemStatus.Succeeded,
                    yOutcome: true,
                    yStatus: ExperimentItemStatus.Succeeded,
                    isComparable: true),
                new ExperimentPairedBinaryCaseOutcome(
                    "case-2",
                    xOutcome: true,
                    xStatus: ExperimentItemStatus.Succeeded,
                    yOutcome: false,
                    yStatus: ExperimentItemStatus.Succeeded,
                    isComparable: false),
                new ExperimentPairedBinaryCaseOutcome(
                    "case-3",
                    xOutcome: false,
                    xStatus: ExperimentItemStatus.Succeeded,
                    yOutcome: true,
                    yStatus: ExperimentItemStatus.Succeeded,
                    isComparable: true),
            ],
            ExperimentUnknownSampleTreatment.CountAsFailure,
            ConfidenceLevel);

        Assert.Equal(3, evidence.TotalCaseCount);
        Assert.Equal(2, evidence.ValidPairCount);
        Assert.Equal(0, evidence.ExcludedCaseCount);
        Assert.Equal(1, evidence.NonComparableCaseCount);
        Assert.Equal(1, evidence.ACount);
        Assert.Equal(0, evidence.BCount);
        Assert.Equal(1, evidence.CCount);
        Assert.Equal(0, evidence.DCount);
    }

    [Fact]
    public void BinaryEvidence_ValidatesAndSnapshotsInputs()
    {
        var originalCases = new List<ExperimentPairedBinaryCaseOutcome>
        {
            new(
                "case-1",
                xOutcome: true,
                xStatus: ExperimentItemStatus.Succeeded,
                yOutcome: true,
                yStatus: ExperimentItemStatus.Succeeded,
                isComparable: true),
        };

        var evidence = ExperimentPairedComparisonEvidence.CreateBinary(
            "X",
            "Y",
            originalCases,
            ExperimentUnknownSampleTreatment.Inconclusive,
            ConfidenceLevel);
        originalCases.Clear();

        Assert.Single(evidence.Cases);

        Assert.Throws<ArgumentException>(() => new ExperimentPairedBinaryCaseOutcome(
            "case-2",
            xOutcome: null,
            xStatus: ExperimentItemStatus.Succeeded,
            yOutcome: true,
            yStatus: ExperimentItemStatus.Succeeded,
            isComparable: true));
        Assert.Throws<ArgumentException>(() => new ExperimentPairedBinaryCaseOutcome(
            "case-2",
            xOutcome: true,
            xStatus: ExperimentItemStatus.ExecutionFailed,
            yOutcome: true,
            yStatus: ExperimentItemStatus.Succeeded,
            isComparable: true));
        Assert.Throws<ArgumentException>(() => ExperimentPairedComparisonEvidence.CreateBinary(
            "X",
            "X",
            evidence.Cases,
            ExperimentUnknownSampleTreatment.Inconclusive,
            ConfidenceLevel));
        Assert.Throws<ArgumentException>(() => ExperimentPairedComparisonEvidence.CreateBinary(
            "X",
            "Y",
            [
                evidence.Cases[0],
                new ExperimentPairedBinaryCaseOutcome(
                    "case-1",
                    xOutcome: false,
                    xStatus: ExperimentItemStatus.Succeeded,
                    yOutcome: false,
                    yStatus: ExperimentItemStatus.Succeeded,
                    isComparable: true),
            ],
            ExperimentUnknownSampleTreatment.Inconclusive,
            ConfidenceLevel));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ExperimentPairedComparisonEvidence.CreateBinary(
                "X",
                "Y",
                evidence.Cases,
                (ExperimentUnknownSampleTreatment)999,
                ConfidenceLevel));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ExperimentPairedComparisonEvidence.CreateBinary(
                "X",
                "Y",
                evidence.Cases,
                ExperimentUnknownSampleTreatment.Inconclusive,
                confidenceLevel: 1));
    }

    [Fact]
    public void ContinuousEvidence_Create_ComputesDeterministicBootstrapAndStructuralStats()
    {
        var cases =
        [
            new ExperimentPairedContinuousCaseMeasurement(
                "case-1",
                xValue: 15.0,
                xStatus: ExperimentItemStatus.Succeeded,
                yValue: 20.0,
                yStatus: ExperimentItemStatus.Succeeded,
                isComparable: true),
            new ExperimentPairedContinuousCaseMeasurement(
                "case-2",
                xValue: 15.9,
                xStatus: ExperimentItemStatus.Succeeded,
                yValue: 20.0,
                yStatus: ExperimentItemStatus.Succeeded,
                isComparable: true),
            new ExperimentPairedContinuousCaseMeasurement(
                "case-3",
                xValue: 17.8,
                xStatus: ExperimentItemStatus.Succeeded,
                yValue: 20.0,
                yStatus: ExperimentItemStatus.Succeeded,
                isComparable: true),
            new ExperimentPairedContinuousCaseMeasurement(
                "case-4",
                xValue: 19.7,
                xStatus: ExperimentItemStatus.Succeeded,
                yValue: 20.0,
                yStatus: ExperimentItemStatus.Succeeded,
                isComparable: true),
            new ExperimentPairedContinuousCaseMeasurement(
                "case-5",
                xValue: 21.7,
                xStatus: ExperimentItemStatus.Succeeded,
                yValue: 20.0,
                yStatus: ExperimentItemStatus.Succeeded,
                isComparable: true),
            new ExperimentPairedContinuousCaseMeasurement(
                "case-6",
                xValue: 23.4,
                xStatus: ExperimentItemStatus.Succeeded,
                yValue: 20.0,
                yStatus: ExperimentItemStatus.Succeeded,
                isComparable: true),
            new ExperimentPairedContinuousCaseMeasurement(
                "case-7",
                xValue: 26.6,
                xStatus: ExperimentItemStatus.Succeeded,
                yValue: 20.0,
                yStatus: ExperimentItemStatus.Succeeded,
                isComparable: true),
            new ExperimentPairedContinuousCaseMeasurement(
                "case-8",
                xValue: 31.8,
                xStatus: ExperimentItemStatus.Succeeded,
                yValue: 20.0,
                yStatus: ExperimentItemStatus.Succeeded,
                isComparable: true),
        ];

        var seed123 = ExperimentPairedComparisonEvidence.CreateContinuous(
            "X",
            "Y",
            cases,
            bootstrapSeed: 123,
            ConfidenceLevel);
        var seed123Repeat = ExperimentPairedComparisonEvidence.CreateContinuous(
            "X",
            "Y",
            cases,
            bootstrapSeed: 123,
            ConfidenceLevel);
        var seed124 = ExperimentPairedComparisonEvidence.CreateContinuous(
            "X",
            "Y",
            cases,
            bootstrapSeed: 124,
            ConfidenceLevel);

        Assert.Equal("X", seed123.XLabel);
        Assert.Equal("Y", seed123.YLabel);
        Assert.Equal(ConfidenceLevel, seed123.ConfidenceLevel);
        Assert.Equal((ulong)123, seed123.BootstrapSeed);
        Assert.Equal(10000, seed123.BootstrapResampleCount);
        Assert.Equal(8, seed123.TotalCaseCount);
        Assert.Equal(8, seed123.ValidPairCount);
        Assert.Equal(0, seed123.DroppedCaseCount);
        Assert.Equal(0, seed123.NonComparableCaseCount);
        Assert.False(seed123.IsInsufficientSample);
        Assert.Equal(21.4875, seed123.XMean!.Value);
        Assert.Equal(20.7, seed123.XMedian!.Value);
        Assert.Equal(20.0, seed123.YMean!.Value);
        Assert.Equal(20.0, seed123.YMedian!.Value);
        Assert.Equal(1.4875, seed123.MeanDifference!.Value);
        Assert.Equal(0.7, seed123.MedianDifference!.Value, precision: 12);
        Assert.Equal(-1.9250000000000003, seed123.LowerBound!.Value, precision: 12);
        Assert.Equal(5.412812499999996, seed123.UpperBound!.Value, precision: 12);
        Assert.Equal(seed123.LowerBound, seed123Repeat.LowerBound);
        Assert.Equal(seed123.UpperBound, seed123Repeat.UpperBound);
        Assert.Equal(-1.9625000000000001, seed124.LowerBound!.Value, precision: 12);
        Assert.Equal(5.375312499999995, seed124.UpperBound!.Value, precision: 12);
        Assert.True(
            seed123.LowerBound != seed124.LowerBound
            || seed123.UpperBound != seed124.UpperBound,
            "Different bootstrap seeds should produce a different deterministic resample trajectory.");
    }

    [Fact]
    public void ContinuousEvidence_DroppedAndNonComparableCounts_TrackPairEligibility()
    {
        var evidence = ExperimentPairedComparisonEvidence.CreateContinuous(
            "X",
            "Y",
            [
                new ExperimentPairedContinuousCaseMeasurement(
                    "case-1",
                    xValue: 11.0,
                    xStatus: ExperimentItemStatus.Succeeded,
                    yValue: 10.0,
                    yStatus: ExperimentItemStatus.Succeeded,
                    isComparable: true),
                new ExperimentPairedContinuousCaseMeasurement(
                    "case-2",
                    xValue: null,
                    xStatus: ExperimentItemStatus.ExecutionFailed,
                    yValue: 10.0,
                    yStatus: ExperimentItemStatus.Succeeded,
                    isComparable: true),
                new ExperimentPairedContinuousCaseMeasurement(
                    "case-3",
                    xValue: 12.0,
                    xStatus: ExperimentItemStatus.Succeeded,
                    yValue: double.NaN,
                    yStatus: ExperimentItemStatus.Succeeded,
                    isComparable: true),
                new ExperimentPairedContinuousCaseMeasurement(
                    "case-4",
                    xValue: 15.0,
                    xStatus: ExperimentItemStatus.Succeeded,
                    yValue: 10.0,
                    yStatus: ExperimentItemStatus.Succeeded,
                    isComparable: false),
                new ExperimentPairedContinuousCaseMeasurement(
                    "case-5",
                    xValue: 16.0,
                    xStatus: ExperimentItemStatus.Succeeded,
                    yValue: 12.0,
                    yStatus: ExperimentItemStatus.Succeeded,
                    isComparable: true),
            ],
            bootstrapSeed: 123,
            ConfidenceLevel);

        Assert.Equal(5, evidence.TotalCaseCount);
        Assert.Equal(2, evidence.ValidPairCount);
        Assert.Equal(2, evidence.DroppedCaseCount);
        Assert.Equal(1, evidence.NonComparableCaseCount);
        Assert.Equal(13.5, evidence.XMean!.Value);
        Assert.Equal(13.5, evidence.XMedian!.Value);
        Assert.Equal(11.0, evidence.YMean!.Value);
        Assert.Equal(11.0, evidence.YMedian!.Value);
        Assert.Equal(2.5, evidence.MeanDifference!.Value);
        Assert.Equal(2.5, evidence.MedianDifference!.Value);
        Assert.True(evidence.IsInsufficientSample);
        Assert.Null(evidence.LowerBound);
        Assert.Null(evidence.UpperBound);
    }

    [Fact]
    public void ContinuousEvidence_InsufficientSample_KeepsDescriptiveStatsAndNullBounds()
    {
        var evidence = ExperimentPairedComparisonEvidence.CreateContinuous(
            "X",
            "Y",
            [
                new ExperimentPairedContinuousCaseMeasurement(
                    "case-1",
                    xValue: 10.0,
                    xStatus: ExperimentItemStatus.Succeeded,
                    yValue: 8.0,
                    yStatus: ExperimentItemStatus.Succeeded,
                    isComparable: true),
                new ExperimentPairedContinuousCaseMeasurement(
                    "case-2",
                    xValue: 14.0,
                    xStatus: ExperimentItemStatus.Succeeded,
                    yValue: 13.0,
                    yStatus: ExperimentItemStatus.Succeeded,
                    isComparable: true),
                new ExperimentPairedContinuousCaseMeasurement(
                    "case-3",
                    xValue: 21.0,
                    xStatus: ExperimentItemStatus.Succeeded,
                    yValue: 16.0,
                    yStatus: ExperimentItemStatus.Succeeded,
                    isComparable: true),
            ],
            bootstrapSeed: 123,
            ConfidenceLevel);

        Assert.Equal(3, evidence.ValidPairCount);
        Assert.Equal(15.0, evidence.XMean!.Value);
        Assert.Equal(14.0, evidence.XMedian!.Value);
        Assert.Equal(37d / 3d, evidence.YMean!.Value);
        Assert.Equal(13.0, evidence.YMedian!.Value);
        Assert.Equal(8d / 3d, evidence.MeanDifference!.Value);
        Assert.Equal(2.0, evidence.MedianDifference!.Value);
        Assert.True(evidence.IsInsufficientSample);
        Assert.Null(evidence.LowerBound);
        Assert.Null(evidence.UpperBound);
    }

    [Fact]
    public void ContinuousEvidence_ValidatesAndSnapshotsInputs()
    {
        var originalCases = new List<ExperimentPairedContinuousCaseMeasurement>
        {
            new(
                "case-1",
                xValue: 11.0,
                xStatus: ExperimentItemStatus.Succeeded,
                yValue: 10.0,
                yStatus: ExperimentItemStatus.Succeeded,
                isComparable: true),
        };

        var evidence = ExperimentPairedComparisonEvidence.CreateContinuous(
            "X",
            "Y",
            originalCases,
            bootstrapSeed: 123,
            ConfidenceLevel);
        originalCases.Clear();

        Assert.Single(evidence.Cases);

        Assert.Throws<ArgumentException>(() => new ExperimentPairedContinuousCaseMeasurement(
            "case-2",
            xValue: null,
            xStatus: ExperimentItemStatus.Succeeded,
            yValue: 10.0,
            yStatus: ExperimentItemStatus.Succeeded,
            isComparable: true));
        Assert.Throws<ArgumentException>(() => new ExperimentPairedContinuousCaseMeasurement(
            "case-2",
            xValue: 9.0,
            xStatus: ExperimentItemStatus.ExecutionFailed,
            yValue: 10.0,
            yStatus: ExperimentItemStatus.Succeeded,
            isComparable: true));
        Assert.Throws<ArgumentException>(() => ExperimentPairedComparisonEvidence.CreateContinuous(
            "X",
            "X",
            evidence.Cases,
            bootstrapSeed: 123,
            ConfidenceLevel));
        Assert.Throws<ArgumentException>(() => ExperimentPairedComparisonEvidence.CreateContinuous(
            "X",
            "Y",
            [
                evidence.Cases[0],
                new ExperimentPairedContinuousCaseMeasurement(
                    "case-1",
                    xValue: 9.0,
                    xStatus: ExperimentItemStatus.Succeeded,
                    yValue: 10.0,
                    yStatus: ExperimentItemStatus.Succeeded,
                    isComparable: true),
            ],
            bootstrapSeed: 123,
            ConfidenceLevel));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ExperimentPairedComparisonEvidence.CreateContinuous(
                "X",
                "Y",
                evidence.Cases,
                bootstrapSeed: 123,
                confidenceLevel: 0));
    }
}
