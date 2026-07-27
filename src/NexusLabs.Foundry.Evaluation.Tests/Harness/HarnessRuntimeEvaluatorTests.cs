using NexusLabs.Foundry.Evaluation.Harness;

namespace NexusLabs.Foundry.Evaluation.Tests.Harness;

public sealed class HarnessRuntimeEvaluatorTests
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    // -------- Telemetry --------

    [Fact]
    public async Task Telemetry_NullSlice_ReturnsEmpty()
    {
        var result = await HarnessEvaluatorTestHarness.RunAsync(
            new HarnessTelemetryCompletenessEvaluator(), new HarnessRunEvaluationEvidence(), _ct);
        Assert.Empty(result.Metrics);
    }

    [Fact]
    public async Task Telemetry_AllCaptured_ReportsComplete()
    {
        var evidence = new HarnessRunEvaluationEvidence
        {
            Telemetry = new HarnessTelemetryEvidence
            {
                ExpectedChatCompletionCount = 3,
                ObservedChatCompletionCount = 3,
                ExpectedToolCallCount = 2,
                ObservedToolCallCount = 2,
                HasAggregateTokenUsage = true,
                HasCallDurations = true,
                HasProgressEvents = true,
            },
        };

        var result = await HarnessEvaluatorTestHarness.RunAsync(new HarnessTelemetryCompletenessEvaluator(), evidence, _ct);

        Assert.True(HarnessEvaluatorTestHarness.BooleanValue(result, HarnessTelemetryCompletenessEvaluator.TelemetryCompleteMetricName));
        Assert.True(HarnessEvaluatorTestHarness.BooleanValue(result, HarnessTelemetryCompletenessEvaluator.CountsMatchMetricName));
    }

    [Fact]
    public async Task Telemetry_MissingField_ReportsIncomplete()
    {
        var evidence = new HarnessRunEvaluationEvidence
        {
            Telemetry = new HarnessTelemetryEvidence
            {
                ExpectedChatCompletionCount = 3,
                ObservedChatCompletionCount = 3,
                ExpectedToolCallCount = 2,
                ObservedToolCallCount = 2,
                HasAggregateTokenUsage = false,
                HasCallDurations = true,
                HasProgressEvents = true,
                MissingFields = ["AggregateTokenUsage"],
            },
        };

        var result = await HarnessEvaluatorTestHarness.RunAsync(new HarnessTelemetryCompletenessEvaluator(), evidence, _ct);

        Assert.False(HarnessEvaluatorTestHarness.BooleanValue(result, HarnessTelemetryCompletenessEvaluator.TelemetryCompleteMetricName));
        Assert.Equal(1, HarnessEvaluatorTestHarness.NumericValue(result, HarnessTelemetryCompletenessEvaluator.MissingFieldCountMetricName));
    }

    // -------- Event lifecycle --------

    [Fact]
    public async Task Lifecycle_NullSlice_ReturnsEmpty()
    {
        var result = await HarnessEvaluatorTestHarness.RunAsync(
            new HarnessEventLifecycleEvaluator(), new HarnessRunEvaluationEvidence(), _ct);
        Assert.Empty(result.Metrics);
    }

    [Fact]
    public async Task Lifecycle_OrderedAndPaired_ReportsValid()
    {
        var evidence = new HarnessRunEvaluationEvidence
        {
            LifecycleEvents =
            [
                Event(HarnessLifecycleEventKind.ContextCompaction, HarnessLifecyclePhase.Started, 1, "asm-1"),
                Event(HarnessLifecycleEventKind.ContextCompaction, HarnessLifecyclePhase.Completed, 2, "asm-1"),
                Event(HarnessLifecycleEventKind.ContextComposed, HarnessLifecyclePhase.Instant, 3, "asm-1"),
            ],
        };

        var result = await HarnessEvaluatorTestHarness.RunAsync(new HarnessEventLifecycleEvaluator(), evidence, _ct);

        Assert.True(HarnessEvaluatorTestHarness.BooleanValue(result, HarnessEventLifecycleEvaluator.OrderedMetricName));
        Assert.True(HarnessEvaluatorTestHarness.BooleanValue(result, HarnessEventLifecycleEvaluator.PairedMetricName));
        Assert.Equal(0, HarnessEvaluatorTestHarness.NumericValue(result, HarnessEventLifecycleEvaluator.UnpairedCountMetricName));
    }

    [Fact]
    public async Task Lifecycle_OutOfOrderSequence_ReportsUnordered()
    {
        var evidence = new HarnessRunEvaluationEvidence
        {
            LifecycleEvents =
            [
                Event(HarnessLifecycleEventKind.Agent, HarnessLifecyclePhase.Started, 5, "a-1"),
                Event(HarnessLifecycleEventKind.Agent, HarnessLifecyclePhase.Completed, 3, "a-1"),
            ],
        };

        var result = await HarnessEvaluatorTestHarness.RunAsync(new HarnessEventLifecycleEvaluator(), evidence, _ct);

        Assert.False(HarnessEvaluatorTestHarness.BooleanValue(result, HarnessEventLifecycleEvaluator.OrderedMetricName));
    }

    [Fact]
    public async Task Lifecycle_OrphanStarted_ReportsUnpaired()
    {
        var evidence = new HarnessRunEvaluationEvidence
        {
            LifecycleEvents =
            [
                Event(HarnessLifecycleEventKind.ContextCompaction, HarnessLifecyclePhase.Started, 1, "asm-1"),
                Event(HarnessLifecycleEventKind.ContextCompaction, HarnessLifecyclePhase.Started, 2, "asm-2"),
                Event(HarnessLifecycleEventKind.ContextCompaction, HarnessLifecyclePhase.Completed, 3, "asm-1"),
            ],
        };

        var result = await HarnessEvaluatorTestHarness.RunAsync(new HarnessEventLifecycleEvaluator(), evidence, _ct);

        Assert.False(HarnessEvaluatorTestHarness.BooleanValue(result, HarnessEventLifecycleEvaluator.PairedMetricName));
        Assert.Equal(1, HarnessEvaluatorTestHarness.NumericValue(result, HarnessEventLifecycleEvaluator.UnpairedCountMetricName));
    }

    // -------- Identity attribution --------

    [Fact]
    public async Task Identity_NullSlice_ReturnsEmpty()
    {
        var result = await HarnessEvaluatorTestHarness.RunAsync(
            new HarnessIdentityAttributionEvaluator(), new HarnessRunEvaluationEvidence(), _ct);
        Assert.Empty(result.Metrics);
    }

    [Fact]
    public async Task Identity_SingleExpectedOwner_ReportsAttributed()
    {
        var evidence = new HarnessRunEvaluationEvidence
        {
            IdentityAttribution = new HarnessIdentityAttributionEvidence
            {
                WorkflowId = "wf-1",
                ExpectedAgentId = "agent-1",
                ObservedAgentIds = ["agent-1", "agent-1"],
                UnattributedRecordCount = 0,
            },
        };

        var result = await HarnessEvaluatorTestHarness.RunAsync(new HarnessIdentityAttributionEvaluator(), evidence, _ct);

        Assert.True(HarnessEvaluatorTestHarness.BooleanValue(result, HarnessIdentityAttributionEvaluator.AttributedMetricName));
        Assert.True(HarnessEvaluatorTestHarness.BooleanValue(result, HarnessIdentityAttributionEvaluator.SingleOwnerMetricName));
    }

    [Fact]
    public async Task Identity_ForeignOwner_ReportsUnattributed()
    {
        var evidence = new HarnessRunEvaluationEvidence
        {
            IdentityAttribution = new HarnessIdentityAttributionEvidence
            {
                WorkflowId = "wf-1",
                ExpectedAgentId = "agent-1",
                ObservedAgentIds = ["agent-1", "agent-2"],
                UnattributedRecordCount = 0,
            },
        };

        var result = await HarnessEvaluatorTestHarness.RunAsync(new HarnessIdentityAttributionEvaluator(), evidence, _ct);

        Assert.False(HarnessEvaluatorTestHarness.BooleanValue(result, HarnessIdentityAttributionEvaluator.AttributedMetricName));
        Assert.False(HarnessEvaluatorTestHarness.BooleanValue(result, HarnessIdentityAttributionEvaluator.SingleOwnerMetricName));
    }

    [Fact]
    public async Task Identity_UnattributedRecords_ReportsUnattributed()
    {
        var evidence = new HarnessRunEvaluationEvidence
        {
            IdentityAttribution = new HarnessIdentityAttributionEvidence
            {
                WorkflowId = "wf-1",
                ExpectedAgentId = "agent-1",
                ObservedAgentIds = ["agent-1"],
                UnattributedRecordCount = 2,
            },
        };

        var result = await HarnessEvaluatorTestHarness.RunAsync(new HarnessIdentityAttributionEvaluator(), evidence, _ct);

        Assert.False(HarnessEvaluatorTestHarness.BooleanValue(result, HarnessIdentityAttributionEvaluator.AttributedMetricName));
        Assert.Equal(2, HarnessEvaluatorTestHarness.NumericValue(result, HarnessIdentityAttributionEvaluator.UnattributedCountMetricName));
    }

    // -------- Cancellation --------

    [Fact]
    public async Task Cancellation_NullSlice_ReturnsEmpty()
    {
        var result = await HarnessEvaluatorTestHarness.RunAsync(
            new HarnessCancellationEvaluator(), new HarnessRunEvaluationEvidence(), _ct);
        Assert.Empty(result.Metrics);
    }

    [Fact]
    public async Task Cancellation_MatchingCategoryNoSuccessOutput_ReportsAppropriate()
    {
        var evidence = new HarnessRunEvaluationEvidence
        {
            Cancellation = new HarnessCancellationEvidence
            {
                ExpectedCategory = HarnessRunTerminalCategory.PerAttemptTimeout,
                ObservedCategory = HarnessRunTerminalCategory.PerAttemptTimeout,
                ProducedSuccessShapedOutput = false,
            },
        };

        var result = await HarnessEvaluatorTestHarness.RunAsync(new HarnessCancellationEvaluator(), evidence, _ct);

        Assert.True(HarnessEvaluatorTestHarness.BooleanValue(result, HarnessCancellationEvaluator.AppropriateMetricName));
    }

    [Fact]
    public async Task Cancellation_SuccessShapedOutput_ReportsInappropriate()
    {
        var evidence = new HarnessRunEvaluationEvidence
        {
            Cancellation = new HarnessCancellationEvidence
            {
                ExpectedCategory = HarnessRunTerminalCategory.TaskCanceled,
                ObservedCategory = HarnessRunTerminalCategory.TaskCanceled,
                ProducedSuccessShapedOutput = true,
            },
        };

        var result = await HarnessEvaluatorTestHarness.RunAsync(new HarnessCancellationEvaluator(), evidence, _ct);

        Assert.False(HarnessEvaluatorTestHarness.BooleanValue(result, HarnessCancellationEvaluator.AppropriateMetricName));
        Assert.False(HarnessEvaluatorTestHarness.BooleanValue(result, HarnessCancellationEvaluator.NoSuccessShapedOutputMetricName));
    }

    [Fact]
    public async Task Cancellation_CategoryMismatch_ReportsInappropriate()
    {
        var evidence = new HarnessRunEvaluationEvidence
        {
            Cancellation = new HarnessCancellationEvidence
            {
                ExpectedCategory = HarnessRunTerminalCategory.TaskCanceled,
                ObservedCategory = HarnessRunTerminalCategory.Completed,
                ProducedSuccessShapedOutput = false,
            },
        };

        var result = await HarnessEvaluatorTestHarness.RunAsync(new HarnessCancellationEvaluator(), evidence, _ct);

        Assert.False(HarnessEvaluatorTestHarness.BooleanValue(result, HarnessCancellationEvaluator.CategoryMatchMetricName));
    }

    // -------- Session continuity --------

    [Fact]
    public async Task Continuity_NullSlice_ReturnsEmpty()
    {
        var result = await HarnessEvaluatorTestHarness.RunAsync(
            new HarnessSessionContinuityEvaluator(), new HarnessRunEvaluationEvidence(), _ct);
        Assert.Empty(result.Metrics);
    }

    [Fact]
    public async Task Continuity_AllPresent_ReportsPreserved()
    {
        var evidence = new HarnessRunEvaluationEvidence
        {
            SessionContinuity = new HarnessSessionContinuityEvidence
            {
                RequiredDecisionReferences = ["decision:accept-plan"],
                PresentDecisionReferences = ["decision:accept-plan", "decision:extra"],
                RequiredStateKeys = ["mode", "todos"],
                PresentStateKeys = ["mode", "todos"],
            },
        };

        var result = await HarnessEvaluatorTestHarness.RunAsync(new HarnessSessionContinuityEvaluator(), evidence, _ct);

        Assert.True(HarnessEvaluatorTestHarness.BooleanValue(result, HarnessSessionContinuityEvaluator.ContinuityPreservedMetricName));
        Assert.Equal(0, HarnessEvaluatorTestHarness.NumericValue(result, HarnessSessionContinuityEvaluator.MissingDecisionReferencesMetricName));
    }

    [Fact]
    public async Task Continuity_MissingReferencesAndKeys_ReportsNotPreserved()
    {
        var evidence = new HarnessRunEvaluationEvidence
        {
            SessionContinuity = new HarnessSessionContinuityEvidence
            {
                RequiredDecisionReferences = ["decision:accept-plan", "decision:approve"],
                PresentDecisionReferences = ["decision:accept-plan"],
                RequiredStateKeys = ["mode", "todos"],
                PresentStateKeys = ["mode"],
            },
        };

        var result = await HarnessEvaluatorTestHarness.RunAsync(new HarnessSessionContinuityEvaluator(), evidence, _ct);

        Assert.False(HarnessEvaluatorTestHarness.BooleanValue(result, HarnessSessionContinuityEvaluator.ContinuityPreservedMetricName));
        Assert.Equal(1, HarnessEvaluatorTestHarness.NumericValue(result, HarnessSessionContinuityEvaluator.MissingDecisionReferencesMetricName));
        Assert.Equal(1, HarnessEvaluatorTestHarness.NumericValue(result, HarnessSessionContinuityEvaluator.MissingStateKeysMetricName));
    }

    private static HarnessLifecycleEventEvidence Event(
        HarnessLifecycleEventKind kind, HarnessLifecyclePhase phase, long sequence, string correlationId) => new()
    {
        Kind = kind,
        Phase = phase,
        SequenceNumber = sequence,
        CorrelationId = correlationId,
        AgentId = "agent-1",
    };
}
