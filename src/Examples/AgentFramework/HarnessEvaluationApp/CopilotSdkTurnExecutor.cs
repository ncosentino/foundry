using GitHub.Copilot;
using GitHub.Copilot.Rpc;

namespace HarnessEvaluationApp;

internal sealed class CopilotSdkTurnExecutor
{
    private const string ProviderSystemMessage =
        "You are a stateless chat-completion provider embedded in an external " +
        "Foundry agent loop. The user message is the complete serialized " +
        "conversation transcript. Continue exactly one assistant turn. Use only " +
        "the declared tools. Do not use ambient files, shell commands, web access, " +
        "skills, memory, subagents, or undeclared tools. When a tool is required, " +
        "invoke it with exact arguments and wait for the external loop. Otherwise, " +
        "return only the final assistant response.";

    private readonly CopilotClient _client;
    private readonly string _workingDirectory;

    internal CopilotSdkTurnExecutor(
        CopilotClient client,
        string workingDirectory)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);
        _client = client;
        _workingDirectory = workingDirectory;
    }

    internal async Task<CopilotSdkTurnResult> ExecuteAsync(
        CopilotSdkTurnRequest request,
        CancellationToken cancellationToken)
    {
        using var sessionDirectory = CopilotSdkSessionDirectory.Create(
            _workingDirectory);
        var declarations = request.Tools
            .Select(tool => tool.AsDeclarationOnly())
            .ToArray();
        var allowedToolNames = declarations
            .Select(tool => tool.Name)
            .ToHashSet(StringComparer.Ordinal);
#pragma warning disable GHCP001
        var configuration = new SessionConfig
        {
            Model = request.ModelId,
            // This is the common provider cap. Each arm has already transformed
            // its transcript through its own context and compaction policy.
            ModelCapabilities = new ModelCapabilitiesOverride
            {
                Limits = new ModelCapabilitiesOverrideLimits
                {
                    MaxPromptTokens = 8000,
                    MaxOutputTokens = request.MaximumOutputTokens,
                },
            },
            Tools = declarations,
            AvailableTools = allowedToolNames.ToArray(),
            OnPermissionRequest = (permission, _) =>
                Task.FromResult<PermissionDecision>(
                    permission is PermissionRequestCustomTool customTool &&
                    allowedToolNames.Contains(customTool.ToolName)
                        ? PermissionDecision.ApproveOnce()
                        : PermissionDecision.Reject(
                            "Only the declaration-only tools supplied by Foundry are allowed.")),
            SystemMessage = new SystemMessageConfig
            {
                Mode = SystemMessageMode.Replace,
                Content = ProviderSystemMessage,
            },
            InfiniteSessions = new InfiniteSessionConfig { Enabled = false },
            Memory = new MemoryConfiguration { Enabled = false },
            ToolSearch = new ToolSearchConfig { Enabled = false },
            LargeOutput = new LargeToolOutputConfig { Enabled = false },
            EnableConfigDiscovery = false,
            EnableOnDemandInstructionDiscovery = false,
            EnableFileHooks = false,
            EnableHostGitOperations = false,
            EnableSessionStore = false,
            EnableSkills = false,
            SkipCustomInstructions = true,
            SkipEmbeddingRetrieval = true,
            EmbeddingCacheStorage = EmbeddingCacheStorageMode.InMemory,
            McpOAuthTokenStorage = McpOAuthTokenStorageMode.InMemory,
            WorkingDirectory = sessionDirectory.DirectoryPath,
            Streaming = false,
        };
#pragma warning restore GHCP001
        var session = await _client
            .CreateSessionAsync(configuration, cancellationToken)
            .ConfigureAwait(false);
        var collector = new CopilotSdkTurnCollector(request.ModelId);
        var subscription = session.On<SessionEvent>(collector.Observe);
        try
        {
            await session.SendAsync(
                new MessageOptions { Prompt = request.TranscriptJson },
                cancellationToken).ConfigureAwait(false);
            return await collector.Completion
                .WaitAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            subscription.Dispose();
            await session.DisposeAsync().ConfigureAwait(false);
            await _client.DeleteSessionAsync(session.SessionId, CancellationToken.None)
                .ConfigureAwait(false);
        }
    }
}
