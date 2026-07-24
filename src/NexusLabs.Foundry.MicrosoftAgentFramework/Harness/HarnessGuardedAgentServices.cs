using NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Providers;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness;

internal sealed record HarnessGuardedAgentServices(
    IHarnessMessageInjector? MessageInjector,
    IHarnessTodoAccessor? TodoAccessor,
    IHarnessAgentModeAccessor? AgentModeAccessor);
