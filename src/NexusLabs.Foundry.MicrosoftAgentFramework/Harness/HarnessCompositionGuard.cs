using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

using NexusLabs.Foundry.MicrosoftAgentFramework.Diagnostics;
using NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Capabilities;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness;

internal static class HarnessCompositionGuard
{
    /// <summary>
    /// The base set of capabilities the shared composition root supports with no
    /// selected-provider plugin enabled. Composition roots that opt a coherent plugin in
    /// (history, planning, ...) union this base set with the capabilities the plugin
    /// contributes rather than maintaining a separate combinatorial static set per
    /// plugin combination; see <see cref="HarnessProviderComposition"/>.
    /// </summary>
    internal static readonly IReadOnlySet<HarnessCapability> G2SupportedCapabilities =
        new HashSet<HarnessCapability>
        {
            HarnessCapability.GeneratedTools,
            HarnessCapability.FunctionInvocation,
            HarnessCapability.MessageInjection,
            HarnessCapability.OpenTelemetry,
        };

    internal static HarnessCompositionGuardResult Validate(
        IChatClient chatClient,
        HarnessCapabilityProfile profile) =>
        Validate(chatClient, profile, G2SupportedCapabilities);

    internal static HarnessCompositionGuardResult Validate(
        IChatClient chatClient,
        HarnessCapabilityProfile profile,
        IReadOnlySet<HarnessCapability> supportedCapabilities)
    {
        ArgumentNullException.ThrowIfNull(chatClient);
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(supportedCapabilities);

        if (profile.Lane != HarnessConstructionLane.SelectedProviders)
        {
            return Failure(
                HarnessCompositionGuardStatus.UnsupportedConstructionLane,
                "G2 composition supports only the selected-provider lane.");
        }

        if (!profile.IsExecutable)
        {
            return Failure(
                HarnessCompositionGuardStatus.ProfileNotExecutable,
                "The capability profile is not executable at its evidence phase.");
        }

        var laterCapability = profile.Capabilities.Values.FirstOrDefault(evidence =>
            evidence.EffectiveState == HarnessCapabilityState.Enabled &&
            !supportedCapabilities.Contains(evidence.Capability));
        if (laterCapability is not null)
        {
            return Failure(
                HarnessCompositionGuardStatus.CapabilityOutsideCompositionPhase,
                $"Capability '{laterCapability.Capability}' belongs to " +
                $"'{laterCapability.DeliveryPhase}' and is not supported by this " +
                "composition root.");
        }

        if (profile.Capabilities[HarnessCapability.FunctionInvocation].EffectiveState !=
            HarnessCapabilityState.Enabled)
        {
            return Failure(
                HarnessCompositionGuardStatus.FunctionInvocationDisabled,
                "The selected composition requires function invocation.");
        }

        if (chatClient.GetService<FunctionInvokingChatClient>() is not null)
        {
            return Failure(
                HarnessCompositionGuardStatus.ExistingFunctionInvocationLoop,
                "The supplied chat client already contains a function invocation loop.");
        }

        if (chatClient.GetService<MessageInjectingChatClient>() is not null)
        {
            return Failure(
                HarnessCompositionGuardStatus.ExistingMessageInjection,
                "The supplied chat client already contains message injection middleware.");
        }

        var telemetryState =
            profile.Capabilities[HarnessCapability.OpenTelemetry].EffectiveState;
        var hasUpstreamTelemetry =
            chatClient.GetService<OpenTelemetryChatClient>() is not null;
        var hasFoundryTelemetry =
            chatClient.GetService<DiagnosticsRecordingChatClient>() is not null;

        if (telemetryState != HarnessCapabilityState.Enabled)
        {
            return hasUpstreamTelemetry || hasFoundryTelemetry
                ? Failure(
                    HarnessCompositionGuardStatus.UnexpectedTelemetry,
                    "Telemetry is present while the capability profile disables it.")
                : Valid();
        }

        var ownersAlign =
            profile.ToolLoopOwner == HarnessToolLoopOwner.Harness &&
            profile.TelemetryOwner == HarnessTelemetryOwner.Harness ||
            profile.ToolLoopOwner == HarnessToolLoopOwner.Foundry &&
            profile.TelemetryOwner == HarnessTelemetryOwner.Foundry;
        if (!ownersAlign)
        {
            return Failure(
                HarnessCompositionGuardStatus.UnsupportedOwnerCombination,
                "G2 requires tool-loop and telemetry ownership to align.");
        }

        return hasUpstreamTelemetry || hasFoundryTelemetry
            ? Failure(
                HarnessCompositionGuardStatus.TelemetryOwnerConflict,
                "The supplied chat client already contains a telemetry owner.")
            : Valid();
    }

    private static HarnessCompositionGuardResult Valid() =>
        new(HarnessCompositionGuardStatus.Valid, null);

    private static HarnessCompositionGuardResult Failure(
        HarnessCompositionGuardStatus status,
        string detail) =>
        new(status, detail);
}
