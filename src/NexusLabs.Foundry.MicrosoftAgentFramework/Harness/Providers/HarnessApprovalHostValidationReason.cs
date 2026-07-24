namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Providers;

/// <summary>
/// Why <see cref="HarnessApprovalHostValidator"/> is being invoked for a given run.
/// </summary>
internal enum HarnessApprovalHostValidationReason
{
    /// <summary>
    /// The inbound messages for this run contain a new
    /// <c>AlwaysApproveToolApprovalResponseContent</c> the caller is supplying for the
    /// first time; honoring it would create or extend a standing approval rule.
    /// </summary>
    NewlySuppliedStandingApproval,

    /// <summary>
    /// This run continues an existing session that could already carry a previously
    /// recorded standing approval rule from an earlier turn. MAF exposes no public way for
    /// Foundry to detect whether such a rule is actually present, so reauthorization is
    /// required conservatively for every continued session while ToolAutoApproval is
    /// enabled.
    /// </summary>
    ContinuedSessionReauthorization,
}
