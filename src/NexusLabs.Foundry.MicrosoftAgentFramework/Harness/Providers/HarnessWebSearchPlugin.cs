using Microsoft.Extensions.AI;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Providers;

/// <summary>
/// Narrow, immutable selected-provider slice describing the single opt-in G3
/// <c>WebSearch</c> capability: the public MEAI 10.6 <see cref="HostedWebSearchTool"/>
/// marker appended to <c>ChatOptions.Tools</c> by <see cref="HarnessProviderComposition"/>.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="HostedWebSearchTool"/> "does not itself implement web searches" (per its own
/// upstream documentation): it is a marker MEAI defines so a caller can inform a service
/// that the service is allowed to perform web searches if the underlying provider is
/// capable of doing so. This plugin holds exactly that marker and nothing else -- no
/// Foundry-owned web search client, no Bing/Azure invocation, and no independent search
/// implementation exists anywhere in this type or in <see cref="HarnessProviderComposition"/>.
/// </para>
/// <para>
/// Whether the marker actually causes a real provider to perform web searches is entirely
/// a runtime decision made by the underlying MAF/MEAI <c>IChatClient</c> the host supplied --
/// this slice never inspects <c>ChatClientMetadata</c>, a provider or model name, or any
/// other runtime signal to decide whether to construct or compose this plugin. The sole,
/// explicit source of truth for whether hosted web search is coherent is host-supplied
/// <see cref="Capabilities.HarnessProviderCapability.HostedWebSearch"/> evidence passed into
/// <see cref="Capabilities.HarnessCapabilityResolutionRequest.ProviderCapabilities"/> by the
/// caller, resolved into <see cref="Capabilities.HarnessCapability.WebSearch"/>'s effective
/// state by <see cref="Capabilities.HarnessCapabilityResolver"/> (pre-existing, unmodified by
/// this slice) -- never inferred here.
/// </para>
/// <para>
/// Hosted web search is stateless from this library's perspective: <see cref="Tool"/>
/// contributes no <c>AgentSession.StateBag</c> keys and is not an <c>AIContextProvider</c>,
/// so composing this plugin alone must never add a provider state key or activate the
/// trusted schema-v2 session envelope; see <see cref="HarnessProviderComposition"/>'s
/// <c>sessionContinuityEnabled</c> computation, which deliberately omits this plugin.
/// </para>
/// </remarks>
internal sealed class HarnessWebSearchPlugin
{
    private HarnessWebSearchPlugin(HostedWebSearchTool tool)
    {
        Tool = tool;
    }

    /// <summary>
    /// The upstream hosted web search marker tool. Appended, unmodified, to
    /// <c>ChatOptions.Tools</c> by <see cref="HarnessProviderComposition"/> when this plugin
    /// is coherently composed; never exposed through
    /// <see cref="HarnessGuardedAgent.GetService"/>.
    /// </summary>
    internal HostedWebSearchTool Tool { get; }

    /// <summary>
    /// Creates a web search plugin wrapping a fresh, default-constructed
    /// <see cref="HostedWebSearchTool"/> marker.
    /// </summary>
    internal static HarnessWebSearchPlugin Create() =>
        new(new HostedWebSearchTool());

    /// <summary>
    /// Creates a web search plugin wrapping a caller-supplied <see cref="HostedWebSearchTool"/>
    /// marker, for callers that need to attach provider-specific
    /// <see cref="AITool.AdditionalProperties"/> to the upstream marker itself.
    /// </summary>
    internal static HarnessWebSearchPlugin Create(HostedWebSearchTool tool)
    {
        ArgumentNullException.ThrowIfNull(tool);
        return new HarnessWebSearchPlugin(tool);
    }
}
