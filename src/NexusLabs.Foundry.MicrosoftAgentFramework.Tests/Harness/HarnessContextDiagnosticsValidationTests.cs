// Tests intentionally exercise explicit CancellationToken parameters (including
// CancellationToken.None) directly. This is the behavior under test, not an oversight of
// TestContext.Current.CancellationToken.
#pragma warning disable xUnit1051

using System.Reflection;

using Microsoft.Extensions.AI;

using NexusLabs.Foundry.MicrosoftAgentFramework.Context;
using NexusLabs.Foundry.MicrosoftAgentFramework.Diagnostics;
using NexusLabs.Foundry.MicrosoftAgentFramework.Harness;
using NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Context;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Tests.Harness;

/// <summary>
/// Validates the integrity contracts on <see cref="HarnessContextCategoryContribution.Create"/>,
/// <see cref="HarnessContextDiagnostics.ForSuccess"/>/<c>ForTermination</c>, and
/// <see cref="HarnessContextDiagnosticsFactory"/> — including rejection of negative estimator
/// results, checked overflow behavior, unique-category enforcement, and defined-enum-value
/// validation for both <see cref="HarnessContextMeasurementUnit"/> and
/// <see cref="HarnessContextCategory"/>.
/// </summary>
public sealed class HarnessContextDiagnosticsValidationTests
{
    private static readonly IReadOnlyList<HarnessContextAssemblyStageCategory> OneStage =
        new[] { HarnessContextAssemblyStageCategory.SnapshotCaptured };

    // ================================================================================
    // HarnessContextCategoryContribution.Create — invalid value rejection (item 1)
    // ================================================================================

    [Fact]
    public void Create_NegativeSize_ThrowsArgumentOutOfRangeException()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(
            () => HarnessContextCategoryContribution.Create(
                HarnessContextCategory.ConversationalMessage, size: -1, entryCount: 1));
        Assert.Equal("size", ex.ParamName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void Create_NonPositiveEntryCount_ThrowsArgumentOutOfRangeException(int entryCount)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(
            () => HarnessContextCategoryContribution.Create(
                HarnessContextCategory.ConversationalMessage, size: 10, entryCount: entryCount));
        Assert.Equal("entryCount", ex.ParamName);
    }

    [Fact]
    public void Create_ZeroSize_IsValid()
    {
        // Zero size is legal — an entry that estimates to zero bytes/units.
        var contribution = HarnessContextCategoryContribution.Create(
            HarnessContextCategory.Summary, size: 0, entryCount: 1);
        Assert.Equal(0, contribution.Size);
        Assert.Equal(1, contribution.EntryCount);
        Assert.Equal(HarnessContextCategory.Summary, contribution.Category);
    }

    [Fact]
    public void Create_ValidPositiveValues_ProducesCorrectProperties()
    {
        var contribution = HarnessContextCategoryContribution.Create(
            HarnessContextCategory.ToolExchange, size: 42, entryCount: 3);
        Assert.Equal(HarnessContextCategory.ToolExchange, contribution.Category);
        Assert.Equal(42, contribution.Size);
        Assert.Equal(3, contribution.EntryCount);
    }

    [Fact]
    public void Create_UndefinedCategory_ThrowsArgumentOutOfRangeException()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(
            () => HarnessContextCategoryContribution.Create(
                (HarnessContextCategory)999, size: 1, entryCount: 1));
        Assert.Equal("category", ex.ParamName);
    }

    // ================================================================================
    // HarnessContextDiagnosticsFactory.Create — negative estimator result (item 2)
    // ================================================================================

    [Fact]
    public async Task BuildCategoryContributions_NegativeEstimatorResult_ThrowsInvalidOperationException()
    {
        // The negative-estimator makes the assembler see a negative original size (below every
        // trigger threshold), so the assembler returns WithinLimit immediately. The factory
        // then calls BuildCategoryContributions which detects the negative per-entry size and
        // throws InvalidOperationException — never wrapped as a different type.
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, "instructions"),
            new(ChatRole.User, "hello"),
        };
        var policy = HarnessCompactionTestFixture.CreatePolicy(
            10_000, 1, 5, 3, new HarnessNegativeSizeContextEstimator());
        var profile = HarnessHybridProfile.Create(
            policy,
            HarnessScriptedUpstreamChatReducer.Echo(),
            new HarnessScriptedMessageClassifier(),
            baselineEntries => new HarnessMutableContextSnapshotProvider(baselineEntries));

        var accessorCtx = new AgentExecutionContextAccessor();
        var binding = HarnessCompositionTestFixture.CaptureBinding(accessorCtx, out var scope);
        using (scope)
        {
            var leaf = new HarnessCompactionObservingChatClient("unused");
            var client = new HarnessHybridCompactionChatClient(
                leaf, profile, binding, accessorCtx,
                HarnessCompositionTestFixture.SessionId,
                runCoordinator: null, progressAccessor: null);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => client.GetResponseAsync(messages, cancellationToken: CancellationToken.None));
        }
    }

    // ================================================================================
    // HarnessContextDiagnostics.ForSuccess — unique-category enforcement (item 2)
    // ================================================================================

    [Fact]
    public void ForSuccess_DuplicateCategory_ThrowsArgumentException()
    {
        var contributions = new List<HarnessContextCategoryContribution>
        {
            HarnessContextCategoryContribution.Create(HarnessContextCategory.ConversationalMessage, 5, 1),
            HarnessContextCategoryContribution.Create(HarnessContextCategory.ConversationalMessage, 5, 1),
        };

        var ex = Assert.Throws<ArgumentException>(() =>
            HarnessContextDiagnostics.ForSuccess(
                HarnessContextCompactionOutcome.WithinLimit,
                HarnessContextMeasurementUnit.HostDefinedUnits,
                originalSize: 10, finalSize: 10,
                triggerThreshold: 5, hardLimit: 100,
                attemptCount: 1, OneStage, contributions));

        // Error message must identify the offending category.
        Assert.Contains(nameof(HarnessContextCategory.ConversationalMessage), ex.Message);
    }

    // ================================================================================
    // HarnessContextDiagnostics.ForSuccess — undefined category rejected defensively (item 3)
    // ================================================================================

    [Fact]
    public void ForSuccess_ContributionWithUndefinedCategory_ThrowsArgumentOutOfRangeException()
    {
        // HarnessContextCategoryContribution.Create already rejects an undefined category (see
        // Create_UndefinedCategory_ThrowsArgumentOutOfRangeException above), so the only way to
        // present ForSuccess with an otherwise-impossible undefined-category contribution is to
        // bypass Create entirely via the private constructor through reflection. This proves
        // ForSuccess independently defends its own invariant rather than blindly trusting that
        // every contribution it is handed was actually produced by Create.
        var invalidContribution = CreateContributionBypassingFactoryValidation(
            (HarnessContextCategory)999, size: 1, entryCount: 1);

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() =>
            HarnessContextDiagnostics.ForSuccess(
                HarnessContextCompactionOutcome.WithinLimit,
                HarnessContextMeasurementUnit.HostDefinedUnits,
                originalSize: 1, finalSize: 1,
                triggerThreshold: 5, hardLimit: 100,
                attemptCount: 1, OneStage, [invalidContribution]));

        Assert.Equal("categoryContributions", ex.ParamName);
    }

    private static HarnessContextCategoryContribution CreateContributionBypassingFactoryValidation(
        HarnessContextCategory category, int size, int entryCount)
    {
        var constructor = typeof(HarnessContextCategoryContribution).GetConstructor(
            BindingFlags.NonPublic | BindingFlags.Instance,
            binder: null,
            types: [typeof(HarnessContextCategory), typeof(int), typeof(int)],
            modifiers: null)!;
        return (HarnessContextCategoryContribution)constructor.Invoke(
            [category, size, entryCount]);
    }

    // ================================================================================
    // HarnessContextDiagnostics.ForSuccess — checked total overflow (item 2)
    // ================================================================================

    [Fact]
    public void ForSuccess_ContributionSumOverflow_ThrowsOverflowException()
    {
        // Two contributions of different categories each at int.MaxValue: their checked sum
        // overflows. This proves the accumulation uses checked arithmetic and never silently
        // wraps the total.
        var contributions = new List<HarnessContextCategoryContribution>
        {
            HarnessContextCategoryContribution.Create(
                HarnessContextCategory.SystemInstruction, int.MaxValue, 1),
            HarnessContextCategoryContribution.Create(
                HarnessContextCategory.ConversationalMessage, int.MaxValue, 1),
        };

        Assert.Throws<OverflowException>(() =>
            HarnessContextDiagnostics.ForSuccess(
                HarnessContextCompactionOutcome.WithinLimit,
                HarnessContextMeasurementUnit.HostDefinedUnits,
                originalSize: int.MaxValue, finalSize: int.MaxValue,
                triggerThreshold: 1, hardLimit: int.MaxValue,
                attemptCount: 1, OneStage, contributions));
    }

    // ================================================================================
    // HarnessContextDiagnostics.ForSuccess / ForTermination — undefined MeasurementUnit
    // ================================================================================

    [Fact]
    public void ForSuccess_UndefinedMeasurementUnit_ThrowsArgumentOutOfRangeException()
    {
        var contributions = new List<HarnessContextCategoryContribution>
        {
            HarnessContextCategoryContribution.Create(HarnessContextCategory.ConversationalMessage, 0, 1),
        };

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() =>
            HarnessContextDiagnostics.ForSuccess(
                HarnessContextCompactionOutcome.WithinLimit,
                (HarnessContextMeasurementUnit)999,
                originalSize: 0, finalSize: 0,
                triggerThreshold: 5, hardLimit: 100,
                attemptCount: 1, OneStage, contributions));

        Assert.Equal("measurementUnit", ex.ParamName);
    }

    [Fact]
    public void ForTermination_UndefinedMeasurementUnit_ThrowsArgumentOutOfRangeException()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() =>
            HarnessContextDiagnostics.ForTermination(
                HarnessContextCompactionOutcome.Irreducible,
                (HarnessContextMeasurementUnit)999,
                originalSize: 100, finalSize: 100,
                triggerThreshold: 5, hardLimit: 10,
                attemptCount: 1, OneStage));

        Assert.Equal("measurementUnit", ex.ParamName);
    }

    // ================================================================================
    // Private test helper estimators
    // ================================================================================

    /// <summary>
    /// Returns <c>-1</c> for every entry, simulating a misconfigured estimator that violates
    /// the non-negative contract. Used to verify <see cref="HarnessContextDiagnosticsFactory"/>
    /// raises <see cref="InvalidOperationException"/> without wrapping.
    /// </summary>
    private sealed class HarnessNegativeSizeContextEstimator : IHarnessContextSizeEstimator
    {
        public HarnessContextMeasurementUnit MeasurementUnit => HarnessContextMeasurementUnit.HostDefinedUnits;

        public int EstimateSize(HarnessContextEntry entry) => -1;
    }
}
