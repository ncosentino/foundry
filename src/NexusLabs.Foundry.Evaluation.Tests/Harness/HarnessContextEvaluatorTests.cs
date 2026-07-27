using NexusLabs.Foundry.Evaluation.Harness;
using NexusLabs.Foundry.MicrosoftAgentFramework.Diagnostics;

namespace NexusLabs.Foundry.Evaluation.Tests.Harness;

public sealed class HarnessContextEvaluatorTests
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;
    private readonly HarnessContextSafetyEvaluator _safety = new();
    private readonly HarnessCompactionValidityEvaluator _validity = new();

    [Fact]
    public async Task Safety_NullSlice_ReturnsEmpty()
    {
        var result = await HarnessEvaluatorTestHarness.RunAsync(
            _safety, new HarnessRunEvaluationEvidence(), _ct);

        Assert.Empty(result.Metrics);
    }

    [Fact]
    public async Task Safety_ValidSuccess_ReportsSafeAndValid()
    {
        var evidence = Evidence(SuccessWithinLimit());

        var result = await HarnessEvaluatorTestHarness.RunAsync(_safety, evidence, _ct);

        Assert.True(HarnessEvaluatorTestHarness.BooleanValue(result, HarnessContextSafetyEvaluator.NoOverflowMetricName));
        Assert.True(HarnessEvaluatorTestHarness.BooleanValue(result, HarnessContextSafetyEvaluator.StructurallyValidMetricName));
        Assert.Equal(0, HarnessEvaluatorTestHarness.NumericValue(result, HarnessContextSafetyEvaluator.OverflowCountMetricName));
        Assert.Equal(1, HarnessEvaluatorTestHarness.NumericValue(result, HarnessContextSafetyEvaluator.CompactionCountMetricName));
    }

    [Fact]
    public async Task Safety_EmptySlice_IsVacuouslySafe()
    {
        var evidence = new HarnessRunEvaluationEvidence { ContextCompactions = [] };

        var result = await HarnessEvaluatorTestHarness.RunAsync(_safety, evidence, _ct);

        Assert.True(HarnessEvaluatorTestHarness.BooleanValue(result, HarnessContextSafetyEvaluator.NoOverflowMetricName));
        Assert.Equal(0, HarnessEvaluatorTestHarness.NumericValue(result, HarnessContextSafetyEvaluator.CompactionCountMetricName));
    }

    [Fact]
    public async Task Safety_SuccessOverHardLimit_ReportsOverflow()
    {
        var overflow = SuccessWithinLimit() with { FinalSize = 1200, CategoryContributionSizeSum = 1200 };
        var result = await HarnessEvaluatorTestHarness.RunAsync(_safety, Evidence(overflow), _ct);

        Assert.False(HarnessEvaluatorTestHarness.BooleanValue(result, HarnessContextSafetyEvaluator.NoOverflowMetricName));
        Assert.Equal(1, HarnessEvaluatorTestHarness.NumericValue(result, HarnessContextSafetyEvaluator.OverflowCountMetricName));
    }

    [Fact]
    public async Task Safety_SuccessWithMismatchedAttribution_ReportsStructurallyInvalid()
    {
        var invalid = SuccessWithinLimit() with { CategoryContributionSizeSum = 700 };
        var result = await HarnessEvaluatorTestHarness.RunAsync(_safety, Evidence(invalid), _ct);

        Assert.False(HarnessEvaluatorTestHarness.BooleanValue(result, HarnessContextSafetyEvaluator.StructurallyValidMetricName));
    }

    [Fact]
    public async Task Safety_TerminationWithAttribution_ReportsStructurallyInvalid()
    {
        var invalidTermination = Termination() with { CategoryContributionCount = 1, FinalSequenceValid = true };
        var result = await HarnessEvaluatorTestHarness.RunAsync(_safety, Evidence(invalidTermination), _ct);

        Assert.False(HarnessEvaluatorTestHarness.BooleanValue(result, HarnessContextSafetyEvaluator.StructurallyValidMetricName));
    }

    [Fact]
    public async Task Validity_NullSlice_ReturnsEmpty()
    {
        var result = await HarnessEvaluatorTestHarness.RunAsync(
            _validity, new HarnessRunEvaluationEvidence(), _ct);

        Assert.Empty(result.Metrics);
    }

    [Fact]
    public async Task Validity_ReducedShrinks_ReportsMonotonic()
    {
        var reduced = new HarnessContextCompactionEvidence
        {
            Outcome = HarnessContextCompactionOutcome.Reduced,
            MeasurementUnit = HarnessContextMeasurementUnit.Utf8Bytes,
            OriginalSize = 1500,
            FinalSize = 900,
            TriggerThreshold = 900,
            HardLimit = 1000,
            AttemptCount = 2,
            Stages = [HarnessContextAssemblyStageCategory.SnapshotCaptured, HarnessContextAssemblyStageCategory.ReducerAttempt],
            CategoryContributionSizeSum = 900,
            CategoryContributionCount = 2,
            FinalSequenceValid = true,
        };

        var result = await HarnessEvaluatorTestHarness.RunAsync(_validity, Evidence(reduced), _ct);

        Assert.True(HarnessEvaluatorTestHarness.BooleanValue(result, HarnessCompactionValidityEvaluator.OutcomeConsistentMetricName));
        Assert.True(HarnessEvaluatorTestHarness.BooleanValue(result, HarnessCompactionValidityEvaluator.ReducedMonotonicMetricName));
        Assert.Equal(2, HarnessEvaluatorTestHarness.NumericValue(result, HarnessCompactionValidityEvaluator.TotalAttemptsMetricName));
    }

    [Fact]
    public async Task Validity_ReducedGrows_ReportsNonMonotonic()
    {
        var grew = new HarnessContextCompactionEvidence
        {
            Outcome = HarnessContextCompactionOutcome.Reduced,
            MeasurementUnit = HarnessContextMeasurementUnit.Utf8Bytes,
            OriginalSize = 500,
            FinalSize = 900,
            TriggerThreshold = 900,
            HardLimit = 1000,
            AttemptCount = 1,
            Stages = [HarnessContextAssemblyStageCategory.ReducerAttempt],
            CategoryContributionSizeSum = 900,
            CategoryContributionCount = 1,
            FinalSequenceValid = true,
        };

        var result = await HarnessEvaluatorTestHarness.RunAsync(_validity, Evidence(grew), _ct);

        Assert.False(HarnessEvaluatorTestHarness.BooleanValue(result, HarnessCompactionValidityEvaluator.ReducedMonotonicMetricName));
    }

    [Fact]
    public async Task Validity_Termination_CountsTermination()
    {
        var result = await HarnessEvaluatorTestHarness.RunAsync(_validity, Evidence(Termination()), _ct);

        Assert.True(HarnessEvaluatorTestHarness.BooleanValue(result, HarnessCompactionValidityEvaluator.OutcomeConsistentMetricName));
        Assert.Equal(1, HarnessEvaluatorTestHarness.NumericValue(result, HarnessCompactionValidityEvaluator.TerminationCountMetricName));
    }

    [Fact]
    public async Task Validity_SuccessWithInvalidSequence_ReportsInconsistent()
    {
        var invalid = SuccessWithinLimit() with { FinalSequenceValid = false };
        var result = await HarnessEvaluatorTestHarness.RunAsync(_validity, Evidence(invalid), _ct);

        Assert.False(HarnessEvaluatorTestHarness.BooleanValue(result, HarnessCompactionValidityEvaluator.OutcomeConsistentMetricName));
    }

    private static HarnessRunEvaluationEvidence Evidence(params HarnessContextCompactionEvidence[] compactions) =>
        new() { ContextCompactions = compactions };

    private static HarnessContextCompactionEvidence SuccessWithinLimit() => new()
    {
        Outcome = HarnessContextCompactionOutcome.WithinLimit,
        MeasurementUnit = HarnessContextMeasurementUnit.Utf8Bytes,
        OriginalSize = 1000,
        FinalSize = 800,
        TriggerThreshold = 900,
        HardLimit = 1000,
        AttemptCount = 1,
        Stages = [HarnessContextAssemblyStageCategory.SnapshotCaptured],
        CategoryContributionSizeSum = 800,
        CategoryContributionCount = 2,
        FinalSequenceValid = true,
    };

    private static HarnessContextCompactionEvidence Termination() => new()
    {
        Outcome = HarnessContextCompactionOutcome.Irreducible,
        MeasurementUnit = HarnessContextMeasurementUnit.Utf8Bytes,
        OriginalSize = 2000,
        FinalSize = 1500,
        TriggerThreshold = 900,
        HardLimit = 1000,
        AttemptCount = 3,
        Stages = [HarnessContextAssemblyStageCategory.SnapshotCaptured, HarnessContextAssemblyStageCategory.DeterministicFallback],
        CategoryContributionSizeSum = 0,
        CategoryContributionCount = 0,
        FinalSequenceValid = null,
    };
}
