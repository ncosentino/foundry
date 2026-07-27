using System.Diagnostics;

using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using NexusLabs.Foundry.Evaluation;
using NexusLabs.Foundry.Evaluation.Harness;
using NexusLabs.Foundry.MicrosoftAgentFramework;
using NexusLabs.Foundry.MicrosoftAgentFramework.Context;
using NexusLabs.Foundry.MicrosoftAgentFramework.Diagnostics;
using NexusLabs.Foundry.MicrosoftAgentFramework.Harness;
using NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Bundle;
using NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Capabilities;
using NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Context;
using NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Providers;
using NexusLabs.Foundry.MicrosoftAgentFramework.Iterative;
using NexusLabs.Foundry.MicrosoftAgentFramework.Workspace;

namespace HarnessEvaluationApp;

internal sealed class HostedArmExecutors
{
    private readonly HostedEvaluationOptions _options;
    private readonly HostedRequestBudget _requestBudget;
    private readonly Func<IChatClient>? _realChatClientFactory;

    internal HostedArmExecutors(
        HostedEvaluationOptions options,
        HostedRequestBudget requestBudget,
        Func<IChatClient>? realChatClientFactory)
    {
        _options = options;
        _requestBudget = requestBudget;
        _realChatClientFactory = realChatClientFactory;
    }

    internal ValueTask<HostedTrialOutput> RunIterativeAsync(
        HarnessComparisonArmExecutionContext context,
        CancellationToken cancellationToken) =>
        RunAsync(context, RunIterativeCoreAsync, cancellationToken);

    internal ValueTask<HostedTrialOutput> RunPlainHarnessAsync(
        HarnessComparisonArmExecutionContext context,
        CancellationToken cancellationToken) =>
        RunAsync(context, RunPlainHarnessCoreAsync, cancellationToken);

    internal ValueTask<HostedTrialOutput> RunHybridAsync(
        HarnessComparisonArmExecutionContext context,
        CancellationToken cancellationToken) =>
        RunAsync(context, RunHybridCoreAsync, cancellationToken);

    private async ValueTask<HostedTrialOutput> RunAsync(
        HarnessComparisonArmExecutionContext context,
        Func<
            HarnessComparisonArmExecutionContext,
            HostedCaseDefinition,
            InMemoryWorkspace,
            IReadOnlyList<AIFunction>,
            HostedToolCallRecorder,
            IChatClient,
            CancellationToken,
            Task<string?>> runner,
        CancellationToken cancellationToken)
    {
        var definition = HostedCaseCatalog.Get(context.Case.Id);
        var workspace = new InMemoryWorkspace();
        var recorder = new HostedToolCallRecorder();
        var cancellationDelay = TimeSpan.FromSeconds(_options.AttemptTimeoutSeconds * 2);
        var tools = HostedCaseTools.Create(workspace, recorder, cancellationDelay);
        var trialCaptureReference = Path.Combine(
            "capture",
            HarnessComparisonExperiment.GetArmId(context.Arm),
            context.Case.Id,
            $"trial-{context.TrialIndex}");
        var captureReference = Path.Combine(
            trialCaptureReference,
            $"attempt-{context.AttemptNumber}");
        var captureDirectory = Path.Combine(_options.OutputDirectory, captureReference);
        Directory.CreateDirectory(captureDirectory);
        var providerClient = new HostedTelemetryChatClient(
            CreateBaseChatClient(),
            _requestBudget,
            _options.MaximumRequestsPerAttempt);
        var chatClient = new HostedOutputCapChatClient(
            new EvaluationCaptureChatClient(
                providerClient,
                new FileEvaluationCaptureStore(captureDirectory)),
            _options.MaximumOutputTokens);
        var stopwatch = Stopwatch.StartNew();
        string? responseText = null;
        var terminalCategory = HarnessRunTerminalCategory.Completed;
        try
        {
            responseText = await runner(
                context,
                definition,
                workspace,
                tools,
                recorder,
                chatClient,
                cancellationToken).ConfigureAwait(false);
            if (definition.ExpectsTimeout && cancellationToken.IsCancellationRequested)
            {
                terminalCategory = HarnessRunTerminalCategory.PerAttemptTimeout;
            }
        }
        catch (OperationCanceledException) when (
            definition.ExpectsTimeout &&
            cancellationToken.IsCancellationRequested)
        {
            terminalCategory = HarnessRunTerminalCategory.PerAttemptTimeout;
        }
        finally
        {
            (chatClient as IDisposable)?.Dispose();
        }
        stopwatch.Stop();

        var output = definition.ExpectsTimeout
            ? null
            : workspace.TryReadFile(definition.OutputPath);
        var completion = definition.ExpectsTimeout
            ? terminalCategory == HarnessRunTerminalCategory.PerAttemptTimeout
            : output is { Success: true } &&
              string.Equals(
                  output.Value.Content,
                  definition.ExpectedOutput,
                  StringComparison.Ordinal);
        return new HostedTrialOutput(
            context.Arm,
            context.Case.Id,
            context.TrialIndex,
            terminalCategory,
            completion,
            responseText,
            recorder.Snapshot(),
            providerClient.CumulativeTokens,
            providerClient.PeakTokens,
            stopwatch.Elapsed.TotalMilliseconds,
            trialCaptureReference.Replace('\\', '/'),
            output is { Success: true } ? output.Value.Content : null);
    }

    private async Task<string?> RunIterativeCoreAsync(
        HarnessComparisonArmExecutionContext context,
        HostedCaseDefinition definition,
        InMemoryWorkspace workspace,
        IReadOnlyList<AIFunction> tools,
        HostedToolCallRecorder recorder,
        IChatClient chatClient,
        CancellationToken cancellationToken)
    {
        using var services = new ServiceCollection()
            .AddFoundryAgentFramework(builder => builder
                .UsingChatClient(chatClient))
            .BuildServiceProvider();
        var loop = services.GetRequiredService<IIterativeAgentLoop>();
        var executionContext = new AgentExecutionContext(
            UserId: "harness-evaluation",
            OrchestrationId: $"iterative-{definition.Id}-{context.TrialIndex}",
            Workspace: workspace);
        var result = await loop.RunAsync(
            new IterativeLoopOptions
            {
                LoopName = $"iterative-{definition.Id}",
                Instructions = HostedCaseCatalog.CommonInstructions,
                Tools = tools.Cast<AITool>().ToArray(),
                PromptFactory = _ => definition.Prompt,
                MaxIterations = 4,
                MaxTotalToolCalls = 8,
                ToolResultMode = ToolResultMode.OneRoundTrip,
                CheckCompletionAfterToolCalls = ToolCompletionCheckMode.AfterToolRounds,
                IsComplete = _ => !definition.ExpectsTimeout &&
                    workspace.FileExists(definition.OutputPath),
                ExecutionContext = executionContext,
            },
            new IterativeContext { Workspace = workspace },
            cancellationToken).ConfigureAwait(false);
        if (definition.ExpectsTimeout &&
            !result.Succeeded &&
            result.Termination == TerminationReason.Cancelled &&
            cancellationToken.IsCancellationRequested)
        {
            cancellationToken.ThrowIfCancellationRequested();
        }

        if (!result.Succeeded && !definition.ExpectsTimeout)
        {
            throw new InvalidOperationException(result.ErrorMessage ?? $"Iterative arm failed: {result.Termination}.");
        }

        return result.FinalResponse?.Text;
    }

    private async Task<string?> RunPlainHarnessCoreAsync(
        HarnessComparisonArmExecutionContext context,
        HostedCaseDefinition definition,
        InMemoryWorkspace workspace,
        IReadOnlyList<AIFunction> tools,
        HostedToolCallRecorder recorder,
        IChatClient chatClient,
        CancellationToken cancellationToken)
    {
        var factory = new FoundryHarnessAgentFactory();
        var configuration = new FoundryHarnessAgentConfiguration
        {
            Id = null,
            Name = $"plain-harness-{definition.Id}",
            Description = "Hosted plain Harness comparison arm.",
            Instructions = HostedCaseCatalog.CommonInstructions,
            HarnessInstructionsOverride = string.Empty,
            ChatClient = chatClient,
            Tools = tools.Cast<AITool>().ToArray(),
            Features = new FoundryHarnessFeatureSelections
            {
                EnableWebSearch = false,
                EnableFileMemory = false,
                EnableAgentSkills = false,
                EnableToolAutoApproval = false,
                EnableApprovalNotRequiredFunctionBypassing = false,
                EnableApprovalResponseBinding = false,
                EnableOpenTelemetry = false,
                EnableTodoProvider = false,
                EnableAgentModeProvider = false,
                EnableCompaction = true,
            },
            ProgressAccessor = null,
            MaxContextWindowTokens = 8000,
            MaxOutputTokens = _options.MaximumOutputTokens,
            MaximumIterationsPerRequest = 8,
            FileAccessStore = null,
            FileAccessProviderOptions = null,
            ChatHistoryProvider = null,
            FileMemoryStore = null,
            AgentSkillsSource = null,
            ToolApprovalAgentOptions = null,
            AgentModeProviderOptions = null,
            CompactionStrategy = null,
            OpenTelemetrySourceName = null,
            AdditionalContextProviders = [],
        };
        var agent = factory.Create(configuration, NullLoggerFactory.Instance);
        try
        {
            var session = await agent.CreateSessionAsync(cancellationToken).ConfigureAwait(false);
            var response = await agent
                .RunAsync(definition.Prompt, session, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            return response.GetText();
        }
        finally
        {
            (agent as IDisposable)?.Dispose();
        }
    }

    private async Task<string?> RunHybridCoreAsync(
        HarnessComparisonArmExecutionContext context,
        HostedCaseDefinition definition,
        InMemoryWorkspace workspace,
        IReadOnlyList<AIFunction> tools,
        HostedToolCallRecorder recorder,
        IChatClient chatClient,
        CancellationToken cancellationToken)
    {
        using var services = new ServiceCollection()
            .AddFoundryAgentFramework()
            .BuildServiceProvider();
        var contextAccessor = services.GetRequiredService<IAgentExecutionContextAccessor>();
        var executionContext = new AgentExecutionContext(
            UserId: "harness-evaluation",
            OrchestrationId: $"hybrid-{definition.Id}-{context.TrialIndex}",
            Workspace: workspace);
        using var executionScope = contextAccessor.BeginScope(executionContext);
        var sessionId = $"hybrid-{definition.Id}-{context.TrialIndex}-{context.AttemptNumber}";
        var capture = HarnessExecutionBinding.Capture(
            contextAccessor,
            sessionId,
            requireWorkspace: true);
        if (capture.Status != HarnessExecutionBindingStatus.Valid || capture.Binding is null)
        {
            throw new InvalidOperationException(capture.Detail ?? $"Binding failed: {capture.Status}.");
        }

        var generatedTools = new HarnessGeneratedToolResolution(
            HarnessGeneratedToolResolutionStatus.Success,
            tools,
            MissingFunctionTypes: [],
            DuplicateToolNames: []);
        var profile = new HarnessCapabilityResolver().Resolve(
            new HarnessCapabilityResolutionRequest(
                ProfileId: "hosted-hybrid-v1",
                Lane: HarnessConstructionLane.SelectedProviders,
                Acceptance: HarnessCapabilityAcceptance.StableAndExperimental,
                EvidenceThroughPhase: HarnessDeliveryPhase.G5,
                RequestedCapabilities: new HashSet<HarnessCapability>
                {
                    HarnessCapability.GeneratedTools,
                    HarnessCapability.FunctionInvocation,
                    HarnessCapability.MessageInjection,
                    HarnessCapability.OpenTelemetry,
                    HarnessCapability.Compaction,
                },
                ProviderCapabilities: new HashSet<HarnessProviderCapability>(),
                ToolLoopOwner: HarnessToolLoopOwner.Foundry,
                TelemetryOwner: HarnessTelemetryOwner.Foundry,
                HistoryPersistenceMode: HarnessHistoryPersistenceMode.NotApplicable));
        if (!profile.IsExecutable)
        {
            throw new InvalidOperationException("The hosted hybrid capability profile was not executable.");
        }

        var hybridProfile = HarnessHybridProfile.Create(
            HarnessHybridContextPolicy.Create(
                hardLimit: 18_000,
                triggerMargin: 3_000,
                recentMessageRetentionCount: 3,
                maximumCompactionAttempts: 2,
                preservationLabel: "hosted-hybrid-v1",
                preservationVersion: 1,
                sizeEstimator: new HarnessUtf8ContextSizeEstimator()),
            new HostedTruncatingChatReducer(),
            new HostedHybridMessageClassifier(),
            baselineEntries => new HostedHybridSnapshotProvider(baselineEntries));
        var composition = new HarnessProviderComposition().Compose(
            new HarnessProviderCompositionRequest(
                ChatClient: chatClient,
                Services: services,
                LoggerFactory: NullLoggerFactory.Instance,
                Name: $"hybrid-{definition.Id}",
                Description: "Hosted hybrid Harness/workspace comparison arm.",
                Instructions: HostedCaseCatalog.CommonInstructions,
                Profile: profile,
                HybridProfile: hybridProfile,
                GeneratedTools: generatedTools,
                ExecutionBinding: capture.Binding,
                ExecutionContextAccessor: contextAccessor,
                SessionId: sessionId,
                HistoryProvider: null,
                PlanningProviders: null,
                ApprovalPlugin: null,
                SkillsPlugin: null,
                WebSearchPlugin: null,
                Metrics: services.GetRequiredService<IAgentMetrics>(),
                ProgressAccessor: null,
                OffloadPlugin: HarnessToolResultOffloadPlugin.Create(4096)));
        if (composition.Status != HarnessProviderCompositionStatus.Success ||
            composition.Agent is null)
        {
            throw new InvalidOperationException(
                composition.Detail ?? $"Hybrid composition failed: {composition.Status}.");
        }

        var agent = composition.Agent;
        try
        {
            var session = await agent.CreateSessionAsync(cancellationToken).ConfigureAwait(false);
            var response = await agent
                .RunAsync(definition.Prompt, session, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            return response.GetText();
        }
        finally
        {
            (agent as IDisposable)?.Dispose();
        }
    }

    private IChatClient CreateBaseChatClient()
    {
        if (_options.DryRun)
        {
            return new HostedScriptedChatClient();
        }

        return _realChatClientFactory?.Invoke()
            ?? throw new InvalidOperationException("The real GitHub Models chat client was not configured.");
    }
}
