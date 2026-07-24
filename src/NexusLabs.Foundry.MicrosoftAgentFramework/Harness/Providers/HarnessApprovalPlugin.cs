using Microsoft.Agents.AI;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Providers;

/// <summary>
/// Narrow, immutable selected-provider slice describing which of the three independent
/// MAF 1.15 tool-approval capabilities were selected: <c>ApprovalResponseBinding</c>,
/// <c>ApprovalNotRequiredBypassing</c>, and <c>ToolAutoApproval</c>. Each capability is
/// independently opt-in: constructing this plugin never implicitly enables a capability
/// the caller did not select, and <see cref="HarnessApprovalCompositionGuard"/> is solely
/// responsible for checking the supplied plugin against the capability profile before any
/// capability is composed into an agent by <see cref="HarnessProviderComposition"/>.
/// </summary>
internal sealed class HarnessApprovalPlugin
{
    private HarnessApprovalPlugin(
        bool responseBindingEnabled,
        bool notRequiredBypassingEnabled,
        ToolApprovalAgentOptions? toolApprovalOptions,
        HarnessApprovalHostValidator? hostValidator)
    {
        ResponseBindingEnabled = responseBindingEnabled;
        NotRequiredBypassingEnabled = notRequiredBypassingEnabled;
        ToolApprovalOptions = toolApprovalOptions;
        HostValidator = hostValidator;
    }

    /// <summary>
    /// Whether the caller selected the <c>ApprovalResponseBinding</c> capability, which
    /// binds inbound <c>ToolApprovalResponseContent</c> to the request it answers via
    /// MAF's public <c>ChatClientBuilderExtensions.UseApprovalResponseBinding</c> seam.
    /// </summary>
    internal bool ResponseBindingEnabled { get; }

    /// <summary>
    /// Whether the caller selected the <c>ApprovalNotRequiredBypassing</c> capability,
    /// which short-circuits approval prompts for tools that do not require approval via
    /// MAF's public <c>ChatClientBuilderExtensions.UseApprovalNotRequiredFunctionBypassing</c>
    /// seam.
    /// </summary>
    internal bool NotRequiredBypassingEnabled { get; }

    /// <summary>
    /// MAF's <c>ToolApprovalAgentOptions</c> configuring the standing-approval agent, or
    /// <see langword="null"/> when the <c>ToolAutoApproval</c> capability was not selected.
    /// Never populated as a side effect of selecting the other two approval capabilities.
    /// </summary>
    internal ToolApprovalAgentOptions? ToolApprovalOptions { get; }

    /// <summary>
    /// The required host reauthorization delegate for standing ("always approve") tool
    /// approvals. Non-<see langword="null"/> exactly when <see cref="ToolApprovalOptions"/>
    /// is non-<see langword="null"/>.
    /// </summary>
    internal HarnessApprovalHostValidator? HostValidator { get; }

    /// <summary>
    /// Creates an approval plugin from the capabilities the caller selected. At least one
    /// of <paramref name="responseBindingEnabled"/>, <paramref name="notRequiredBypassingEnabled"/>,
    /// or a non-<see langword="null"/> <paramref name="toolApprovalOptions"/> must be
    /// supplied; a plugin contributing nothing must not be constructed. Selecting
    /// <c>ToolAutoApproval</c> (a non-<see langword="null"/> <paramref name="toolApprovalOptions"/>)
    /// requires a non-<see langword="null"/> <paramref name="hostValidator"/>, and the
    /// converse: a host validator is only meaningful alongside <c>ToolAutoApproval</c>.
    /// </summary>
    internal static HarnessApprovalPlugin Create(
        bool responseBindingEnabled,
        bool notRequiredBypassingEnabled,
        ToolApprovalAgentOptions? toolApprovalOptions,
        HarnessApprovalHostValidator? hostValidator)
    {
        if (!responseBindingEnabled &&
            !notRequiredBypassingEnabled &&
            toolApprovalOptions is null)
        {
            throw new InvalidOperationException(
                "At least one approval capability must be selected to construct an " +
                "approval plugin.");
        }

        if (toolApprovalOptions is not null && hostValidator is null)
        {
            throw new InvalidOperationException(
                "The ToolAutoApproval capability requires a host reauthorization " +
                "validator.");
        }

        if (toolApprovalOptions is null && hostValidator is not null)
        {
            throw new InvalidOperationException(
                "A host reauthorization validator was supplied without the " +
                "ToolAutoApproval capability's ToolApprovalAgentOptions.");
        }

        return new HarnessApprovalPlugin(
            responseBindingEnabled,
            notRequiredBypassingEnabled,
            toolApprovalOptions,
            hostValidator);
    }
}
