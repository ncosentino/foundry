using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

using NexusLabs.Foundry.MicrosoftAgentFramework.Context;
using NexusLabs.Foundry.MicrosoftAgentFramework.Diagnostics;
using NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Capabilities;
using NexusLabs.Foundry.MicrosoftAgentFramework.Progress;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness;

internal sealed record HarnessProviderCompositionRequest(
    IChatClient ChatClient,
    IServiceProvider Services,
    ILoggerFactory LoggerFactory,
    string Name,
    string Description,
    string Instructions,
    HarnessCapabilityProfile Profile,
    HarnessGeneratedToolResolution GeneratedTools,
    HarnessExecutionBinding ExecutionBinding,
    IAgentExecutionContextAccessor ExecutionContextAccessor,
    string SessionId,
    IAgentMetrics? Metrics,
    IProgressReporterAccessor? ProgressAccessor);
