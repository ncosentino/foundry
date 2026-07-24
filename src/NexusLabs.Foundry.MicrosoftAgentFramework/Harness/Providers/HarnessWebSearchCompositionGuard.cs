using Microsoft.Extensions.AI;

using NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Capabilities;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Providers;

/// <summary>
/// Focused profile/plugin coherence guard for the single provider-dependent
/// <c>WebSearch</c> capability. Beyond the capability-enabled/plugin-supplied symmetry
/// shared with the other single-plugin guards, this guard also rejects the request before
/// any agent is built if the hosted web search marker's tool name or runtime type would
/// collide with a generated tool (or, defensively, with another hosted marker), since
/// <see cref="HarnessProviderComposition"/> appends exactly one
/// <see cref="HostedWebSearchTool"/> to <c>ChatOptions.Tools</c> alongside the generated
/// tools and never adds a second hosted marker or a duplicate-named tool.
/// </summary>
/// <remarks>
/// This guard never inspects <c>ChatClientMetadata</c>, a provider or model name, or any
/// other runtime signal: whether <see cref="HarnessCapability.WebSearch"/> is
/// <see cref="HarnessCapabilityState.Enabled"/> is decided entirely by
/// <see cref="HarnessCapabilityResolver"/> from host-supplied
/// <see cref="HarnessProviderCapability.HostedWebSearch"/> evidence (see
/// <see cref="HarnessCapabilityResolutionRequest.ProviderCapabilities"/>), which this guard
/// only reads back from the already-resolved <see cref="HarnessCapabilityProfile"/>.
/// </remarks>
internal static class HarnessWebSearchCompositionGuard
{
    internal static HarnessWebSearchCompositionGuardResult Validate(
        HarnessCapabilityProfile profile,
        HarnessWebSearchPlugin? webSearchPlugin,
        IReadOnlyList<AIFunction> generatedFunctions)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(generatedFunctions);

        var webSearchEnabled =
            profile.Capabilities[HarnessCapability.WebSearch].EffectiveState ==
            HarnessCapabilityState.Enabled;

        if (!webSearchEnabled)
        {
            // Covers both "WebSearch was never requested" and "WebSearch was requested but
            // remains Deferred because no HostedWebSearch provider evidence was proven" --
            // in either case a supplied plugin would be unselected/non-executable and must
            // be rejected before any agent is built.
            return webSearchPlugin is null
                ? Valid()
                : Failure(
                    HarnessWebSearchCompositionGuardStatus.WebSearchPluginUnexpected,
                    "A web search plugin was supplied while the capability profile does " +
                    "not enable the WebSearch capability (it was either not selected, or " +
                    "remains Deferred because no host-supplied HostedWebSearch provider " +
                    "evidence was proven).");
        }

        if (webSearchPlugin is null)
        {
            return Failure(
                HarnessWebSearchCompositionGuardStatus.WebSearchPluginRequired,
                "The WebSearch capability is selected and enabled but no web search plugin " +
                "was supplied.");
        }

        var markerName = webSearchPlugin.Tool.Name;
        var markerType = webSearchPlugin.Tool.GetType();
        foreach (var function in generatedFunctions)
        {
            if (string.Equals(function.Name, markerName, StringComparison.Ordinal))
            {
                return Failure(
                    HarnessWebSearchCompositionGuardStatus.WebSearchToolNameCollision,
                    $"A generated tool named '{function.Name}' collides with the hosted " +
                    "web search marker's tool name.");
            }

            // Structurally unreachable through the public HarnessGeneratedToolResolution
            // surface today -- AIFunction and HostedWebSearchTool are disjoint AITool
            // subtypes, so no generated function can ever report this exact runtime type --
            // but this check is retained defensively so a future generated-tool or hosted-
            // marker source could never silently smuggle a duplicate hosted marker past
            // this guard.
            if (function.GetType() == markerType)
            {
                return Failure(
                    HarnessWebSearchCompositionGuardStatus.WebSearchToolTypeCollision,
                    $"A generated tool of type '{function.GetType()}' collides with the " +
                    "hosted web search marker's runtime type.");
            }
        }

        return Valid();
    }

    private static HarnessWebSearchCompositionGuardResult Valid() =>
        new(HarnessWebSearchCompositionGuardStatus.Valid, null);

    private static HarnessWebSearchCompositionGuardResult Failure(
        HarnessWebSearchCompositionGuardStatus status,
        string detail) =>
        new(status, detail);
}
