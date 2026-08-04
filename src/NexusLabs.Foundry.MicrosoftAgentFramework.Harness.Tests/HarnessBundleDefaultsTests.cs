using NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Bundle;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Tests;

/// <summary>
/// Proves that <see cref="FoundryHarnessAgentFactory.DescribeEffectiveDefaults(FoundryHarnessAgentConfiguration)"/>
/// reports the exact requested-versus-effective disposition matrix (including the
/// requested/effective axis and the separate backing-selection axis) for the installed
/// <c>Microsoft.Agents.AI.Harness</c> 1.16.0 bundle: always-on-unavoidable dimensions, toggle
/// dimensions tracking <see cref="FoundryHarnessFeatureSelections"/>, opt-in dimensions driven by
/// backing-object presence, and dimensions not yet exposed by this API candidate reported as
/// limitations rather than silently omitted. Also validates that
/// <see cref="FoundryHarnessFeatureDisposition.Create"/> and
/// <see cref="FoundryHarnessEffectiveDefaults.Create"/> enforce their factory invariants.
/// </summary>
public sealed class HarnessBundleDefaultsTests
{
    private static readonly FoundryHarnessAgentFactory Factory = new();

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

    [Theory]
    [InlineData(FoundryHarnessFeature.FunctionInvocation)]
    [InlineData(FoundryHarnessFeature.MessageInjection)]
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
            Assert.Equal(FoundryHarnessFeatureBackingSelection.NotApplicable, disposition.BackingSelection);
            Assert.Null(disposition.BackingDescription);
        }
    }

    [Fact]
    public void HistoryPersistence_NoProviderSupplied_ReportsUpstreamDefaultBacking()
    {
        var configuration = HarnessBundleTestsHelpers.CreateBaseline();

        var disposition = Factory.DescribeEffectiveDefaults(configuration)
            .GetDisposition(FoundryHarnessFeature.HistoryPersistence);

        Assert.Equal(FoundryHarnessFeatureRequestedState.NotConfigurable, disposition.RequestedState);
        Assert.Equal(FoundryHarnessFeatureEffectiveState.AlwaysOnUnavoidable, disposition.EffectiveState);
        Assert.Equal(FoundryHarnessFeatureBackingSelection.UpstreamDefault, disposition.BackingSelection);
        Assert.False(string.IsNullOrWhiteSpace(disposition.BackingDescription));
    }

    [Fact]
    public void HistoryPersistence_ProviderSupplied_ReportsCallerSuppliedBacking()
    {
        var configuration = HarnessBundleTestsHelpers.CreateBaseline() with
        {
            ChatHistoryProvider = new FakeChatHistoryProvider(),
        };

        var disposition = Factory.DescribeEffectiveDefaults(configuration)
            .GetDisposition(FoundryHarnessFeature.HistoryPersistence);

        Assert.Equal(FoundryHarnessFeatureBackingSelection.CallerSupplied, disposition.BackingSelection);
        Assert.False(string.IsNullOrWhiteSpace(disposition.BackingDescription));
    }

    [Fact]
    public void HistoryPersistence_CompactionEnabledWithBothBudgets_ReportsReducerInBackingDescription()
    {
        var configuration = AllFeaturesEnabledWithBudgets();

        var disposition = Factory.DescribeEffectiveDefaults(configuration)
            .GetDisposition(FoundryHarnessFeature.HistoryPersistence);

        Assert.Equal(FoundryHarnessFeatureBackingSelection.UpstreamDefault, disposition.BackingSelection);
        Assert.Contains("reducer", disposition.BackingDescription!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void HistoryPersistence_CompactionEnabledWithExplicitStrategy_ReportsReducerInBackingDescription()
    {
        var configuration = HarnessBundleTestsHelpers.CreateBaseline(
            HarnessBundleTestsHelpers.AllFeaturesDisabled() with { EnableCompaction = true }) with
        {
            CompactionStrategy = new Microsoft.Agents.AI.Compaction.ContextWindowCompactionStrategy(
                8_000, 1_000),
        };

        var disposition = Factory.DescribeEffectiveDefaults(configuration)
            .GetDisposition(FoundryHarnessFeature.HistoryPersistence);

        Assert.Equal(FoundryHarnessFeatureBackingSelection.UpstreamDefault, disposition.BackingSelection);
        Assert.Contains("reducer", disposition.BackingDescription!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("caller-supplied", disposition.BackingDescription!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void HistoryPersistence_CompactionDisabledWithOutputOnlyBudget_ReportsNoReducerInBackingDescription()
    {
        var configuration = HarnessBundleTestsHelpers.CreateBaseline(
            HarnessBundleTestsHelpers.AllFeaturesDisabled()) with
        {
            MaxOutputTokens = 1_000,
        };

        var disposition = Factory.DescribeEffectiveDefaults(configuration)
            .GetDisposition(FoundryHarnessFeature.HistoryPersistence);

        Assert.Equal(FoundryHarnessFeatureBackingSelection.UpstreamDefault, disposition.BackingSelection);
        Assert.DoesNotContain("reducer", disposition.BackingDescription!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void HarnessInstructions_Null_ReportsNotRequestedEnabledWithUpstreamDefaultBacking()
    {
        var configuration = HarnessBundleTestsHelpers.CreateBaseline();

        var disposition = Factory.DescribeEffectiveDefaults(configuration)
            .GetDisposition(FoundryHarnessFeature.HarnessInstructions);

        Assert.Equal(FoundryHarnessFeatureRequestedState.NotRequested, disposition.RequestedState);
        Assert.Equal(FoundryHarnessFeatureEffectiveState.Enabled, disposition.EffectiveState);
        Assert.Equal(FoundryHarnessFeatureBackingSelection.UpstreamDefault, disposition.BackingSelection);
        Assert.False(string.IsNullOrWhiteSpace(disposition.BackingDescription));
    }

    [Fact]
    public void HarnessInstructions_EmptyString_ReportsRequestedDisabledAndDisabled()
    {
        var configuration = HarnessBundleTestsHelpers.CreateBaseline() with { HarnessInstructionsOverride = "" };

        var disposition = Factory.DescribeEffectiveDefaults(configuration)
            .GetDisposition(FoundryHarnessFeature.HarnessInstructions);

        Assert.Equal(FoundryHarnessFeatureRequestedState.RequestedDisabled, disposition.RequestedState);
        Assert.Equal(FoundryHarnessFeatureEffectiveState.Disabled, disposition.EffectiveState);
        Assert.Equal(FoundryHarnessFeatureBackingSelection.NotApplicable, disposition.BackingSelection);
        Assert.Null(disposition.BackingDescription);
    }

    [Fact]
    public void HarnessInstructions_NonEmptyOverride_ReportsRequestedEnabledWithCallerSuppliedBacking()
    {
        var configuration = HarnessBundleTestsHelpers.CreateBaseline() with
        {
            HarnessInstructionsOverride = "Custom harness instructions.",
        };

        var disposition = Factory.DescribeEffectiveDefaults(configuration)
            .GetDisposition(FoundryHarnessFeature.HarnessInstructions);

        Assert.Equal(FoundryHarnessFeatureRequestedState.RequestedEnabled, disposition.RequestedState);
        Assert.Equal(FoundryHarnessFeatureEffectiveState.Enabled, disposition.EffectiveState);
        Assert.Equal(FoundryHarnessFeatureBackingSelection.CallerSupplied, disposition.BackingSelection);
        Assert.False(string.IsNullOrWhiteSpace(disposition.BackingDescription));
    }

    [Theory]
    [InlineData(FoundryHarnessFeature.WebSearch)]
    [InlineData(FoundryHarnessFeature.ApprovalNotRequiredFunctionBypassing)]
    [InlineData(FoundryHarnessFeature.ApprovalResponseBinding)]
    [InlineData(FoundryHarnessFeature.TodoProvider)]
    public void ToggleDimensions_RequestedEnabled_ReportsEffectiveEnabled(FoundryHarnessFeature feature)
    {
        var configuration = AllFeaturesEnabledWithBudgets();

        var disposition = Factory.DescribeEffectiveDefaults(configuration).GetDisposition(feature);

        Assert.Equal(FoundryHarnessFeatureRequestedState.RequestedEnabled, disposition.RequestedState);
        Assert.Equal(FoundryHarnessFeatureEffectiveState.Enabled, disposition.EffectiveState);
        Assert.Equal(FoundryHarnessFeatureBackingSelection.NotApplicable, disposition.BackingSelection);
    }

    [Theory]
    [InlineData(FoundryHarnessFeature.WebSearch)]
    [InlineData(FoundryHarnessFeature.ApprovalNotRequiredFunctionBypassing)]
    [InlineData(FoundryHarnessFeature.ApprovalResponseBinding)]
    [InlineData(FoundryHarnessFeature.TodoProvider)]
    public void ToggleDimensions_RequestedDisabled_ReportsEffectiveDisabled(FoundryHarnessFeature feature)
    {
        var configuration = HarnessBundleTestsHelpers.CreateBaseline(
            HarnessBundleTestsHelpers.AllFeaturesDisabled());

        var disposition = Factory.DescribeEffectiveDefaults(configuration).GetDisposition(feature);

        Assert.Equal(FoundryHarnessFeatureRequestedState.RequestedDisabled, disposition.RequestedState);
        Assert.Equal(FoundryHarnessFeatureEffectiveState.Disabled, disposition.EffectiveState);
    }

    [Theory]
    [InlineData(FoundryHarnessFeature.FileMemory)]
    [InlineData(FoundryHarnessFeature.AgentSkills)]
    [InlineData(FoundryHarnessFeature.ToolAutoApproval)]
    [InlineData(FoundryHarnessFeature.AgentModeProvider)]
    [InlineData(FoundryHarnessFeature.OpenTelemetry)]
    [InlineData(FoundryHarnessFeature.Compaction)]
    public void ToggleWithBackingDimensions_Disabled_ReportsNotApplicableBacking(FoundryHarnessFeature feature)
    {
        var configuration = HarnessBundleTestsHelpers.CreateBaseline(
            HarnessBundleTestsHelpers.AllFeaturesDisabled());

        var disposition = Factory.DescribeEffectiveDefaults(configuration).GetDisposition(feature);

        Assert.Equal(FoundryHarnessFeatureRequestedState.RequestedDisabled, disposition.RequestedState);
        Assert.Equal(FoundryHarnessFeatureEffectiveState.Disabled, disposition.EffectiveState);
        Assert.Equal(FoundryHarnessFeatureBackingSelection.NotApplicable, disposition.BackingSelection);
        Assert.Null(disposition.BackingDescription);
    }

    [Theory]
    [InlineData(FoundryHarnessFeature.FileMemory)]
    [InlineData(FoundryHarnessFeature.AgentSkills)]
    [InlineData(FoundryHarnessFeature.ToolAutoApproval)]
    [InlineData(FoundryHarnessFeature.AgentModeProvider)]
    [InlineData(FoundryHarnessFeature.OpenTelemetry)]
    public void ToggleWithBackingDimensions_EnabledWithoutBackingObject_ReportsUpstreamDefaultBacking(
        FoundryHarnessFeature feature)
    {
        var configuration = HarnessBundleTestsHelpers.CreateBaseline(
            HarnessBundleTestsHelpers.AllFeaturesEnabled() with { EnableCompaction = false });

        var disposition = Factory.DescribeEffectiveDefaults(configuration).GetDisposition(feature);

        Assert.Equal(FoundryHarnessFeatureRequestedState.RequestedEnabled, disposition.RequestedState);
        Assert.Equal(FoundryHarnessFeatureEffectiveState.Enabled, disposition.EffectiveState);
        Assert.Equal(FoundryHarnessFeatureBackingSelection.UpstreamDefault, disposition.BackingSelection);
        Assert.False(string.IsNullOrWhiteSpace(disposition.BackingDescription));
    }

    [Fact]
    public void FileMemory_EnabledWithStoreSupplied_ReportsCallerSuppliedBacking()
    {
        var configuration = HarnessBundleTestsHelpers.CreateBaseline(
            HarnessBundleTestsHelpers.AllFeaturesDisabled() with { EnableFileMemory = true }) with
        {
            FileMemoryStore = new InMemoryAgentFileStoreFake(),
        };

        var disposition = Factory.DescribeEffectiveDefaults(configuration)
            .GetDisposition(FoundryHarnessFeature.FileMemory);

        Assert.Equal(FoundryHarnessFeatureBackingSelection.CallerSupplied, disposition.BackingSelection);
    }

    [Fact]
    public void AgentSkills_EnabledWithSourceSupplied_ReportsCallerSuppliedBacking()
    {
        var configuration = HarnessBundleTestsHelpers.CreateBaseline(
            HarnessBundleTestsHelpers.AllFeaturesDisabled() with { EnableAgentSkills = true }) with
        {
            AgentSkillsSource = new FakeAgentSkillsSource(),
        };

        var disposition = Factory.DescribeEffectiveDefaults(configuration)
            .GetDisposition(FoundryHarnessFeature.AgentSkills);

        Assert.Equal(FoundryHarnessFeatureBackingSelection.CallerSupplied, disposition.BackingSelection);
    }

    [Fact]
    public void ToolAutoApproval_EnabledWithOptionsSupplied_ReportsCallerSuppliedBacking()
    {
        var configuration = HarnessBundleTestsHelpers.CreateBaseline(
            HarnessBundleTestsHelpers.AllFeaturesDisabled() with { EnableToolAutoApproval = true }) with
        {
            ToolApprovalAgentOptions = new(),
        };

        var disposition = Factory.DescribeEffectiveDefaults(configuration)
            .GetDisposition(FoundryHarnessFeature.ToolAutoApproval);

        Assert.Equal(FoundryHarnessFeatureBackingSelection.CallerSupplied, disposition.BackingSelection);
    }

    [Fact]
    public void AgentModeProvider_EnabledWithOptionsSupplied_ReportsCallerSuppliedBacking()
    {
        var configuration = HarnessBundleTestsHelpers.CreateBaseline(
            HarnessBundleTestsHelpers.AllFeaturesDisabled() with { EnableAgentModeProvider = true }) with
        {
            AgentModeProviderOptions = new(),
        };

        var disposition = Factory.DescribeEffectiveDefaults(configuration)
            .GetDisposition(FoundryHarnessFeature.AgentModeProvider);

        Assert.Equal(FoundryHarnessFeatureBackingSelection.CallerSupplied, disposition.BackingSelection);
    }

    [Fact]
    public void OpenTelemetry_EnabledWithSourceNameSupplied_ReportsCallerSuppliedBacking()
    {
        var configuration = HarnessBundleTestsHelpers.CreateBaseline(
            HarnessBundleTestsHelpers.AllFeaturesDisabled() with { EnableOpenTelemetry = true }) with
        {
            OpenTelemetrySourceName = "custom-source",
        };

        var disposition = Factory.DescribeEffectiveDefaults(configuration)
            .GetDisposition(FoundryHarnessFeature.OpenTelemetry);

        Assert.Equal(FoundryHarnessFeatureBackingSelection.CallerSupplied, disposition.BackingSelection);
        Assert.Contains("custom-source", disposition.BackingDescription, StringComparison.Ordinal);
    }

    [Fact]
    public void Compaction_RequestedEnabledWithBothTokenBudgets_ReportsEffectiveEnabled()
    {
        var configuration = AllFeaturesEnabledWithBudgets();

        var disposition = Factory.DescribeEffectiveDefaults(configuration)
            .GetDisposition(FoundryHarnessFeature.Compaction);

        Assert.Equal(FoundryHarnessFeatureRequestedState.RequestedEnabled, disposition.RequestedState);
        Assert.Equal(FoundryHarnessFeatureEffectiveState.Enabled, disposition.EffectiveState);
        Assert.Equal(FoundryHarnessFeatureBackingSelection.UpstreamDefault, disposition.BackingSelection);
    }

    [Fact]
    public void Compaction_RequestedEnabledWithExplicitStrategy_ReportsCallerSuppliedBacking()
    {
        var configuration = HarnessBundleTestsHelpers.CreateBaseline(
            HarnessBundleTestsHelpers.AllFeaturesDisabled() with { EnableCompaction = true }) with
        {
            CompactionStrategy = new Microsoft.Agents.AI.Compaction.ContextWindowCompactionStrategy(
                8_000, 1_000),
        };

        var disposition = Factory.DescribeEffectiveDefaults(configuration)
            .GetDisposition(FoundryHarnessFeature.Compaction);

        Assert.Equal(FoundryHarnessFeatureBackingSelection.CallerSupplied, disposition.BackingSelection);
    }

    [Fact]
    public void FileAccess_NoStoreSupplied_ReportsNotRequestedAndDisabled()
    {
        var configuration = HarnessBundleTestsHelpers.CreateBaseline();

        var disposition = Factory.DescribeEffectiveDefaults(configuration)
            .GetDisposition(FoundryHarnessFeature.FileAccess);

        Assert.Equal(FoundryHarnessFeatureRequestedState.NotRequested, disposition.RequestedState);
        Assert.Equal(FoundryHarnessFeatureEffectiveState.Disabled, disposition.EffectiveState);
        Assert.Equal(FoundryHarnessFeatureBackingSelection.NotApplicable, disposition.BackingSelection);
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
        Assert.Equal(FoundryHarnessFeatureBackingSelection.CallerSupplied, disposition.BackingSelection);
    }

    [Fact]
    public void AdditionalContextProviders_Empty_ReportsNotRequestedAndDisabled()
    {
        var configuration = HarnessBundleTestsHelpers.CreateBaseline();

        var disposition = Factory.DescribeEffectiveDefaults(configuration)
            .GetDisposition(FoundryHarnessFeature.AdditionalContextProviders);

        Assert.Equal(FoundryHarnessFeatureRequestedState.NotRequested, disposition.RequestedState);
        Assert.Equal(FoundryHarnessFeatureEffectiveState.Disabled, disposition.EffectiveState);
        Assert.Equal(FoundryHarnessFeatureBackingSelection.NotApplicable, disposition.BackingSelection);
    }

    [Fact]
    public void AdditionalContextProviders_NonEmpty_ReportsRequestedEnabledWithCallerSuppliedBacking()
    {
        var configuration = HarnessBundleTestsHelpers.CreateBaseline() with
        {
            AdditionalContextProviders = [new FakeAIContextProvider()],
        };

        var disposition = Factory.DescribeEffectiveDefaults(configuration)
            .GetDisposition(FoundryHarnessFeature.AdditionalContextProviders);

        Assert.Equal(FoundryHarnessFeatureRequestedState.RequestedEnabled, disposition.RequestedState);
        Assert.Equal(FoundryHarnessFeatureEffectiveState.Enabled, disposition.EffectiveState);
        Assert.Equal(FoundryHarnessFeatureBackingSelection.CallerSupplied, disposition.BackingSelection);
        Assert.Contains("1", disposition.BackingDescription, StringComparison.Ordinal);
    }

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
        Assert.Equal(FoundryHarnessFeatureBackingSelection.NotApplicable, disposition.BackingSelection);
    }

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
        var configuration = HarnessBundleTestsHelpers.CreateBaseline();

        var defaults = Factory.DescribeEffectiveDefaults(configuration);

        Assert.IsNotType<List<FoundryHarnessFeatureDisposition>>(defaults.Dispositions);
    }

    [Fact]
    internal void EffectiveDefaultsCreate_DuplicateFeature_ThrowsArgumentException()
    {
        var existing = Factory.DescribeEffectiveDefaults(HarnessBundleTestsHelpers.CreateBaseline());
        var dispositions = existing.Dispositions.ToList();

        dispositions.Add(dispositions[0]);

        Assert.Throws<ArgumentException>(
            () => FoundryHarnessEffectiveDefaults.Create(dispositions));
    }

    [Fact]
    internal void EffectiveDefaultsCreate_MissingFeature_ThrowsArgumentException()
    {
        var existing = Factory.DescribeEffectiveDefaults(HarnessBundleTestsHelpers.CreateBaseline());
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

    [Fact]
    internal void DispositionCreate_AlwaysOnUnavoidableWithoutLimitation_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            FoundryHarnessFeatureDisposition.Create(
                FoundryHarnessFeature.FunctionInvocation,
                FoundryHarnessFeatureRequestedState.NotConfigurable,
                FoundryHarnessFeatureEffectiveState.AlwaysOnUnavoidable,
                limitation: null,
                FoundryHarnessFeatureBackingSelection.NotApplicable,
                backingDescription: null));
    }

    [Fact]
    internal void DispositionCreate_AlwaysOnUnavoidableWithWhitespaceLimitation_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            FoundryHarnessFeatureDisposition.Create(
                FoundryHarnessFeature.FunctionInvocation,
                FoundryHarnessFeatureRequestedState.NotConfigurable,
                FoundryHarnessFeatureEffectiveState.AlwaysOnUnavoidable,
                limitation: "   ",
                FoundryHarnessFeatureBackingSelection.NotApplicable,
                backingDescription: null));
    }

    [Fact]
    internal void DispositionCreate_NotConfigurableWithoutAlwaysOnUnavoidable_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            FoundryHarnessFeatureDisposition.Create(
                FoundryHarnessFeature.FunctionInvocation,
                FoundryHarnessFeatureRequestedState.NotConfigurable,
                FoundryHarnessFeatureEffectiveState.Enabled,
                limitation: "some limitation",
                FoundryHarnessFeatureBackingSelection.NotApplicable,
                backingDescription: null));
    }

    [Fact]
    internal void DispositionCreate_AlwaysOnUnavoidableWithoutNotConfigurable_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            FoundryHarnessFeatureDisposition.Create(
                FoundryHarnessFeature.FunctionInvocation,
                FoundryHarnessFeatureRequestedState.RequestedEnabled,
                FoundryHarnessFeatureEffectiveState.AlwaysOnUnavoidable,
                limitation: "some limitation",
                FoundryHarnessFeatureBackingSelection.NotApplicable,
                backingDescription: null));
    }

    [Fact]
    internal void DispositionCreate_NonNullWhitespaceLimitation_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            FoundryHarnessFeatureDisposition.Create(
                FoundryHarnessFeature.WebSearch,
                FoundryHarnessFeatureRequestedState.RequestedEnabled,
                FoundryHarnessFeatureEffectiveState.Enabled,
                limitation: "   ",
                FoundryHarnessFeatureBackingSelection.NotApplicable,
                backingDescription: null));
    }

    [Fact]
    internal void DispositionCreate_BackingApplicableWithoutBackingDescription_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            FoundryHarnessFeatureDisposition.Create(
                FoundryHarnessFeature.FileMemory,
                FoundryHarnessFeatureRequestedState.RequestedEnabled,
                FoundryHarnessFeatureEffectiveState.Enabled,
                limitation: null,
                FoundryHarnessFeatureBackingSelection.UpstreamDefault,
                backingDescription: null));
    }

    [Fact]
    internal void DispositionCreate_BackingApplicableWithWhitespaceBackingDescription_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            FoundryHarnessFeatureDisposition.Create(
                FoundryHarnessFeature.FileMemory,
                FoundryHarnessFeatureRequestedState.RequestedEnabled,
                FoundryHarnessFeatureEffectiveState.Enabled,
                limitation: null,
                FoundryHarnessFeatureBackingSelection.CallerSupplied,
                backingDescription: "   "));
    }

    [Fact]
    internal void DispositionCreate_NotApplicableBackingWithNonNullBackingDescription_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            FoundryHarnessFeatureDisposition.Create(
                FoundryHarnessFeature.WebSearch,
                FoundryHarnessFeatureRequestedState.RequestedEnabled,
                FoundryHarnessFeatureEffectiveState.Enabled,
                limitation: null,
                FoundryHarnessFeatureBackingSelection.NotApplicable,
                backingDescription: "unexpected"));
    }

    [Fact]
    internal void DispositionCreate_ValidToggleEnabled_ReturnsDisposition()
    {
        var disposition = FoundryHarnessFeatureDisposition.Create(
            FoundryHarnessFeature.WebSearch,
            FoundryHarnessFeatureRequestedState.RequestedEnabled,
            FoundryHarnessFeatureEffectiveState.Enabled,
            limitation: null,
            FoundryHarnessFeatureBackingSelection.NotApplicable,
            backingDescription: null);

        Assert.Equal(FoundryHarnessFeature.WebSearch, disposition.Feature);
        Assert.Equal(FoundryHarnessFeatureRequestedState.RequestedEnabled, disposition.RequestedState);
        Assert.Equal(FoundryHarnessFeatureEffectiveState.Enabled, disposition.EffectiveState);
        Assert.Null(disposition.Limitation);
        Assert.Equal(FoundryHarnessFeatureBackingSelection.NotApplicable, disposition.BackingSelection);
        Assert.Null(disposition.BackingDescription);
    }

    [Fact]
    internal void DispositionCreate_ValidAlwaysOnWithLimitation_ReturnsDisposition()
    {
        const string limitation = "This dimension is always on.";

        var disposition = FoundryHarnessFeatureDisposition.Create(
            FoundryHarnessFeature.FunctionInvocation,
            FoundryHarnessFeatureRequestedState.NotConfigurable,
            FoundryHarnessFeatureEffectiveState.AlwaysOnUnavoidable,
            limitation,
            FoundryHarnessFeatureBackingSelection.NotApplicable,
            backingDescription: null);

        Assert.Equal(FoundryHarnessFeatureEffectiveState.AlwaysOnUnavoidable, disposition.EffectiveState);
        Assert.Equal(limitation, disposition.Limitation);
    }

    [Fact]
    internal void DispositionCreate_ValidNotExposedWithLimitation_ReturnsDisposition()
    {
        const string limitation = "Not exposed in this API candidate.";

        var disposition = FoundryHarnessFeatureDisposition.Create(
            FoundryHarnessFeature.BackgroundAgents,
            FoundryHarnessFeatureRequestedState.NotRequested,
            FoundryHarnessFeatureEffectiveState.Disabled,
            limitation,
            FoundryHarnessFeatureBackingSelection.NotApplicable,
            backingDescription: null);

        Assert.Equal(FoundryHarnessFeatureRequestedState.NotRequested, disposition.RequestedState);
        Assert.Equal(FoundryHarnessFeatureEffectiveState.Disabled, disposition.EffectiveState);
        Assert.Equal(limitation, disposition.Limitation);
    }

    [Fact]
    internal void DispositionCreate_ValidWithCallerSuppliedBacking_ReturnsDisposition()
    {
        const string backingDescription = "Caller-supplied instance is used directly.";

        var disposition = FoundryHarnessFeatureDisposition.Create(
            FoundryHarnessFeature.FileMemory,
            FoundryHarnessFeatureRequestedState.RequestedEnabled,
            FoundryHarnessFeatureEffectiveState.Enabled,
            limitation: null,
            FoundryHarnessFeatureBackingSelection.CallerSupplied,
            backingDescription);

        Assert.Equal(FoundryHarnessFeatureBackingSelection.CallerSupplied, disposition.BackingSelection);
        Assert.Equal(backingDescription, disposition.BackingDescription);
    }
}
