using NexusLabs.Foundry.Evaluation.Harness;
using NexusLabs.Foundry.MicrosoftAgentFramework.Diagnostics;

namespace NexusLabs.Foundry.Evaluation.Tests.Harness;

public sealed class HarnessArtifactEvaluatorTests
{
    private const string ReferenceId = "artifact://sha256/" + Digest;
    private const string Digest = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;
    private readonly HarnessArtifactReuseEvaluator _reuse = new();
    private readonly HarnessArtifactRehydrationEvaluator _rehydration = new();

    [Fact]
    public async Task Reuse_NullSlice_ReturnsEmpty()
    {
        var result = await HarnessEvaluatorTestHarness.RunAsync(_reuse, new HarnessRunEvaluationEvidence(), _ct);
        Assert.Empty(result.Metrics);
    }

    [Fact]
    public async Task Reuse_OffloadedReference_IsConsistentWithByteSavings()
    {
        var offloaded = new HarnessArtifactDecisionEvidence
        {
            Operation = HarnessArtifactOperationCategory.Offload,
            Outcome = HarnessArtifactOutcomeCategory.Offloaded,
            Content = HarnessArtifactContentCategory.ToolResult,
            Reason = HarnessArtifactDecisionReason.ThresholdExceeded,
            ConfiguredThresholdOrBudget = 1000,
            InputUtf8Bytes = 5000,
            ObservedUtf8ByteSize = 5000,
            OutputUtf8Bytes = 82,
            ReferenceId = ReferenceId,
        };

        var result = await HarnessEvaluatorTestHarness.RunAsync(_reuse, Evidence(offloaded), _ct);

        Assert.True(HarnessEvaluatorTestHarness.BooleanValue(result, HarnessArtifactReuseEvaluator.OffloadConsistentMetricName));
        Assert.Equal(1, HarnessEvaluatorTestHarness.NumericValue(result, HarnessArtifactReuseEvaluator.OffloadCountMetricName));
        Assert.Equal(5000 - 82, HarnessEvaluatorTestHarness.NumericValue(result, HarnessArtifactReuseEvaluator.ByteSavingsMetricName));
    }

    [Fact]
    public async Task Reuse_ExistingReference_CountsReuse()
    {
        var reused = new HarnessArtifactDecisionEvidence
        {
            Operation = HarnessArtifactOperationCategory.Offload,
            Outcome = HarnessArtifactOutcomeCategory.ExistingReference,
            Content = HarnessArtifactContentCategory.ToolResult,
            Reason = HarnessArtifactDecisionReason.ExistingContentMatch,
            ConfiguredThresholdOrBudget = 1000,
            InputUtf8Bytes = 4000,
            ObservedUtf8ByteSize = 4000,
            OutputUtf8Bytes = 82,
            ReferenceId = ReferenceId,
        };

        var result = await HarnessEvaluatorTestHarness.RunAsync(_reuse, Evidence(reused), _ct);

        Assert.Equal(1, HarnessEvaluatorTestHarness.NumericValue(result, HarnessArtifactReuseEvaluator.ReuseCountMetricName));
        Assert.True(HarnessEvaluatorTestHarness.BooleanValue(result, HarnessArtifactReuseEvaluator.OffloadConsistentMetricName));
    }

    [Fact]
    public async Task Reuse_InlineWithReference_IsInconsistent()
    {
        var inlineWithRef = new HarnessArtifactDecisionEvidence
        {
            Operation = HarnessArtifactOperationCategory.Offload,
            Outcome = HarnessArtifactOutcomeCategory.Inline,
            Content = HarnessArtifactContentCategory.ToolResult,
            Reason = HarnessArtifactDecisionReason.BelowThreshold,
            ConfiguredThresholdOrBudget = 1000,
            InputUtf8Bytes = 100,
            ObservedUtf8ByteSize = 100,
            OutputUtf8Bytes = 100,
            ReferenceId = ReferenceId,
        };

        var result = await HarnessEvaluatorTestHarness.RunAsync(_reuse, Evidence(inlineWithRef), _ct);

        Assert.False(HarnessEvaluatorTestHarness.BooleanValue(result, HarnessArtifactReuseEvaluator.OffloadConsistentMetricName));
    }

    [Fact]
    public async Task Reuse_OffloadedMissingReference_IsInconsistent()
    {
        var missingRef = new HarnessArtifactDecisionEvidence
        {
            Operation = HarnessArtifactOperationCategory.Offload,
            Outcome = HarnessArtifactOutcomeCategory.Offloaded,
            Content = HarnessArtifactContentCategory.ToolResult,
            Reason = HarnessArtifactDecisionReason.ThresholdExceeded,
            ConfiguredThresholdOrBudget = 1000,
            InputUtf8Bytes = 5000,
            ObservedUtf8ByteSize = 5000,
            OutputUtf8Bytes = null,
            ReferenceId = null,
        };

        var result = await HarnessEvaluatorTestHarness.RunAsync(_reuse, Evidence(missingRef), _ct);

        Assert.False(HarnessEvaluatorTestHarness.BooleanValue(result, HarnessArtifactReuseEvaluator.OffloadConsistentMetricName));
    }

    [Fact]
    public async Task PresentNullDecision_ScoresInvalidInsteadOfThrowing()
    {
        var evidence = new HarnessRunEvaluationEvidence { ArtifactDecisions = [null!] };

        var reuse = await HarnessEvaluatorTestHarness.RunAsync(_reuse, evidence, _ct);
        var rehydration = await HarnessEvaluatorTestHarness.RunAsync(_rehydration, evidence, _ct);

        Assert.False(HarnessEvaluatorTestHarness.BooleanValue(
            reuse,
            HarnessArtifactReuseEvaluator.OffloadConsistentMetricName));
        Assert.False(HarnessEvaluatorTestHarness.BooleanValue(
            rehydration,
            HarnessArtifactRehydrationEvaluator.RehydrationConsistentMetricName));
    }

    [Fact]
    public async Task Rehydration_NullSlice_ReturnsEmpty()
    {
        var result = await HarnessEvaluatorTestHarness.RunAsync(_rehydration, new HarnessRunEvaluationEvidence(), _ct);
        Assert.Empty(result.Metrics);
    }

    [Fact]
    public async Task Rehydration_Resolved_IsConsistentAndVerified()
    {
        var resolved = new HarnessArtifactDecisionEvidence
        {
            Operation = HarnessArtifactOperationCategory.Rehydration,
            Outcome = HarnessArtifactOutcomeCategory.Resolved,
            Content = HarnessArtifactContentCategory.RecoverableContextSegment,
            Reason = HarnessArtifactDecisionReason.DigestVerified,
            ConfiguredThresholdOrBudget = 10000,
            InputUtf8Bytes = 82,
            ObservedUtf8ByteSize = 4000,
            OutputUtf8Bytes = 4000,
            ReferenceId = ReferenceId,
        };

        var result = await HarnessEvaluatorTestHarness.RunAsync(_rehydration, Evidence(resolved), _ct);

        Assert.Equal(1, HarnessEvaluatorTestHarness.NumericValue(result, HarnessArtifactRehydrationEvaluator.ResolvedCountMetricName));
        Assert.True(HarnessEvaluatorTestHarness.BooleanValue(result, HarnessArtifactRehydrationEvaluator.RehydrationConsistentMetricName));
        Assert.True(HarnessEvaluatorTestHarness.BooleanValue(result, HarnessArtifactRehydrationEvaluator.DigestVerifiedMetricName));
    }

    [Fact]
    public async Task Rehydration_Stale_NotResolvedAndUnverified()
    {
        var stale = new HarnessArtifactDecisionEvidence
        {
            Operation = HarnessArtifactOperationCategory.Rehydration,
            Outcome = HarnessArtifactOutcomeCategory.Stale,
            Content = HarnessArtifactContentCategory.RecoverableContextSegment,
            Reason = HarnessArtifactDecisionReason.DigestMismatch,
            ConfiguredThresholdOrBudget = 10000,
            InputUtf8Bytes = 82,
            ObservedUtf8ByteSize = 4000,
            OutputUtf8Bytes = null,
            ReferenceId = ReferenceId,
        };

        var result = await HarnessEvaluatorTestHarness.RunAsync(_rehydration, Evidence(stale), _ct);

        Assert.Equal(0, HarnessEvaluatorTestHarness.NumericValue(result, HarnessArtifactRehydrationEvaluator.ResolvedCountMetricName));
        Assert.True(HarnessEvaluatorTestHarness.BooleanValue(result, HarnessArtifactRehydrationEvaluator.RehydrationConsistentMetricName));
        Assert.True(HarnessEvaluatorTestHarness.BooleanValue(result, HarnessArtifactRehydrationEvaluator.DigestVerifiedMetricName));
    }

    [Fact]
    public async Task Rehydration_ResolvedMissingOutput_IsInconsistent()
    {
        var badResolved = new HarnessArtifactDecisionEvidence
        {
            Operation = HarnessArtifactOperationCategory.Rehydration,
            Outcome = HarnessArtifactOutcomeCategory.Resolved,
            Content = HarnessArtifactContentCategory.RecoverableContextSegment,
            Reason = HarnessArtifactDecisionReason.DigestVerified,
            ConfiguredThresholdOrBudget = 10000,
            InputUtf8Bytes = 82,
            ObservedUtf8ByteSize = 4000,
            OutputUtf8Bytes = null,
            ReferenceId = ReferenceId,
        };

        var result = await HarnessEvaluatorTestHarness.RunAsync(_rehydration, Evidence(badResolved), _ct);

        Assert.False(HarnessEvaluatorTestHarness.BooleanValue(result, HarnessArtifactRehydrationEvaluator.RehydrationConsistentMetricName));
    }

    private static HarnessRunEvaluationEvidence Evidence(params HarnessArtifactDecisionEvidence[] decisions) =>
        new() { ArtifactDecisions = decisions };
}
