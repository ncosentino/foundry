using Microsoft.Extensions.AI;

using NexusLabs.Foundry.MicrosoftAgentFramework.Context;
using NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Capabilities;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Context;

/// <summary>
/// One explicit invocation of <see cref="HarnessCompactionComposition.Compose"/>: the real provider
/// <see cref="IChatClient"/> to conditionally wrap, the resolved capability profile whose
/// <see cref="HarnessCapability.Compaction"/> evidence governs whether compaction is enabled at all, the
/// optional explicit <see cref="HarnessHybridProfile"/> opt-in, and the current execution
/// binding/session this call's installed node (if any) must validate against.
/// </summary>
internal sealed record HarnessCompactionCompositionRequest(
    IChatClient ChatClient,
    HarnessCapabilityProfile Profile,
    HarnessHybridProfile? HybridProfile,
    HarnessExecutionBinding ExecutionBinding,
    IAgentExecutionContextAccessor ExecutionContextAccessor,
    string SessionId);
