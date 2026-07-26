using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

using NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Bundle;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Tests;

/// <summary>
/// Behavioral/default pins against a real, constructed upstream <c>Microsoft.Agents.AI.HarnessAgent</c>
/// (via <see cref="FoundryHarnessAgentFactory.Create(FoundryHarnessAgentConfiguration)"/> with a fake
/// <see cref="IChatClient"/>, no live network dependency): asserts on <c>agent.GetService&lt;T&gt;()</c>
/// results directly, rather than only on <see cref="FoundryHarnessBundleDefaultsInspector"/> output.
/// </summary>
/// <remarks>
/// <para>
/// These tests were designed from live reflection probes against
/// <c>Microsoft.Agents.AI.Harness</c> 1.15.0 (see the package-level research notes), which
/// confirmed exactly which pipeline components are discoverable via <c>GetService&lt;T&gt;()</c> on
/// the constructed agent.
/// </para>
/// <para>
/// <b>Known discoverability gap (do not "fix" by adding reflection to production code):</b> the
/// in-loop compaction provider is <b>not</b> discoverable via <c>GetService&lt;T&gt;()</c> even when
/// compaction is genuinely active with valid token budgets; live probing showed nothing
/// "Compaction"-related is reachable anywhere in the constructed agent's object graph via public
/// reflection, and the internal chat-client decorator that would own it
/// (<c>AIContextProviderChatClient</c>) is a non-public type in
/// <c>Microsoft.Agents.AI</c> 1.15.0 that does not forward <c>GetService</c> queries down to it.
/// <c>PerServiceCallChatHistoryPersistingChatClient</c> is likewise non-public, so it can never be
/// used as a <c>GetService&lt;T&gt;()</c> type argument from test code either. These are reported as
/// permanent limitations of this API candidate rather than worked around with test-only reflection
/// hacks (production code performs no reflection to compensate).
/// </para>
/// </remarks>
public sealed class HarnessAgentPipelineBehaviorTests
{
    private static readonly FoundryHarnessAgentFactory Factory = new();

    [Fact]
    public void Create_FunctionInvokingChatClient_IsAlwaysDiscoverable()
    {
        var configuration = HarnessBundleTestsHelpers.CreateBaseline();

        var agent = Factory.Create(configuration);

        Assert.NotNull(agent.GetService<FunctionInvokingChatClient>());
    }

    [Fact]
    public void Create_MaximumIterationsPerRequest_ReflectsConfiguredValue()
    {
        var configuration = HarnessBundleTestsHelpers.CreateBaseline() with
        {
            MaximumIterationsPerRequest = 12,
        };

        var agent = Factory.Create(configuration);

        var functionInvokingChatClient = agent.GetService<FunctionInvokingChatClient>();
        Assert.NotNull(functionInvokingChatClient);
        Assert.Equal(12, functionInvokingChatClient.MaximumIterationsPerRequest);
    }

    [Fact]
    public void Create_MessageInjectingChatClient_IsAlwaysDiscoverable()
    {
        var configuration = HarnessBundleTestsHelpers.CreateBaseline();

        var agent = Factory.Create(configuration);

        Assert.NotNull(agent.GetService<MessageInjectingChatClient>());
    }

    [Fact]
    public void Create_NoChatHistoryProviderSupplied_ResolvesToInMemoryChatHistoryProviderDefault()
    {
        var configuration = HarnessBundleTestsHelpers.CreateBaseline();

        var agent = Factory.Create(configuration);

        var resolved = agent.GetService<ChatHistoryProvider>();
        Assert.NotNull(resolved);
        Assert.IsType<InMemoryChatHistoryProvider>(resolved);
    }

    [Fact]
    public void Create_ChatHistoryProviderSupplied_ResolvesToExactSameInstance()
    {
        var suppliedProvider = new FakeChatHistoryProvider();
        var configuration = HarnessBundleTestsHelpers.CreateBaseline() with
        {
            ChatHistoryProvider = suppliedProvider,
        };

        var agent = Factory.Create(configuration);

        var resolved = agent.GetService<ChatHistoryProvider>();
        Assert.Same(suppliedProvider, resolved);
    }

    [Fact]
    public void Create_ToolAutoApprovalEnabled_ToolApprovalAgentIsDiscoverable()
    {
        var configuration = HarnessBundleTestsHelpers.CreateBaseline(
            HarnessBundleTestsHelpers.AllFeaturesDisabled() with { EnableToolAutoApproval = true });

        var agent = Factory.Create(configuration);

        Assert.NotNull(agent.GetService<ToolApprovalAgent>());
    }

    [Fact]
    public void Create_ToolAutoApprovalDisabled_ToolApprovalAgentIsNotDiscoverable()
    {
        var configuration = HarnessBundleTestsHelpers.CreateBaseline(
            HarnessBundleTestsHelpers.AllFeaturesDisabled());

        var agent = Factory.Create(configuration);

        Assert.Null(agent.GetService<ToolApprovalAgent>());
    }

    [Fact]
    public void Create_OpenTelemetryEnabled_OpenTelemetryAgentIsDiscoverable()
    {
        var configuration = HarnessBundleTestsHelpers.CreateBaseline(
            HarnessBundleTestsHelpers.AllFeaturesDisabled() with { EnableOpenTelemetry = true });

        var agent = Factory.Create(configuration);

        Assert.NotNull(agent.GetService<OpenTelemetryAgent>());
    }

    [Fact]
    public void Create_OpenTelemetryDisabled_OpenTelemetryAgentIsNotDiscoverable()
    {
        var configuration = HarnessBundleTestsHelpers.CreateBaseline(
            HarnessBundleTestsHelpers.AllFeaturesDisabled());

        var agent = Factory.Create(configuration);

        Assert.Null(agent.GetService<OpenTelemetryAgent>());
    }

    [Fact]
    public void Create_FileMemoryEnabled_FileMemoryProviderIsDiscoverable()
    {
        var configuration = HarnessBundleTestsHelpers.CreateBaseline(
            HarnessBundleTestsHelpers.AllFeaturesDisabled() with { EnableFileMemory = true });

        var agent = Factory.Create(configuration);

        Assert.NotNull(agent.GetService<FileMemoryProvider>());
    }

    [Fact]
    public void Create_FileMemoryDisabled_FileMemoryProviderIsNotDiscoverable()
    {
        var configuration = HarnessBundleTestsHelpers.CreateBaseline(
            HarnessBundleTestsHelpers.AllFeaturesDisabled());

        var agent = Factory.Create(configuration);

        Assert.Null(agent.GetService<FileMemoryProvider>());
    }

    [Fact]
    public void Create_AgentSkillsEnabled_AgentSkillsProviderIsDiscoverable()
    {
        var configuration = HarnessBundleTestsHelpers.CreateBaseline(
            HarnessBundleTestsHelpers.AllFeaturesDisabled() with { EnableAgentSkills = true });

        var agent = Factory.Create(configuration);

        Assert.NotNull(agent.GetService<AgentSkillsProvider>());
    }

    [Fact]
    public void Create_AgentSkillsDisabled_AgentSkillsProviderIsNotDiscoverable()
    {
        var configuration = HarnessBundleTestsHelpers.CreateBaseline(
            HarnessBundleTestsHelpers.AllFeaturesDisabled());

        var agent = Factory.Create(configuration);

        Assert.Null(agent.GetService<AgentSkillsProvider>());
    }

    [Fact]
    public void Create_AgentModeProviderEnabled_AgentModeProviderIsDiscoverable()
    {
        var configuration = HarnessBundleTestsHelpers.CreateBaseline(
            HarnessBundleTestsHelpers.AllFeaturesDisabled() with { EnableAgentModeProvider = true });

        var agent = Factory.Create(configuration);

        Assert.NotNull(agent.GetService<AgentModeProvider>());
    }

    [Fact]
    public void Create_AgentModeProviderDisabled_AgentModeProviderIsNotDiscoverable()
    {
        var configuration = HarnessBundleTestsHelpers.CreateBaseline(
            HarnessBundleTestsHelpers.AllFeaturesDisabled());

        var agent = Factory.Create(configuration);

        Assert.Null(agent.GetService<AgentModeProvider>());
    }

    [Fact]
    public void Create_TodoProviderEnabled_TodoProviderIsDiscoverable()
    {
        var configuration = HarnessBundleTestsHelpers.CreateBaseline(
            HarnessBundleTestsHelpers.AllFeaturesDisabled() with { EnableTodoProvider = true });

        var agent = Factory.Create(configuration);

        Assert.NotNull(agent.GetService<TodoProvider>());
    }

    [Fact]
    public void Create_TodoProviderDisabled_TodoProviderIsNotDiscoverable()
    {
        var configuration = HarnessBundleTestsHelpers.CreateBaseline(
            HarnessBundleTestsHelpers.AllFeaturesDisabled());

        var agent = Factory.Create(configuration);

        Assert.Null(agent.GetService<TodoProvider>());
    }

    [Fact]
    public void Create_FileAccessStoreSupplied_FileAccessProviderIsDiscoverable()
    {
        var configuration = HarnessBundleTestsHelpers.CreateBaseline() with
        {
            FileAccessStore = new InMemoryAgentFileStoreFake(),
        };

        var agent = Factory.Create(configuration);

        Assert.NotNull(agent.GetService<FileAccessProvider>());
    }

    [Fact]
    public void Create_NoFileAccessStoreSupplied_FileAccessProviderIsNotDiscoverable()
    {
        var configuration = HarnessBundleTestsHelpers.CreateBaseline();

        var agent = Factory.Create(configuration);

        Assert.Null(agent.GetService<FileAccessProvider>());
    }

    [Fact]
    public void Create_ChatClientAgentIsAlwaysDiscoverable()
    {
        var configuration = HarnessBundleTestsHelpers.CreateBaseline();

        var agent = Factory.Create(configuration);

        Assert.NotNull(agent.GetService<ChatClientAgent>());
    }

    [Fact]
    public void Create_WithLoggerFactoryOverload_FunctionInvokingChatClientIsDiscoverable()
    {
        var configuration = HarnessBundleTestsHelpers.CreateBaseline();

        var agent = Factory.Create(configuration, NullLoggerFactory.Instance);

        Assert.NotNull(agent.GetService<FunctionInvokingChatClient>());
    }

    [Theory]
    [InlineData(FoundryHarnessFeature.WebSearch)]
    [InlineData(FoundryHarnessFeature.TodoProvider)]
    [InlineData(FoundryHarnessFeature.AgentModeProvider)]
    [InlineData(FoundryHarnessFeature.FileMemory)]
    [InlineData(FoundryHarnessFeature.FileAccess)]
    [InlineData(FoundryHarnessFeature.AgentSkills)]
    public async Task Run_EnabledBuiltInProviderInjectsExpectedToolNames(
        FoundryHarnessFeature feature)
    {
        var chatClient = new FakeHarnessChatClient();
        var configuration = HarnessBundleTestsHelpers.CreateWithBuiltInToolProvider(feature, chatClient);
        var agent = Factory.Create(configuration);

        await agent.RunAsync(
            "run",
            cancellationToken: TestContext.Current.CancellationToken);

        var actualToolNames = chatClient.LastOptions?.Tools?
            .Select(tool => tool.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(GetExpectedBuiltInToolNames(feature), actualToolNames);
    }

    [Fact]
    public async Task Run_MaxOutputTokensOnly_PropagatesToProviderChatOptions()
    {
        var chatClient = new FakeHarnessChatClient();
        var configuration = HarnessBundleTestsHelpers.CreateBaseline() with
        {
            ChatClient = chatClient,
            MaxOutputTokens = 1_234,
        };
        var agent = Factory.Create(configuration);

        await agent.RunAsync(
            "run",
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(1_234, chatClient.LastOptions?.MaxOutputTokens);
    }

    private static IReadOnlyList<string> GetExpectedBuiltInToolNames(
        FoundryHarnessFeature feature) =>
        feature switch
        {
            FoundryHarnessFeature.WebSearch =>
            [
                "web_search",
            ],
            FoundryHarnessFeature.TodoProvider =>
            [
                "todos_add",
                "todos_complete",
                "todos_get_all",
                "todos_get_remaining",
                "todos_remove",
            ],
            FoundryHarnessFeature.AgentModeProvider =>
            [
                "mode_get",
                "mode_set",
            ],
            FoundryHarnessFeature.FileMemory =>
            [
                "file_memory_delete",
                "file_memory_grep",
                "file_memory_ls",
                "file_memory_read",
                "file_memory_replace",
                "file_memory_replace_lines",
                "file_memory_write",
            ],
            FoundryHarnessFeature.FileAccess =>
            [
                "file_access_delete",
                "file_access_grep",
                "file_access_ls",
                "file_access_read",
                "file_access_replace",
                "file_access_replace_lines",
                "file_access_write",
            ],
            FoundryHarnessFeature.AgentSkills =>
            [
                "load_skill",
                "read_skill_resource",
                "run_skill_script",
            ],
            _ => throw new ArgumentOutOfRangeException(nameof(feature), feature, null),
        };
}
