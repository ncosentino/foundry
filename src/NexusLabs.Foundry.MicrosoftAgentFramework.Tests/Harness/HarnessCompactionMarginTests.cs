// Tests intentionally exercise explicit CancellationToken parameters (including
// CancellationToken.None) directly. This is the behavior under test, not an oversight of
// TestContext.Current.CancellationToken.
#pragma warning disable xUnit1051

using NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Context;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Tests.Harness;

/// <summary>
/// Tests for <see cref="HarnessHybridContextPolicy"/>'s required, explicitly validated construction
/// parameters and its trigger-margin arithmetic: every hard limit, trigger margin, recent-message
/// retention count, maximum compaction attempt bound, and preservation label/version must be supplied
/// and must satisfy an independent positivity/ordering rule, and the exact
/// <c>hardLimit - triggerMargin</c> threshold boundary must never trigger strictly below it and always
/// trigger at or above it.
/// </summary>
public sealed class HarnessCompactionMarginTests
{
    private static readonly HarnessUtf8ContextSizeEstimator DefaultEstimator = new();

    // --- Required, explicitly validated construction parameters ---------------------------

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_NonPositiveHardLimit_ThrowsArgumentOutOfRangeException(int hardLimit)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            HarnessCompactionTestFixture.CreatePolicy(hardLimit, 1, 1, 1, DefaultEstimator));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_NonPositiveTriggerMargin_ThrowsArgumentOutOfRangeException(int triggerMargin)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            HarnessCompactionTestFixture.CreatePolicy(100, triggerMargin, 1, 1, DefaultEstimator));
    }

    [Theory]
    [InlineData(100)]
    [InlineData(150)]
    public void Create_TriggerMarginAtOrAboveHardLimit_ThrowsArgumentOutOfRangeException(int triggerMargin)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            HarnessCompactionTestFixture.CreatePolicy(100, triggerMargin, 1, 1, DefaultEstimator));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_NonPositiveRecentMessageRetentionCount_ThrowsArgumentOutOfRangeException(int recentMessageRetentionCount)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            HarnessCompactionTestFixture.CreatePolicy(100, 10, recentMessageRetentionCount, 1, DefaultEstimator));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_NonPositiveMaximumCompactionAttempts_ThrowsArgumentOutOfRangeException(int maximumCompactionAttempts)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            HarnessCompactionTestFixture.CreatePolicy(100, 10, 1, maximumCompactionAttempts, DefaultEstimator));
    }

    [Fact]
    public void Create_NullPreservationLabel_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            HarnessHybridContextPolicy.Create(100, 10, 1, 1, null!, 1, DefaultEstimator));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_EmptyOrWhiteSpacePreservationLabel_ThrowsArgumentException(string preservationLabel)
    {
        Assert.Throws<ArgumentException>(() =>
            HarnessHybridContextPolicy.Create(100, 10, 1, 1, preservationLabel, 1, DefaultEstimator));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_NonPositivePreservationVersion_ThrowsArgumentOutOfRangeException(int preservationVersion)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            HarnessHybridContextPolicy.Create(100, 10, 1, 1, "label", preservationVersion, DefaultEstimator));
    }

    [Fact]
    public void Create_NullSizeEstimator_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            HarnessHybridContextPolicy.Create(100, 10, 1, 1, "label", 1, null!));
    }

    [Fact]
    public void Create_AllValidValues_Succeeds()
    {
        var policy = HarnessCompactionTestFixture.CreatePolicy(100, 10, 3, 2, DefaultEstimator);

        Assert.Equal(100, policy.HardLimit);
        Assert.Equal(10, policy.TriggerMargin);
        Assert.Equal(3, policy.RecentMessageRetentionCount);
        Assert.Equal(2, policy.MaximumCompactionAttempts);
        Assert.Equal(HarnessCompactionTestFixture.DefaultPreservationLabel, policy.PreservationLabel);
        Assert.Equal(HarnessCompactionTestFixture.DefaultPreservationVersion, policy.PreservationVersion);
    }

    // --- Exact trigger-threshold boundary: hardLimit - triggerMargin -----------------------

    [Fact]
    public void Evaluate_EstimatedSizeStrictlyBelowThreshold_DoesNotTrigger()
    {
        var sizes = new Dictionary<string, int> { ["entry-1"] = 89 };
        var policy = HarnessCompactionTestFixture.CreatePolicy(
            100, 10, 1, 1, new HarnessFixedSizeContextEstimator(sizes));
        var entries = new[] { HarnessCompactionTestFixture.SystemEntry("entry-1", "system") };

        var evaluation = policy.Evaluate(entries, CancellationToken.None);

        Assert.Equal(90, evaluation.TriggerThreshold);
        Assert.Equal(89, evaluation.EstimatedSize);
        Assert.False(evaluation.Triggered);
    }

    [Fact]
    public void Evaluate_EstimatedSizeExactlyAtThreshold_Triggers()
    {
        var sizes = new Dictionary<string, int> { ["entry-1"] = 90 };
        var policy = HarnessCompactionTestFixture.CreatePolicy(
            100, 10, 1, 1, new HarnessFixedSizeContextEstimator(sizes));
        var entries = new[] { HarnessCompactionTestFixture.SystemEntry("entry-1", "system") };

        var evaluation = policy.Evaluate(entries, CancellationToken.None);

        Assert.Equal(90, evaluation.TriggerThreshold);
        Assert.Equal(90, evaluation.EstimatedSize);
        Assert.True(evaluation.Triggered);
    }

    [Fact]
    public void Evaluate_EstimatedSizeAboveThreshold_Triggers()
    {
        var sizes = new Dictionary<string, int> { ["entry-1"] = 91 };
        var policy = HarnessCompactionTestFixture.CreatePolicy(
            100, 10, 1, 1, new HarnessFixedSizeContextEstimator(sizes));
        var entries = new[] { HarnessCompactionTestFixture.SystemEntry("entry-1", "system") };

        var evaluation = policy.Evaluate(entries, CancellationToken.None);

        Assert.True(evaluation.Triggered);
    }

    [Fact]
    public void Evaluate_SumsFixedSizeAcrossAllEntries()
    {
        var sizes = new Dictionary<string, int> { ["entry-1"] = 30, ["entry-2"] = 40, ["entry-3"] = 20 };
        var policy = HarnessCompactionTestFixture.CreatePolicy(
            100, 10, 1, 1, new HarnessFixedSizeContextEstimator(sizes));
        var entries = new[]
        {
            HarnessCompactionTestFixture.SystemEntry("entry-1", "a"),
            HarnessCompactionTestFixture.AuthoritativeEntry("entry-2", "b"),
            HarnessCompactionTestFixture.ConversationalEntry("entry-3", Microsoft.Extensions.AI.ChatRole.User, "c"),
        };

        var evaluation = policy.Evaluate(entries, CancellationToken.None);

        Assert.Equal(90, evaluation.EstimatedSize);
        Assert.True(evaluation.Triggered);
    }

    // --- Cancellation checked at deterministic boundaries -----------------------------------

    [Fact]
    public void Evaluate_AlreadyCanceledToken_ThrowsOperationCanceledException()
    {
        var policy = HarnessCompactionTestFixture.CreatePolicy(100, 10, 1, 1, DefaultEstimator);
        var entries = new[] { HarnessCompactionTestFixture.SystemEntry("entry-1", "system") };
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.Throws<OperationCanceledException>(() => policy.Evaluate(entries, cts.Token));
    }

    [Fact]
    public void SelectRequiredPreservation_AlreadyCanceledToken_ThrowsOperationCanceledException()
    {
        var policy = HarnessCompactionTestFixture.CreatePolicy(100, 10, 1, 1, DefaultEstimator);
        var entries = new[] { HarnessCompactionTestFixture.SystemEntry("entry-1", "system") };
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.Throws<OperationCanceledException>(() => policy.SelectRequiredPreservation(entries, cts.Token));
    }

    [Fact]
    public void SelectRequiredPreservation_DuplicateEntryIds_ThrowsArgumentException()
    {
        var policy = HarnessCompactionTestFixture.CreatePolicy(100, 10, 1, 1, DefaultEstimator);
        var entries = new[]
        {
            HarnessCompactionTestFixture.SystemEntry("duplicate-id", "first"),
            HarnessCompactionTestFixture.SystemEntry("duplicate-id", "second"),
        };

        Assert.Throws<ArgumentException>(() => policy.SelectRequiredPreservation(entries, CancellationToken.None));
    }

    [Fact]
    public void Evaluate_NullEntries_ThrowsArgumentNullException()
    {
        var policy = HarnessCompactionTestFixture.CreatePolicy(100, 10, 1, 1, DefaultEstimator);

        Assert.Throws<ArgumentNullException>(() => policy.Evaluate(null!, CancellationToken.None));
    }

    // --- Size estimator contract: negative returns rejected, integer sum overflow throws ------

    [Fact]
    public void Evaluate_NegativeEstimate_ThrowsInvalidOperationException()
    {
        var sizes = new Dictionary<string, int> { ["entry-1"] = -1 };
        var policy = HarnessCompactionTestFixture.CreatePolicy(
            100, 10, 1, 1, new HarnessFixedSizeContextEstimator(sizes));
        var entries = new[] { HarnessCompactionTestFixture.SystemEntry("entry-1", "system") };

        Assert.Throws<InvalidOperationException>(() => policy.Evaluate(entries, CancellationToken.None));
    }

    [Fact]
    public void Evaluate_SizeSumOverflowsInt32_ThrowsOverflowException()
    {
        var sizes = new Dictionary<string, int>
        {
            ["entry-1"] = int.MaxValue - 10,
            ["entry-2"] = 11,
        };
        var policy = HarnessCompactionTestFixture.CreatePolicy(
            int.MaxValue, 1, 1, 1, new HarnessFixedSizeContextEstimator(sizes));
        var entries = new[]
        {
            HarnessCompactionTestFixture.SystemEntry("entry-1", "a"),
            HarnessCompactionTestFixture.ConversationalEntry("entry-2", Microsoft.Extensions.AI.ChatRole.User, "b"),
        };

        Assert.Throws<OverflowException>(() => policy.Evaluate(entries, CancellationToken.None));
    }
}
