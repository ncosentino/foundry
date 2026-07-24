namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness;

internal enum HarnessProviderCompositionStatus
{
    Success,
    ExecutionBindingInvalid,
    CompositionGuardRejected,
    GeneratedToolResolutionFailed,
    GeneratedToolsEmpty,
    GeneratedToolsNotEnabled,
    FunctionInvocationLoopMissing,
    MessageInjectionMissing,
    FoundryMetricsMissing,
}
