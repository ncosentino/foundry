using Microsoft.Extensions.AI.Evaluation;

using NexusLabs.Foundry.Evaluation.Harness;

namespace NexusLabs.Foundry.Evaluation.Tests.Harness;

public sealed class HarnessDiagnosticsParityTests
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;
    private readonly HarnessDiagnosticsSchemaProfileEvaluator _evaluator = new();

    [Fact]
    public async Task MissingContext_ReturnsEmptyResult()
    {
        var result = await HarnessEvaluatorTestHarness.RunWithoutContextAsync(_evaluator, _ct);

        Assert.Empty(result.Metrics);
    }

    [Theory]
    [InlineData("IterativeLoop", false, false)]
    [InlineData("PlainHarness", false, false)]
    [InlineData("Hybrid", true, true)]
    public async Task CompleteProfileAcrossArms_ReportsComplete(
        string executionMode, bool contextDiagnostics, bool artifactDiagnostics)
    {
        var evidence = new HarnessRunEvaluationEvidence
        {
            DiagnosticsSchema = CompleteProfile(executionMode, contextDiagnostics, artifactDiagnostics),
        };

        var result = await HarnessEvaluatorTestHarness.RunAsync(_evaluator, evidence, _ct);

        Assert.True(HarnessEvaluatorTestHarness.BooleanValue(result, HarnessDiagnosticsSchemaProfileEvaluator.SchemaCompleteMetricName));
        Assert.Equal(executionMode, HarnessEvaluatorTestHarness.StringValue(result, HarnessDiagnosticsSchemaProfileEvaluator.ExecutionModeMetricName));
    }

    [Fact]
    public async Task NonComparableArms_ProduceDistinctProfiles()
    {
        var iterative = await ProfileFingerprintAsync(CompleteProfile("IterativeLoop", false, false));
        var hybrid = await ProfileFingerprintAsync(CompleteProfile("Hybrid", true, true));
        var plain = await ProfileFingerprintAsync(CompleteProfile("PlainHarness", false, false));

        // The hybrid arm captures context/artifact diagnostics the others do not, so its schema
        // fingerprint differs — the precondition a downstream parity check relies on.
        Assert.NotEqual(iterative, hybrid);
        Assert.NotEqual(plain, hybrid);
        Assert.NotEqual(iterative, plain);
    }

    [Fact]
    public async Task IdenticalSchemas_ProduceIdenticalProfiles()
    {
        var first = await ProfileFingerprintAsync(CompleteProfile("Hybrid", true, true));
        var second = await ProfileFingerprintAsync(CompleteProfile("Hybrid", true, true));

        Assert.Equal(first, second);
    }

    [Fact]
    public async Task MissingCoreField_ReportsIncomplete()
    {
        var evidence = new HarnessRunEvaluationEvidence
        {
            DiagnosticsSchema = CompleteProfile("IterativeLoop", false, false) with
            {
                HasAggregateTokenUsage = false,
            },
        };

        var result = await HarnessEvaluatorTestHarness.RunAsync(_evaluator, evidence, _ct);

        Assert.False(HarnessEvaluatorTestHarness.BooleanValue(result, HarnessDiagnosticsSchemaProfileEvaluator.SchemaCompleteMetricName));
    }

    [Fact]
    public async Task EmptyExecutionMode_ReportsIncomplete()
    {
        var evidence = new HarnessRunEvaluationEvidence
        {
            DiagnosticsSchema = CompleteProfile("IterativeLoop", false, false) with { ExecutionMode = "" },
        };

        var result = await HarnessEvaluatorTestHarness.RunAsync(_evaluator, evidence, _ct);

        Assert.False(HarnessEvaluatorTestHarness.BooleanValue(result, HarnessDiagnosticsSchemaProfileEvaluator.SchemaCompleteMetricName));
    }

    [Fact]
    public async Task FieldCount_CountsCapturedFields()
    {
        var evidence = new HarnessRunEvaluationEvidence
        {
            DiagnosticsSchema = CompleteProfile("Hybrid", true, true),
        };

        var result = await HarnessEvaluatorTestHarness.RunAsync(_evaluator, evidence, _ct);

        Assert.Equal(8, HarnessEvaluatorTestHarness.NumericValue(result, HarnessDiagnosticsSchemaProfileEvaluator.FieldCountMetricName));
    }

    private async Task<string> ProfileFingerprintAsync(HarnessDiagnosticsSchemaProfile profile)
    {
        var result = await HarnessEvaluatorTestHarness.RunAsync(
            _evaluator,
            new HarnessRunEvaluationEvidence { DiagnosticsSchema = profile },
            _ct);
        return HarnessEvaluatorTestHarness.StringValue(result, HarnessDiagnosticsSchemaProfileEvaluator.SchemaProfileMetricName);
    }

    private static HarnessDiagnosticsSchemaProfile CompleteProfile(
        string executionMode, bool contextDiagnostics, bool artifactDiagnostics) => new()
    {
        ExecutionMode = executionMode,
        HasAggregateTokenUsage = true,
        HasChatCompletionDiagnostics = true,
        HasToolCallDiagnostics = true,
        HasTimingBoundaries = true,
        HasInputMessages = true,
        HasOutputResponse = true,
        SupportsContextDiagnostics = contextDiagnostics,
        SupportsArtifactDiagnostics = artifactDiagnostics,
    };
}
