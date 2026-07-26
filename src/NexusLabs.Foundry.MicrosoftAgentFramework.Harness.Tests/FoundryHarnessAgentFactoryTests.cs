using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

using NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Bundle;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Tests;

/// <summary>
/// Proves that <see cref="FoundryHarnessAgentFactory"/> fails closed on invalid configuration and
/// otherwise constructs an <see cref="Microsoft.Agents.AI.AIAgent"/> through the official upstream
/// <c>Microsoft.Agents.AI.Harness</c> complete-bundle pipeline, entirely against a fake
/// <see cref="IChatClient"/> with no live service dependency.
/// </summary>
public sealed class FoundryHarnessAgentFactoryTests
{
    private static readonly FoundryHarnessAgentFactory Factory = new();

    [Fact]
    public void Create_NullConfiguration_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => Factory.Create(configuration: null!));
    }

    [Fact]
    public void Create_NullChatClient_ThrowsArgumentNullException()
    {
        var configuration = HarnessBundleTestsHelpers.CreateBaseline() with { ChatClient = null! };

        Assert.Throws<ArgumentNullException>(() => Factory.Create(configuration));
    }

    [Fact]
    public void Create_NullTools_ThrowsArgumentNullException()
    {
        var configuration = HarnessBundleTestsHelpers.CreateBaseline() with { Tools = null! };

        Assert.Throws<ArgumentNullException>(() => Factory.Create(configuration));
    }

    [Fact]
    public void Create_NullFeatures_ThrowsArgumentNullException()
    {
        var configuration = HarnessBundleTestsHelpers.CreateBaseline() with { Features = null! };

        Assert.Throws<ArgumentNullException>(() => Factory.Create(configuration));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_BlankName_ThrowsArgumentException(string name)
    {
        var configuration = HarnessBundleTestsHelpers.CreateBaseline() with { Name = name };

        Assert.Throws<ArgumentException>(() => Factory.Create(configuration));
    }

    [Fact]
    public void Create_DuplicateToolNames_ThrowsArgumentException()
    {
        var configuration = HarnessBundleTestsHelpers.CreateBaseline() with
        {
            Tools =
            [
                AIFunctionFactory.Create(() => "a", name: "shared-tool"),
                AIFunctionFactory.Create(() => "b", name: "shared-tool"),
            ],
        };

        var exception = Assert.Throws<ArgumentException>(() => Factory.Create(configuration));
        Assert.Contains("shared-tool", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_CompactionEnabledWithoutTokenBudgets_ThrowsArgumentException()
    {
        var configuration = HarnessBundleTestsHelpers.CreateBaseline(
            HarnessBundleTestsHelpers.AllFeaturesEnabled());

        Assert.Throws<ArgumentException>(() => Factory.Create(configuration));
    }

    [Fact]
    public void Create_CompactionEnabledWithOnlyContextWindowTokens_ThrowsArgumentException()
    {
        var configuration = HarnessBundleTestsHelpers.CreateBaseline(
            HarnessBundleTestsHelpers.AllFeaturesEnabled()) with
        {
            MaxContextWindowTokens = 8_000,
        };

        Assert.Throws<ArgumentException>(() => Factory.Create(configuration));
    }

    [Fact]
    public void Create_ChatClientWithExistingFunctionInvocationLoop_ThrowsInvalidOperationException()
    {
        var decoratedChatClient = new ChatClientBuilder(new FakeHarnessChatClient())
            .UseFunctionInvocation()
            .Build();
        var configuration = HarnessBundleTestsHelpers.CreateBaseline() with { ChatClient = decoratedChatClient };

        Assert.Throws<InvalidOperationException>(() => Factory.Create(configuration));
    }

    [Fact]
    public void Create_ChatClientWithExistingMessageInjection_ThrowsInvalidOperationException()
    {
        var decoratedChatClient = new MessageInjectingChatClient(new FakeHarnessChatClient());
        var configuration = HarnessBundleTestsHelpers.CreateBaseline() with { ChatClient = decoratedChatClient };

        Assert.Throws<InvalidOperationException>(() => Factory.Create(configuration));
    }

    [Fact]
    public void Create_OpenTelemetryEnabledWithExistingInstrumentation_ThrowsInvalidOperationException()
    {
        var decoratedChatClient = new ChatClientBuilder(new FakeHarnessChatClient())
            .UseOpenTelemetry()
            .Build();
        var configuration = HarnessBundleTestsHelpers.CreateBaseline(
            HarnessBundleTestsHelpers.AllFeaturesDisabled() with { EnableCompaction = false }) with
        {
            ChatClient = decoratedChatClient,
        };

        // OTel enabled + pre-existing OTel → duplicate telemetry owner
        Assert.Throws<InvalidOperationException>(() => Factory.Create(configuration));
    }

    [Fact]
    public void Create_OpenTelemetryDisabledWithExistingInstrumentation_ThrowsInvalidOperationException()
    {
        // Fail-closed: pre-existing OTel is rejected even when EnableOpenTelemetry is false,
        // because the requested-disabled state would be ineffective (the instrumentation remains active).
        var decoratedChatClient = new ChatClientBuilder(new FakeHarnessChatClient())
            .UseOpenTelemetry()
            .Build();
        var configuration = HarnessBundleTestsHelpers.CreateBaseline(
            HarnessBundleTestsHelpers.AllFeaturesDisabled()) with
        {
            ChatClient = decoratedChatClient,
        };

        Assert.Throws<InvalidOperationException>(() => Factory.Create(configuration));
    }

    [Fact]
    public void DescribeEffectiveDefaults_OpenTelemetryDisabledWithExistingInstrumentation_ThrowsInvalidOperationException()
    {
        // Defaults-report consistency: the report and Create have the same validity envelope.
        // A configuration that would fail Create must also fail DescribeEffectiveDefaults so
        // the inspector never produces a report that cannot actually be constructed.
        var decoratedChatClient = new ChatClientBuilder(new FakeHarnessChatClient())
            .UseOpenTelemetry()
            .Build();
        var configuration = HarnessBundleTestsHelpers.CreateBaseline(
            HarnessBundleTestsHelpers.AllFeaturesDisabled()) with
        {
            ChatClient = decoratedChatClient,
        };

        Assert.Throws<InvalidOperationException>(() => Factory.DescribeEffectiveDefaults(configuration));
    }

    [Fact]
    public void Create_ValidMinimalConfiguration_ReturnsAgentWithNoLiveServiceCall()
    {
        var configuration = HarnessBundleTestsHelpers.CreateBaseline();

        var agent = Factory.Create(configuration);

        Assert.NotNull(agent);
        Assert.Equal(configuration.Name, agent.Name);
    }

    [Fact]
    public void Create_ValidConfigurationWithTools_ReturnsAgent()
    {
        var configuration = HarnessBundleTestsHelpers.CreateBaseline() with
        {
            Tools = [AIFunctionFactory.Create(() => "ok", name: "distinct-tool")],
        };

        var agent = Factory.Create(configuration);

        Assert.NotNull(agent);
    }

    [Fact]
    public void Create_ValidConfigurationWithCompactionAndBothTokenBudgets_ReturnsAgent()
    {
        var configuration = HarnessBundleTestsHelpers.CreateBaseline(
            HarnessBundleTestsHelpers.AllFeaturesDisabled() with { EnableCompaction = true }) with
        {
            MaxContextWindowTokens = 8_000,
            MaxOutputTokens = 1_000,
        };

        var agent = Factory.Create(configuration);

        Assert.NotNull(agent);
    }

    [Fact]
    public void Create_NonNullEmptyId_ThrowsArgumentException()
    {
        var configuration = HarnessBundleTestsHelpers.CreateBaseline() with { Id = "" };

        Assert.Throws<ArgumentException>(() => Factory.Create(configuration));
    }

    [Theory]
    [InlineData("   ")]
    public void Create_NonNullWhitespaceId_ThrowsArgumentException(string id)
    {
        var configuration = HarnessBundleTestsHelpers.CreateBaseline() with { Id = id };

        Assert.Throws<ArgumentException>(() => Factory.Create(configuration));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_MaxContextWindowTokensNotPositive_ThrowsArgumentOutOfRangeException(int value)
    {
        var configuration = HarnessBundleTestsHelpers.CreateBaseline() with
        {
            MaxContextWindowTokens = value,
        };

        Assert.Throws<ArgumentOutOfRangeException>(() => Factory.Create(configuration));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_MaxOutputTokensNotPositive_ThrowsArgumentOutOfRangeException(int value)
    {
        var configuration = HarnessBundleTestsHelpers.CreateBaseline() with
        {
            MaxOutputTokens = value,
        };

        Assert.Throws<ArgumentOutOfRangeException>(() => Factory.Create(configuration));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_MaximumIterationsPerRequestNotPositive_ThrowsArgumentOutOfRangeException(int value)
    {
        var configuration = HarnessBundleTestsHelpers.CreateBaseline() with
        {
            MaximumIterationsPerRequest = value,
        };

        Assert.Throws<ArgumentOutOfRangeException>(() => Factory.Create(configuration));
    }

    [Fact]
    public void Create_MaxOutputTokensEqualToMaxContextWindowTokens_ThrowsArgumentException()
    {
        // Upstream requires MaxOutputTokens < MaxContextWindowTokens
        var configuration = HarnessBundleTestsHelpers.CreateBaseline() with
        {
            MaxContextWindowTokens = 1_000,
            MaxOutputTokens = 1_000,
        };

        Assert.Throws<ArgumentException>(() => Factory.Create(configuration));
    }

    [Fact]
    public void Create_MaxOutputTokensGreaterThanMaxContextWindowTokens_ThrowsArgumentException()
    {
        var configuration = HarnessBundleTestsHelpers.CreateBaseline() with
        {
            MaxContextWindowTokens = 1_000,
            MaxOutputTokens = 2_000,
        };

        Assert.Throws<ArgumentException>(() => Factory.Create(configuration));
    }

    [Fact]
    public void Create_NullToolElement_ThrowsArgumentException()
    {
        var configuration = HarnessBundleTestsHelpers.CreateBaseline() with
        {
            Tools = [null!],
        };

        Assert.Throws<ArgumentException>(() => Factory.Create(configuration));
    }

    [Fact]
    public void Create_BlankToolName_ThrowsArgumentException()
    {
        var configuration = HarnessBundleTestsHelpers.CreateBaseline() with
        {
            Tools = [AIFunctionFactory.Create(() => "ok", name: "   ")],
        };

        Assert.Throws<ArgumentException>(() => Factory.Create(configuration));
    }

    [Fact]
    public void Create_WithLoggerFactoryOverload_ReturnsAgent()
    {
        var configuration = HarnessBundleTestsHelpers.CreateBaseline();

        var agent = Factory.Create(configuration, NullLoggerFactory.Instance);

        Assert.NotNull(agent);
    }

    [Fact]
    public void Create_WithLoggerFactoryOverload_NullLoggerFactory_ThrowsArgumentNullException()
    {
        var configuration = HarnessBundleTestsHelpers.CreateBaseline();

        Assert.Throws<ArgumentNullException>(
            () => Factory.Create(configuration, loggerFactory: null!));
    }

    [Fact]
    public void Create_WithServiceProviderOverload_NullServiceProvider_ThrowsArgumentNullException()
    {
        var configuration = HarnessBundleTestsHelpers.CreateBaseline();

        Assert.Throws<ArgumentNullException>(
            () => Factory.Create(configuration, services: null!));
    }

    [Fact]
    public void Create_WithLoggerFactoryAndServiceProviderOverload_NullServiceProvider_ThrowsArgumentNullException()
    {
        var configuration = HarnessBundleTestsHelpers.CreateBaseline();

        Assert.Throws<ArgumentNullException>(
            () => Factory.Create(configuration, NullLoggerFactory.Instance, services: null!));
    }

    [Fact]
    public void DescribeEffectiveDefaults_NullConfiguration_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => Factory.DescribeEffectiveDefaults(null!));
    }

    [Fact]
    public void DescribeEffectiveDefaults_CompactionEnabledWithoutBudgets_ThrowsSameAsCreate()
    {
        // Validates that DescribeEffectiveDefaults applies the same validation as Create,
        // ensuring the report can never describe a configuration that cannot be constructed.
        var configuration = HarnessBundleTestsHelpers.CreateBaseline(
            HarnessBundleTestsHelpers.AllFeaturesEnabled());

        Assert.Throws<ArgumentException>(() => Factory.DescribeEffectiveDefaults(configuration));
    }

    [Fact]
    public void DescribeEffectiveDefaults_DoesNotConstructAnAgent()
    {
        var configuration = HarnessBundleTestsHelpers.CreateBaseline();

        var effectiveDefaults = Factory.DescribeEffectiveDefaults(configuration);

        Assert.NotNull(effectiveDefaults);
        Assert.NotEmpty(effectiveDefaults.Dispositions);
    }
}
