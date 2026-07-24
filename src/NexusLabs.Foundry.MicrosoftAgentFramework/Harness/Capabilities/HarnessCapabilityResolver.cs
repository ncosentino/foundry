using System.Collections.ObjectModel;

using Microsoft.Agents.AI;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Capabilities;

internal sealed class HarnessCapabilityResolver
{
    internal const int SchemaVersion = 1;
    internal const string MiddlewareOrderVersion = "maf-1.15.0";
    internal static string MafVersion { get; } =
        typeof(AIAgent).Assembly.GetName().Version?.ToString(3) ??
        "unknown";

    private const string MafPackage = "Microsoft.Agents.AI";
    private const string FoundryPackage = "NexusLabs.Foundry.MicrosoftAgentFramework";

    private static readonly IReadOnlyList<CapabilityDefinition> Definitions =
    [
        Stable(HarnessCapability.GeneratedTools, FoundryPackage, false, HarnessCapabilityTrustBoundary.HostIdentity, HarnessCapabilityAotStatus.Verified, HarnessCapabilityDiagnosticsStatus.Available, HarnessDeliveryPhase.G2),
        Stable(HarnessCapability.FunctionInvocation, MafPackage, true, HarnessCapabilityTrustBoundary.None, HarnessCapabilityAotStatus.Verified, HarnessCapabilityDiagnosticsStatus.Available, HarnessDeliveryPhase.G2),
        Stable(HarnessCapability.MessageInjection, MafPackage, true, HarnessCapabilityTrustBoundary.HostIdentity, HarnessCapabilityAotStatus.Verified, HarnessCapabilityDiagnosticsStatus.Partial, HarnessDeliveryPhase.G2),
        Stable(HarnessCapability.PerServiceHistory, MafPackage, true, HarnessCapabilityTrustBoundary.HostIdentity, HarnessCapabilityAotStatus.Verified, HarnessCapabilityDiagnosticsStatus.Available, HarnessDeliveryPhase.G3),
        Stable(HarnessCapability.ApprovalResponseBinding, MafPackage, true, HarnessCapabilityTrustBoundary.Approval, HarnessCapabilityAotStatus.Verified, HarnessCapabilityDiagnosticsStatus.Partial, HarnessDeliveryPhase.G3),
        Stable(HarnessCapability.ApprovalNotRequiredBypassing, MafPackage, true, HarnessCapabilityTrustBoundary.Approval, HarnessCapabilityAotStatus.Verified, HarnessCapabilityDiagnosticsStatus.Partial, HarnessDeliveryPhase.G3),
        Stable(HarnessCapability.ToolAutoApproval, MafPackage, true, HarnessCapabilityTrustBoundary.Approval, HarnessCapabilityAotStatus.Verified, HarnessCapabilityDiagnosticsStatus.Partial, HarnessDeliveryPhase.G3),
        Stable(HarnessCapability.Todo, MafPackage, true, HarnessCapabilityTrustBoundary.None, HarnessCapabilityAotStatus.Unverified, HarnessCapabilityDiagnosticsStatus.Partial, HarnessDeliveryPhase.G3),
        Stable(HarnessCapability.AgentMode, MafPackage, true, HarnessCapabilityTrustBoundary.None, HarnessCapabilityAotStatus.Unverified, HarnessCapabilityDiagnosticsStatus.Partial, HarnessDeliveryPhase.G3),
        Stable(HarnessCapability.FileMemory, MafPackage, true, HarnessCapabilityTrustBoundary.FileSystem, HarnessCapabilityAotStatus.Unverified, HarnessCapabilityDiagnosticsStatus.Partial, HarnessDeliveryPhase.G4),
        Experimental(HarnessCapability.FileAccess, MafPackage, false, HarnessCapabilityTrustBoundary.FileSystem, HarnessCapabilityAotStatus.Unverified, HarnessCapabilityDiagnosticsStatus.Partial, HarnessDeliveryPhase.G4),
        Stable(HarnessCapability.Skills, MafPackage, true, HarnessCapabilityTrustBoundary.ExternalContent, HarnessCapabilityAotStatus.Unverified, HarnessCapabilityDiagnosticsStatus.Partial, HarnessDeliveryPhase.G3),
        ProviderDependent(HarnessCapability.WebSearch, MafPackage, true, HarnessProviderCapability.HostedWebSearch, HarnessCapabilityTrustBoundary.ExternalContent, HarnessCapabilityAotStatus.Unverified, HarnessCapabilityDiagnosticsStatus.Partial, HarnessDeliveryPhase.G3),
        Stable(HarnessCapability.OpenTelemetry, MafPackage, true, HarnessCapabilityTrustBoundary.None, HarnessCapabilityAotStatus.Compatible, HarnessCapabilityDiagnosticsStatus.Available, HarnessDeliveryPhase.G2),
        Experimental(HarnessCapability.Compaction, MafPackage, false, HarnessCapabilityTrustBoundary.None, HarnessCapabilityAotStatus.Verified, HarnessCapabilityDiagnosticsStatus.Available, HarnessDeliveryPhase.G5),
        Experimental(HarnessCapability.BackgroundAgents, MafPackage, false, HarnessCapabilityTrustBoundary.HostIdentity, HarnessCapabilityAotStatus.Unverified, HarnessCapabilityDiagnosticsStatus.Partial, HarnessDeliveryPhase.G6),
        Experimental(HarnessCapability.LoopEvaluation, MafPackage, false, HarnessCapabilityTrustBoundary.None, HarnessCapabilityAotStatus.Unverified, HarnessCapabilityDiagnosticsStatus.Partial, HarnessDeliveryPhase.G6),
    ];

    internal HarnessCapabilityProfile Resolve(
        HarnessCapabilityResolutionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ProfileId);
        ArgumentNullException.ThrowIfNull(request.RequestedCapabilities);
        ArgumentNullException.ThrowIfNull(request.ProviderCapabilities);

        var capabilities = new Dictionary<HarnessCapability, HarnessCapabilityEvidence>();
        foreach (var definition in Definitions)
        {
            capabilities.Add(
                definition.Capability,
                ResolveCapability(definition, request));
        }

        var activeDefinitions = Definitions
            .Where(definition =>
                request.RequestedCapabilities.Contains(definition.Capability) ||
                request.Lane == HarnessConstructionLane.CompleteBundle &&
                definition.DefaultEnabledInBundle)
            .ToList();
        var isExecutable = activeDefinitions.Count > 0 &&
            activeDefinitions.All(definition =>
                capabilities[definition.Capability].EffectiveState ==
                HarnessCapabilityState.Enabled);

        var historyCoherent = ResolveHistoryCoherence(request, capabilities);
        isExecutable = isExecutable && historyCoherent;

        return new HarnessCapabilityProfile(
            SchemaVersion,
            request.ProfileId,
            request.Lane,
            MafVersion,
            MiddlewareOrderVersion,
            request.EvidenceThroughPhase,
            isExecutable,
            new ReadOnlyDictionary<HarnessCapability, HarnessCapabilityEvidence>(capabilities),
            request.ToolLoopOwner,
            request.TelemetryOwner,
            request.HistoryPersistenceMode);
    }

    /// <summary>
    /// Applies the session-continuity coherence rule on top of the per-capability resolution:
    /// a selected <see cref="HarnessCapability.PerServiceHistory"/> capability must carry an
    /// explicit, supported persistence mode to be enabled, and a persistence mode must not be
    /// requested unless the capability itself is selected. Returns whether the history slice
    /// (if any) is coherent with the rest of the profile.
    /// </summary>
    private static bool ResolveHistoryCoherence(
        HarnessCapabilityResolutionRequest request,
        Dictionary<HarnessCapability, HarnessCapabilityEvidence> capabilities)
    {
        var historyEvidence = capabilities[HarnessCapability.PerServiceHistory];
        var historySelected = historyEvidence.Requested ||
            request.Lane == HarnessConstructionLane.CompleteBundle &&
                historyEvidence.DefaultEnabledInBundle;

        if (!historySelected)
        {
            // A persistence mode without the capability selected must never produce an
            // executable profile: there is nothing for the mode to apply to.
            return request.HistoryPersistenceMode == HarnessHistoryPersistenceMode.NotApplicable;
        }

        if (request.HistoryPersistenceMode == HarnessHistoryPersistenceMode.ServiceManaged)
        {
            capabilities[HarnessCapability.PerServiceHistory] = historyEvidence with
            {
                EffectiveState = HarnessCapabilityState.Deferred,
                Rationale = "MAF 1.15 supports service-stored conversation history, but " +
                    "this selected-provider slice has no approved provider-specific " +
                    "capability evidence and performs no runtime negotiation.",
            };
            return false;
        }

        if (historyEvidence.EffectiveState != HarnessCapabilityState.Enabled)
        {
            // Deferred/disabled for an unrelated reason (evidence phase, experimental
            // acceptance, ...); the history-specific coherence rule does not apply.
            return true;
        }

        if (request.HistoryPersistenceMode == HarnessHistoryPersistenceMode.NotApplicable)
        {
            capabilities[HarnessCapability.PerServiceHistory] = historyEvidence with
            {
                EffectiveState = HarnessCapabilityState.Deferred,
                Rationale = "A selected PerServiceHistory capability requires an explicit " +
                    "in-memory, serialized, or durable-provider persistence mode.",
            };
            return false;
        }

        var persistenceRationale = request.HistoryPersistenceMode switch
        {
            HarnessHistoryPersistenceMode.InMemory =>
                "non-durable in-memory state.",
            HarnessHistoryPersistenceMode.Serialized =>
                "serializable state whose durability depends on caller-owned persistence.",
            HarnessHistoryPersistenceMode.DurableProvider =>
                "a caller-supplied durable provider.",
            _ => throw new ArgumentOutOfRangeException(
                nameof(request.HistoryPersistenceMode),
                request.HistoryPersistenceMode,
                null),
        };
        capabilities[HarnessCapability.PerServiceHistory] = historyEvidence with
        {
            Rationale = historyEvidence.Rationale +
                $" Persistence mode '{request.HistoryPersistenceMode}' uses " +
                persistenceRationale,
        };
        return true;
    }

    private static HarnessCapabilityEvidence ResolveCapability(
        CapabilityDefinition definition,
        HarnessCapabilityResolutionRequest request)
    {
        var requested = request.RequestedCapabilities.Contains(definition.Capability);
        var selected = request.Lane == HarnessConstructionLane.CompleteBundle
            ? definition.DefaultEnabledInBundle || requested
            : requested;

        var effectiveState = selected
            ? HarnessCapabilityState.Enabled
            : HarnessCapabilityState.Disabled;
        var rationale = selected
            ? request.Lane == HarnessConstructionLane.CompleteBundle && definition.DefaultEnabledInBundle
                ? "Enabled by the complete-bundle default."
                : "Enabled by explicit selected-provider request."
            : "Not selected.";

        if (selected &&
            request.Lane == HarnessConstructionLane.CompleteBundle &&
            request.EvidenceThroughPhase < HarnessDeliveryPhase.G6)
        {
            effectiveState = HarnessCapabilityState.Deferred;
            rationale = "The complete-bundle lane has not passed the G6 delivery gate.";
        }
        else if (selected &&
            definition.DeliveryPhase > request.EvidenceThroughPhase)
        {
            effectiveState = HarnessCapabilityState.Deferred;
            rationale = $"Capability evidence is not available through {definition.DeliveryPhase}.";
        }
        else if (selected &&
            definition.Stability == HarnessCapabilityStability.Experimental &&
            request.Acceptance != HarnessCapabilityAcceptance.StableAndExperimental)
        {
            effectiveState = HarnessCapabilityState.Disabled;
            rationale = "The capability is experimental and explicit experimental acceptance was not provided.";
        }
        else if (selected &&
            definition.RequiredProviderCapability is HarnessProviderCapability requiredProvider &&
            !request.ProviderCapabilities.Contains(requiredProvider))
        {
            effectiveState = HarnessCapabilityState.Deferred;
            rationale = $"Provider capability '{requiredProvider}' has not been proven.";
        }

        var sourceVersion = definition.SourcePackage == FoundryPackage
            ? GetFoundryVersion()
            : MafVersion;

        return new HarnessCapabilityEvidence(
            definition.Capability,
            definition.SourcePackage,
            sourceVersion,
            definition.Stability,
            definition.DefaultEnabledInBundle,
            requested,
            effectiveState,
            definition.RequiredProviderCapability,
            definition.TrustBoundary,
            definition.AotStatus,
            definition.DiagnosticsStatus,
            definition.DeliveryPhase,
            rationale);
    }

    private static string GetFoundryVersion() =>
        typeof(HarnessCapabilityResolver).Assembly.GetName().Version?.ToString() ??
        "unknown";

    private static CapabilityDefinition Stable(
        HarnessCapability capability,
        string sourcePackage,
        bool defaultEnabledInBundle,
        HarnessCapabilityTrustBoundary trustBoundary,
        HarnessCapabilityAotStatus aotStatus,
        HarnessCapabilityDiagnosticsStatus diagnosticsStatus,
        HarnessDeliveryPhase deliveryPhase) =>
        new(
            capability,
            sourcePackage,
            HarnessCapabilityStability.Stable,
            defaultEnabledInBundle,
            null,
            trustBoundary,
            aotStatus,
            diagnosticsStatus,
            deliveryPhase);

    private static CapabilityDefinition Experimental(
        HarnessCapability capability,
        string sourcePackage,
        bool defaultEnabledInBundle,
        HarnessCapabilityTrustBoundary trustBoundary,
        HarnessCapabilityAotStatus aotStatus,
        HarnessCapabilityDiagnosticsStatus diagnosticsStatus,
        HarnessDeliveryPhase deliveryPhase) =>
        new(
            capability,
            sourcePackage,
            HarnessCapabilityStability.Experimental,
            defaultEnabledInBundle,
            null,
            trustBoundary,
            aotStatus,
            diagnosticsStatus,
            deliveryPhase);

    private static CapabilityDefinition ProviderDependent(
        HarnessCapability capability,
        string sourcePackage,
        bool defaultEnabledInBundle,
        HarnessProviderCapability requiredProviderCapability,
        HarnessCapabilityTrustBoundary trustBoundary,
        HarnessCapabilityAotStatus aotStatus,
        HarnessCapabilityDiagnosticsStatus diagnosticsStatus,
        HarnessDeliveryPhase deliveryPhase) =>
        new(
            capability,
            sourcePackage,
            HarnessCapabilityStability.ProviderDependent,
            defaultEnabledInBundle,
            requiredProviderCapability,
            trustBoundary,
            aotStatus,
            diagnosticsStatus,
            deliveryPhase);

    private sealed record CapabilityDefinition(
        HarnessCapability Capability,
        string SourcePackage,
        HarnessCapabilityStability Stability,
        bool DefaultEnabledInBundle,
        HarnessProviderCapability? RequiredProviderCapability,
        HarnessCapabilityTrustBoundary TrustBoundary,
        HarnessCapabilityAotStatus AotStatus,
        HarnessCapabilityDiagnosticsStatus DiagnosticsStatus,
        HarnessDeliveryPhase DeliveryPhase);
}
