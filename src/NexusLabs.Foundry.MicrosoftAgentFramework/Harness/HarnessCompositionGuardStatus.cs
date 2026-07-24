namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness;

internal enum HarnessCompositionGuardStatus
{
    Valid,
    UnsupportedConstructionLane,
    ProfileNotExecutable,
    CapabilityOutsideCompositionPhase,
    FunctionInvocationDisabled,
    ExistingFunctionInvocationLoop,
    ExistingMessageInjection,
    UnsupportedOwnerCombination,
    TelemetryOwnerConflict,
    UnexpectedTelemetry,
}
