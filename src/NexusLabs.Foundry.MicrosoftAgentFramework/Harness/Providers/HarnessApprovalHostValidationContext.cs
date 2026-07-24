namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Providers;

/// <summary>
/// Stable, non-MAF identifiers describing the run being authorized when
/// <see cref="HarnessApprovalHostValidator"/> is invoked. Deliberately does not carry any
/// MAF approval type (request/response content, <c>ToolApprovalState</c>, etc.) so host
/// validators never depend on upstream MAF surface.
/// </summary>
/// <param name="UserId">The trusted user identity from the active execution binding.</param>
/// <param name="OrchestrationId">The trusted orchestration identity from the active execution binding.</param>
/// <param name="SessionId">The trusted session identifier from the active execution binding.</param>
/// <param name="Reason">Why reauthorization is being requested for this run.</param>
/// <param name="ToolName">
/// The tool name the standing approval would apply to, when known; <see langword="null"/>
/// when reauthorizing a restored session for which no specific tool name could be
/// determined from the inbound messages.
/// </param>
internal sealed record HarnessApprovalHostValidationContext(
    string UserId,
    string OrchestrationId,
    string SessionId,
    HarnessApprovalHostValidationReason Reason,
    string? ToolName);
