using NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Bundle;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Tests;

/// <summary>
/// Proves that <see cref="FoundryHarnessAgentFactory.DescribeEffectiveDefaults(FoundryHarnessAgentConfiguration)"/>
/// reports the exact requested-versus-effective disposition matrix for the installed
/// <c>Microsoft.Agents.AI.Harness</c> 1.15.0 bundle: three dimensions are always-on and
/// unavoidable regardless of configuration, nine dimensions track the caller's explicit
/// <see cref="FoundryHarnessFeatureSelections"/> choices, one dimension is opt-in via
/// <see cref="FoundryHarnessAgentConfiguration.FileAccessStore"/>, and two dimensions are not yet
/// exposed by this API candidate and are reported as limitations rather than silently omitted.
/// Also validates that <see cref="FoundryHarnessFeatureDisposition.Create"/> and
/// <see cref="FoundryHarnessEffectiveDefaults.Create"/> enforce their factory invariants.
/// </summary>
public sealed class HarnessBundleDefaultsTests
{
    private static readonly FoundryHarnessAgentFactory Factory = new();

    // ─── Helpers ─────────────────────────────────────────────────────────────

    /// <summary>
    /// A fully-enabled baseline configuration that also supplies the token budgets required
    /// by compaction, making it valid for both <c>Create</c> and <c>DescribeEffectiveDefaults</c>.
    /// </summary>
    private static FoundryHarnessAgentConfiguration AllFeaturesEnabledWithBudgets() =>
        HarnessBundleTestsHelpers.CreateBaseline(HarnessBundleTestsHelpers.AllFeaturesEnabled()) with
        {
            MaxContextWindowTokens = 8_000,
            MaxOutputTokens = 1_000,
        };

    // ─── Always-on dimensions ────────────────────────────────────────────────

    [Theory]
    [InlineData(FoundryHarnessFeature.FunctionInvocation)]
    [InlineData(FoundryHarnessFeature.MessageInjection)]
    [InlineData(FoundryHarnessFeature.HistoryPersistence)]
    public void AlwaysOnDimensions_AreUnavoidableRegardlessOfFeatureSelections(FoundryHarnessFeature feature)
    {
        var disabledConfiguration = HarnessBundleTestsHelpers.CreateBaseline(
            HarnessBundleTestsHelpers.AllFeaturesDisabled());
        var enabledConfiguration = AllFeaturesEnabledWithBudgets();

        var disabledDisposition = Factory.DescribeEffectiveDefaults(disabledConfiguration).GetDisposition(feature);
        var enabledDisposition = Factory.DescribeEffectiveDefaults(enabledConfiguration).GetDisposition(feature);

        foreach (var disposition in new[] { disabledDisposition, enabledDisposition })
        {
            Assert.Equal(FoundryHarnessFeatureRequestedState.NotConfigurable, disposition.RequestedState);
            Assert.Equal(FoundryHarnessFeatureEffectiveState.AlwaysOnUnavoidable, disposition.EffectiveState);
            Assert.False(string.IsNullOrWhiteSpace(disposition.Limitation));
        }
    }

    // ─── Toggle dimensions ───────────────────────────────────────────────────

    [Theory]
    [InlineData(FoundryHarnessFeature.WebSearch)]
    [InlineData(FoundryHarnessFeature.FileMemory)]
    [InlineData(FoundryHarnessFeature.AgentSkills)]
    [InlineData(FoundryHarnessFeature.ToolAutoApproval)]
    [InlineData(FoundryHarnessFeature.ApprovalNotRequiredFunctionBypassing)]
    [InlineData(FoundryHarnessFeature.ApprovalResponseBinding)]
    [InlineData(FoundryHarnessFeature.OpenTelemetry)]
    [InlineData(FoundryHarnessFeature.TodoProvider)]
    [InlineData(FoundryHarnessFeature.AgentModeProvider)]
    public void ToggleDimensions_RequestedEnabled_ReportsEffectiveEnabled(FoundryHarnessFeature feature)
    {
        var configuration = AllFeaturesEnabledWithBudgets();

        var disposition = Factory.DescribeEffectiveDefaults(configuration).GetDisposition(feature);

        Assert.Equal(FoundryHarnessFeatureRequestedState.RequestedEnabled, disposition.RequestedState);
        Assert.Equal(FoundryHarnessFeatureEffectiveState.Enabled, disposition.EffectiveState);
    }

    [Theory]
    [InlineData(FoundryHarnessFeature.WebSearch)]
    [InlineData(FoundryHarnessFeature.FileMemory)]
    [InlineData(FoundryHarnessFeature.AgentSkills)]
    [InlineData(FoundryHarnessFeature.ToolAutoApproval)]
    [InlineData(FoundryHarnessFeature.ApprovalNotRequiredFunctionBypassing)]
    [InlineData(FoundryHarnessFeature.ApprovalResponseBinding)]
    [InlineData(FoundryHarnessFeature.OpenTelemetry)]
    [InlineData(FoundryHarnessFeature.TodoProvider)]
    [InlineData(FoundryHarnessFeature.AgentModeProvider)]
    public void ToggleDimensions_RequestedDisabled_ReportsEffectiveDisabled(FoundryHarnessFeature feature)
    {
        var configuration = HarnessBundleTestsHelpers.CreateBaseline(
            HarnessBundleTestsHelpers.AllFeaturesDisabled());

        var disposition = Factory.DescribeEffectiveDefaults(configuration).GetDisposition(feature);

        Assert.Equal(FoundryHarnessFeatureRequestedState.RequestedDisabled, disposition.RequestedState);
        Assert.Equal(FoundryHarnessFeatureEffectiveState.Disabled, disposition.EffectiveState);
    }

    // ─── Compaction dimension ─────────────────────────────────────────────────

    [Fact]
    public void Compaction_RequestedEnabledWithBothTokenBudgets_ReportsEffectiveEnabled()
    {
        var configuration = AllFeaturesEnabledWithBudgets();

        var disposition = Factory.DescribeEffectiveDefaults(configuration)
            .GetDisposition(FoundryHarnessFeature.Compaction);

        Assert.Equal(FoundryHarnessFeatureRequestedState.RequestedEnabled, disposition.RequestedState);
        Assert.Equal(FoundryHarnessFeatureEffectiveState.Enabled, disposition.EffectiveState);
    }

    // ─── FileAccess dimension ─────────────────────────────────────────────────

    [Fact]
    public void FileAccess_NoStoreSupplied_ReportsNotRequestedAndDisabled()
    {
        var configuration = HarnessBundleTestsHelpers.CreateBaseline();

        var disposition = Factory.DescribeEffectiveDefaults(configuration)
            .GetDisposition(FoundryHarnessFeature.FileAccess);

        Assert.Equal(FoundryHarnessFeatureRequestedState.NotRequested, disposition.RequestedState);
        Assert.Equal(FoundryHarnessFeatureEffectiveState.Disabled, disposition.EffectiveState);
    }

    [Fact]
    public void FileAccess_StoreSupplied_ReportsRequestedEnabledAndEffectiveEnabled()
    {
        var configuration = HarnessBundleTestsHelpers.CreateBaseline() with
        {
            FileAccessStore = new InMemoryAgentFileStoreFake(),
        };

        var disposition = Factory.DescribeEffectiveDefaults(configuration)
            .GetDisposition(FoundryHarnessFeature.FileAccess);

        Assert.Equal(FoundryHarnessFeatureRequestedState.RequestedEnabled, disposition.RequestedState);
        Assert.Equal(FoundryHarnessFeatureEffectiveState.Enabled, disposition.EffectiveState);
    }

    // ─── Out-of-scope dimensions ──────────────────────────────────────────────

    [Theory]
    [InlineData(FoundryHarnessFeature.BackgroundAgents)]
    [InlineData(FoundryHarnessFeature.LoopEvaluation)]
    public void OutOfScopeDimensions_AlwaysReportNotRequestedDisabledWithLimitation(FoundryHarnessFeature feature)
    {
        var configuration = AllFeaturesEnabledWithBudgets();

        var disposition = Factory.DescribeEffectiveDefaults(configuration).GetDisposition(feature);

        Assert.Equal(FoundryHarnessFeatureRequestedState.NotRequested, disposition.RequestedState);
        Assert.Equal(FoundryHarnessFeatureEffectiveState.Disabled, disposition.EffectiveState);
        Assert.False(string.IsNullOrWhiteSpace(disposition.Limitation));
    }

    // ─── Report completeness ──────────────────────────────────────────────────

    [Fact]
    public void EffectiveDefaults_CoversEveryFoundryHarnessFeatureExactlyOnce()
    {
        var configuration = HarnessBundleTestsHelpers.CreateBaseline();

        var dispositions = Factory.DescribeEffectiveDefaults(configuration).Dispositions;

        var allFeatures = Enum.GetValues<FoundryHarnessFeature>();
        Assert.Equal(allFeatures.Length, dispositions.Count);
        Assert.Equal(allFeatures.ToHashSet(), dispositions.Select(disposition => disposition.Feature).ToHashSet());
    }

    [Fact]
    public void EffectiveDefaults_Dispositions_IsDefensiveCopy()
    {
        // Verify the returned Dispositions list cannot be mutated from outside.
        // IReadOnlyList does not expose mutation, but the underlying object should
        // be a read-only collection rather than a mutable list.
        var configuration = HarnessBundleTestsHelpers.CreateBaseline();

        var defaults = Factory.DescribeEffectiveDefaults(configuration);

        Assert.IsNotType<List<FoundryHarnessFeatureDisposition>>(defaults.Dispositions);
    }

    // ─── FoundryHarnessEffectiveDefaults.Create factory invariants (internal) ──

    [Fact]
    internal void EffectiveDefaultsCreate_DuplicateFeature_ThrowsArgumentException()
    {
        var existing = Factory.DescribeEffectiveDefaults(HarnessBundleTestsHelpers.CreateBaseline());
        var dispositions = existing.Dispositions.ToList();

        // Add a duplicate entry for the first feature
        dispositions.Add(dispositions[0]);

        Assert.Throws<ArgumentException>(
            () => FoundryHarnessEffectiveDefaults.Create(dispositions));
    }

    [Fact]
    internal void EffectiveDefaultsCreate_MissingFeature_ThrowsArgumentException()
    {
        var existing = Factory.DescribeEffectiveDefaults(HarnessBundleTestsHelpers.CreateBaseline());
        // Drop the last disposition so one feature is missing.
        var dispositions = existing.Dispositions.Take(existing.Dispositions.Count - 1).ToList();

        Assert.Throws<ArgumentException>(
            () => FoundryHarnessEffectiveDefaults.Create(dispositions));
    }

    [Fact]
    internal void EffectiveDefaultsCreate_NullList_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(
            () => FoundryHarnessEffectiveDefaults.Create(null!));
    }

    // ─── FoundryHarnessFeatureDisposition.Create factory invariants (internal) ──

    [Fact]
    internal void DispositionCreate_AlwaysOnUnavoidableWithoutLimitation_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            FoundryHarnessFeatureDisposition.Create(
                FoundryHarnessFeature.FunctionInvocation,
                FoundryHarnessFeatureRequestedState.NotConfigurable,
                FoundryHarnessFeatureEffectiveState.AlwaysOnUnavoidable,
                limitation: null));
    }

    [Fact]
    internal void DispositionCreate_AlwaysOnUnavoidableWithWhitespaceLimitation_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            FoundryHarnessFeatureDisposition.Create(
                FoundryHarnessFeature.FunctionInvocation,
                FoundryHarnessFeatureRequestedState.NotConfigurable,
                FoundryHarnessFeatureEffectiveState.AlwaysOnUnavoidable,
                limitation: "   "));
    }

    [Fact]
    internal void DispositionCreate_NotConfigurableWithoutAlwaysOnUnavoidable_ThrowsArgumentException()
    {
        // NotConfigurable ↔ AlwaysOnUnavoidable must co-occur
        Assert.Throws<ArgumentException>(() =>
            FoundryHarnessFeatureDisposition.Create(
                FoundryHarnessFeature.FunctionInvocation,
                FoundryHarnessFeatureRequestedState.NotConfigurable,
                FoundryHarnessFeatureEffectiveState.Enabled,
                limitation: "some limitation"));
    }

    [Fact]
    internal void DispositionCreate_AlwaysOnUnavoidableWithoutNotConfigurable_ThrowsArgumentException()
    {
        // AlwaysOnUnavoidable ↔ NotConfigurable must co-occur
        Assert.Throws<ArgumentException>(() =>
            FoundryHarnessFeatureDisposition.Create(
                FoundryHarnessFeature.FunctionInvocation,
                FoundryHarnessFeatureRequestedState.RequestedEnabled,
                FoundryHarnessFeatureEffectiveState.AlwaysOnUnavoidable,
                limitation: "some limitation"));
    }

    [Fact]
    internal void DispositionCreate_NonNullWhitespaceLimitation_ThrowsArgumentException()
    {
        // Non-null limitation must not be whitespace-only
        Assert.Throws<ArgumentException>(() =>
            FoundryHarnessFeatureDisposition.Create(
                FoundryHarnessFeature.WebSearch,
                FoundryHarnessFeatureRequestedState.RequestedEnabled,
                FoundryHarnessFeatureEffectiveState.Enabled,
                limitation: "   "));
    }

    [Fact]
    internal void DispositionCreate_ValidToggleEnabled_ReturnsDisposition()
    {
        var disposition = FoundryHarnessFeatureDisposition.Create(
            FoundryHarnessFeature.WebSearch,
            FoundryHarnessFeatureRequestedState.RequestedEnabled,
            FoundryHarnessFeatureEffectiveState.Enabled,
            null);

        Assert.Equal(FoundryHarnessFeature.WebSearch, disposition.Feature);
        Assert.Equal(FoundryHarnessFeatureRequestedState.RequestedEnabled, disposition.RequestedState);
        Assert.Equal(FoundryHarnessFeatureEffectiveState.Enabled, disposition.EffectiveState);
        Assert.Null(disposition.Limitation);
    }

    [Fact]
    internal void DispositionCreate_ValidAlwaysOnWithLimitation_ReturnsDisposition()
    {
        const string limitation = "This dimension is always on.";

        var disposition = FoundryHarnessFeatureDisposition.Create(
            FoundryHarnessFeature.FunctionInvocation,
            FoundryHarnessFeatureRequestedState.NotConfigurable,
            FoundryHarnessFeatureEffectiveState.AlwaysOnUnavoidable,
            limitation);

        Assert.Equal(FoundryHarnessFeatureEffectiveState.AlwaysOnUnavoidable, disposition.EffectiveState);
        Assert.Equal(limitation, disposition.Limitation);
    }

    [Fact]
    internal void DispositionCreate_ValidNotExposedWithLimitation_ReturnsDisposition()
    {
        // NotExposed pattern: NotRequested + Disabled + non-null limitation
        const string limitation = "Not exposed in this API candidate.";

        var disposition = FoundryHarnessFeatureDisposition.Create(
            FoundryHarnessFeature.BackgroundAgents,
            FoundryHarnessFeatureRequestedState.NotRequested,
            FoundryHarnessFeatureEffectiveState.Disabled,
            limitation);

        Assert.Equal(FoundryHarnessFeatureRequestedState.NotRequested, disposition.RequestedState);
        Assert.Equal(FoundryHarnessFeatureEffectiveState.Disabled, disposition.EffectiveState);
        Assert.Equal(limitation, disposition.Limitation);
    }
}
