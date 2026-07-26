using HarnessHybridApp;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using NexusLabs.Foundry.MicrosoftAgentFramework;
using NexusLabs.Foundry.MicrosoftAgentFramework.Context;
using NexusLabs.Foundry.MicrosoftAgentFramework.Diagnostics;
using NexusLabs.Foundry.MicrosoftAgentFramework.Harness;
using NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Capabilities;
using NexusLabs.Foundry.MicrosoftAgentFramework.Progress;
using NexusLabs.Foundry.MicrosoftAgentFramework.Workspace;

const string SessionId = "harness-hybrid-session";
const string ExpectedResponse = "selected-provider:stored:hybrid-proof";

using var services = new ServiceCollection()
    .AddFoundryAgentFramework()
    .AddTransient<HybridWorkspaceTool>()
    .BuildServiceProvider();
using var serviceScope = services.CreateScope();
var runServices = serviceScope.ServiceProvider;
var contextAccessor = services.GetRequiredService<IAgentExecutionContextAccessor>();
var workspace = new InMemoryWorkspace();
var executionContext = new AgentExecutionContext(
    UserId: "harness-hybrid-user",
    OrchestrationId: "harness-hybrid-orchestration",
    Properties: null,
    Workspace: workspace);
using var executionScope = contextAccessor.BeginScope(executionContext);

var bindingCapture = HarnessExecutionBinding.Capture(
    contextAccessor,
    SessionId,
    requireWorkspace: true);
if (bindingCapture.Status != HarnessExecutionBindingStatus.Valid ||
    bindingCapture.Binding is null)
{
    Console.Error.WriteLine(bindingCapture.Detail);
    return 1;
}

if (!AgentFrameworkGeneratedBootstrap.TryGetAIFunctionProvider(out var provider))
{
    Console.Error.WriteLine("The source-generated function provider was unavailable.");
    return 2;
}

var generatedTools = new HarnessGeneratedToolSource(provider, runServices)
    .Resolve([typeof(HybridWorkspaceTool)]);
if (generatedTools.Status != HarnessGeneratedToolResolutionStatus.Success)
{
    Console.Error.WriteLine($"Generated tool resolution failed: {generatedTools.Status}.");
    return 3;
}

var requestedCapabilities = new HashSet<HarnessCapability>
{
    HarnessCapability.GeneratedTools,
    HarnessCapability.FunctionInvocation,
    HarnessCapability.MessageInjection,
    HarnessCapability.OpenTelemetry,
};
var profile = new HarnessCapabilityResolver().Resolve(
    new HarnessCapabilityResolutionRequest(
        ProfileId: "harness-hybrid-stable-selected-provider",
        Lane: HarnessConstructionLane.SelectedProviders,
        Acceptance: HarnessCapabilityAcceptance.StableOnly,
        EvidenceThroughPhase: HarnessDeliveryPhase.G2,
        RequestedCapabilities: requestedCapabilities,
        ProviderCapabilities: new HashSet<HarnessProviderCapability>(),
        ToolLoopOwner: HarnessToolLoopOwner.Foundry,
        TelemetryOwner: HarnessTelemetryOwner.Foundry,
        HistoryPersistenceMode: HarnessHistoryPersistenceMode.NotApplicable));
if (!profile.IsExecutable)
{
    Console.Error.WriteLine("The stable selected-provider profile was not executable.");
    return 4;
}

var progressSink = new HybridProgressSink();
var progressFactory = services.GetRequiredService<IProgressReporterFactory>();
var progressAccessor = services.GetRequiredService<IProgressReporterAccessor>();
var progressReporter = progressFactory.Create(
    "harness-hybrid-workflow",
    [progressSink]);
using var progressScope = progressAccessor.BeginScope(progressReporter);

var composition = new HarnessProviderComposition().Compose(
    new HarnessProviderCompositionRequest(
        ChatClient: new HybridScriptedChatClient(
            generatedTools.Functions.Single().Name),
        Services: runServices,
        LoggerFactory: NullLoggerFactory.Instance,
        Name: "Harness Hybrid Stable Example",
        Description: "Deterministic non-Azure selected-provider Harness example.",
        Instructions: "Use the generated workspace tool exactly once.",
        Profile: profile,
        HybridProfile: null,
        GeneratedTools: generatedTools,
        ExecutionBinding: bindingCapture.Binding,
        ExecutionContextAccessor: contextAccessor,
        SessionId: SessionId,
        HistoryProvider: null,
        PlanningProviders: null,
        ApprovalPlugin: null,
        SkillsPlugin: null,
        WebSearchPlugin: null,
        Metrics: services.GetRequiredService<IAgentMetrics>(),
        ProgressAccessor: progressAccessor,
        OffloadPlugin: null));
if (composition.Status != HarnessProviderCompositionStatus.Success ||
    composition.Agent is null)
{
    Console.Error.WriteLine(
        composition.Detail
        ?? $"Composition failed with status {composition.Status}.");
    return 5;
}

var agent = composition.Agent;
var session = await agent.CreateSessionAsync();
var response = await agent.RunAsync(
    "Persist the deterministic selected-provider proof.",
    session);
string? responseText = response.GetText();
var output = workspace.TryReadFile(HybridWorkspaceTool.OutputPath);
if (!output.Success ||
    !string.Equals(
        output.Value.Content,
        HybridWorkspaceTool.ExpectedContent,
        StringComparison.Ordinal) ||
    !string.Equals(responseText, ExpectedResponse, StringComparison.Ordinal))
{
    Console.Error.WriteLine("The selected-provider workspace/result proof failed.");
    return 6;
}

if (profile.Capabilities[HarnessCapability.Compaction].EffectiveState !=
        HarnessCapabilityState.Disabled ||
    profile.ToolLoopOwner != HarnessToolLoopOwner.Foundry ||
    profile.TelemetryOwner != HarnessTelemetryOwner.Foundry)
{
    Console.Error.WriteLine("The stable profile ownership or hybrid disposition was incorrect.");
    return 7;
}

if (!progressSink.Events.OfType<LlmCallCompletedEvent>().Any() ||
    !progressSink.Events.OfType<ToolCallCompletedEvent>().Any())
{
    Console.Error.WriteLine("Foundry diagnostics progress was not observed.");
    return 8;
}

(agent as IDisposable)?.Dispose();
Console.WriteLine(
    $"HarnessHybridApp:{SessionId}:{responseText}:" +
    $"Compaction={profile.Capabilities[HarnessCapability.Compaction].EffectiveState}");
return 0;
