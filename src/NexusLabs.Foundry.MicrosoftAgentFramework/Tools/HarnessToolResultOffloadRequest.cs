using NexusLabs.Foundry.MicrosoftAgentFramework.Context;
using NexusLabs.Foundry.MicrosoftAgentFramework.Harness;
using NexusLabs.Foundry.MicrosoftAgentFramework.Progress;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Tools;

/// <summary>
/// Input to one <see cref="HarnessToolResultOffloadTransform.Transform"/> call: the raw tool
/// result together with everything the shared, caller-agnostic transform needs to decide whether
/// to inline it or offload it to the workspace. Used identically by both
/// <c>IterativeAgentLoop</c> and selected-provider <c>HarnessProviderComposition</c>'s FICC
/// <c>FunctionInvoker</c> — this record is the single contract that makes the transform
/// caller-agnostic.
/// </summary>
/// <param name="RawResult">
/// The exact, unmodified object a tool/function invocation returned. Never itself mutated or
/// re-invoked by the transform.
/// </param>
/// <param name="ToolName">The name of the tool/function that produced <paramref name="RawResult"/>.</param>
/// <param name="CallId">The call ID of the tool invocation that produced <paramref name="RawResult"/>.</param>
/// <param name="ExecutionBinding">
/// The trusted, previously-captured <see cref="HarnessExecutionBinding"/> to revalidate and use for
/// any workspace access, or <see langword="null"/> if no binding could be captured (in which case
/// the transform still inlines values at or under the threshold, but fails closed on oversized
/// values rather than ever inlining/truncating/discarding them).
/// </param>
/// <param name="ExecutionContextAccessor">
/// The accessor used to revalidate <paramref name="ExecutionBinding"/> against the current ambient
/// execution context, or <see langword="null"/> if none is available.
/// </param>
/// <param name="Policy">The required, explicit offload policy driving this decision.</param>
/// <param name="CreatedAtUtc">The timestamp to record if a fresh artifact reference is constructed.</param>
/// <param name="CancellationToken">The cancellation token for this transform call.</param>
/// <param name="ProgressAccessor">
/// The required, explicit accessor used to report exactly one
/// <see cref="HarnessArtifactOffloadDecisionEvent"/> for this decision, or <see langword="null"/>
/// when no progress reporting is wired for this caller. When non-null but no scope is currently
/// active, resolving <see cref="IProgressReporterAccessor.Current"/> safely yields a no-op reporter
/// — no exception, no event.
/// </param>
internal sealed record HarnessToolResultOffloadRequest(
    object? RawResult,
    string ToolName,
    string CallId,
    HarnessExecutionBinding? ExecutionBinding,
    IAgentExecutionContextAccessor? ExecutionContextAccessor,
    HarnessToolResultOffloadPolicy Policy,
    DateTimeOffset CreatedAtUtc,
    CancellationToken CancellationToken,
    IProgressReporterAccessor? ProgressAccessor);
