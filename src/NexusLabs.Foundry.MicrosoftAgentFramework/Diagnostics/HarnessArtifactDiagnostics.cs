using NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Context;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Diagnostics;

/// <summary>
/// Privacy-safe, structured evidence for one artifact offload or rehydration decision. Carries
/// only categorical classifications, byte counts, and a bounded reference identity — never an
/// artifact body, a serialized raw result, a workspace path, an owner user/orchestration/session
/// identity, or a raw exception message. Every instance is produced by <see cref="ForOffload"/> or
/// <see cref="ForRehydration"/>, each of which only accepts the outcome and reason values valid for
/// that operation, so a snapshot can never mix the two decisions' state machines.
/// </summary>
/// <remarks>
/// The same instance attached to an internal offload outcome or rehydration result is also the
/// instance carried by the corresponding progress event, so inspecting either surface observes
/// identical data.
/// </remarks>
public sealed record HarnessArtifactDiagnostics
{
    private static readonly HashSet<HarnessArtifactOutcomeCategory> OffloadOutcomes =
    [
        HarnessArtifactOutcomeCategory.Inline,
        HarnessArtifactOutcomeCategory.Offloaded,
        HarnessArtifactOutcomeCategory.ExistingReference,
        HarnessArtifactOutcomeCategory.Failed,
        HarnessArtifactOutcomeCategory.RecoveryRequired,
    ];

    private static readonly HashSet<HarnessArtifactOutcomeCategory> RehydrationOutcomes =
    [
        HarnessArtifactOutcomeCategory.Resolved,
        HarnessArtifactOutcomeCategory.Stale,
        HarnessArtifactOutcomeCategory.Missing,
        HarnessArtifactOutcomeCategory.Unauthorized,
        HarnessArtifactOutcomeCategory.OverBudget,
    ];

    private static readonly HashSet<HarnessArtifactDecisionReason> OffloadReasons =
    [
        HarnessArtifactDecisionReason.BelowThreshold,
        HarnessArtifactDecisionReason.RecoverableSegmentBypass,
        HarnessArtifactDecisionReason.ThresholdExceeded,
        HarnessArtifactDecisionReason.ExistingContentMatch,
        HarnessArtifactDecisionReason.NoAuthorizedWorkspace,
        HarnessArtifactDecisionReason.WorkspaceReadFailed,
        HarnessArtifactDecisionReason.ContentAddressMismatch,
        HarnessArtifactDecisionReason.WorkspaceWriteFailed,
        HarnessArtifactDecisionReason.CanceledAfterWrite,
        HarnessArtifactDecisionReason.CheckpointFailed,
    ];

    private static readonly HashSet<HarnessArtifactDecisionReason> RehydrationReasons =
    [
        HarnessArtifactDecisionReason.DigestVerified,
        HarnessArtifactDecisionReason.DigestMismatch,
        HarnessArtifactDecisionReason.Missing,
        HarnessArtifactDecisionReason.OwnerMismatch,
        HarnessArtifactDecisionReason.BudgetExceeded,
    ];

    private static readonly HashSet<HarnessArtifactOutcomeCategory> OffloadOutcomesRequiringReferenceId =
    [
        HarnessArtifactOutcomeCategory.Offloaded,
        HarnessArtifactOutcomeCategory.ExistingReference,
    ];

    private HarnessArtifactDiagnostics(
        HarnessArtifactOperationCategory operation,
        HarnessArtifactOutcomeCategory outcome,
        HarnessArtifactContentCategory content,
        HarnessArtifactDecisionReason reason,
        int? observedUtf8ByteSize,
        int configuredThresholdOrBudget,
        string? referenceId,
        HarnessContextAttribution attribution)
    {
        Operation = operation;
        Outcome = outcome;
        Content = content;
        Reason = reason;
        ObservedUtf8ByteSize = observedUtf8ByteSize;
        ConfiguredThresholdOrBudget = configuredThresholdOrBudget;
        ReferenceId = referenceId;
        Attribution = attribution;
    }

    /// <summary>Whether this snapshot describes an offload decision or a rehydration decision.</summary>
    public HarnessArtifactOperationCategory Operation { get; }

    /// <summary>The explicit outcome of the decision.</summary>
    public HarnessArtifactOutcomeCategory Outcome { get; }

    /// <summary>The kind of content the decision was made about.</summary>
    public HarnessArtifactContentCategory Content { get; }

    /// <summary>The stable, explicit reason category behind <see cref="Outcome"/>.</summary>
    public HarnessArtifactDecisionReason Reason { get; }

    /// <summary>
    /// The UTF-8 byte size actually observed/measured for this decision, or
    /// <see langword="null"/> when no content was ever measured (for example a rehydration
    /// <see cref="HarnessArtifactOutcomeCategory.Missing"/> or
    /// <see cref="HarnessArtifactOutcomeCategory.Unauthorized"/> outcome, where the workspace was
    /// never — or could never be — read).
    /// </summary>
    public int? ObservedUtf8ByteSize { get; }

    /// <summary>
    /// The configured inline byte threshold (offload) or maximum byte budget (rehydration) this
    /// decision was evaluated against. Always known regardless of outcome, because it is a
    /// caller-supplied input rather than an observation.
    /// </summary>
    public int ConfiguredThresholdOrBudget { get; }

    /// <summary>
    /// The bounded, model/history-facing artifact reference identity (<c>artifact://sha256/...</c>)
    /// this decision concerns, when one is available. Always available for a rehydration decision
    /// (the reference being resolved is always known upfront). Only available for an offload
    /// decision whose outcome committed or reused a reference
    /// (<see cref="HarnessArtifactOutcomeCategory.Offloaded"/> or
    /// <see cref="HarnessArtifactOutcomeCategory.ExistingReference"/>); <see langword="null"/> for
    /// every other offload outcome.
    /// </summary>
    public string? ReferenceId { get; }

    /// <summary>
    /// The privacy-safe UTF-8 byte attribution for this decision — how many bytes were observed
    /// coming in, and how many bytes actually became model/history-facing context as a result. The
    /// identical instance is also carried by the progress event and internal outcome/result this
    /// snapshot is attached to, since it rides along on this shared <see cref="HarnessArtifactDiagnostics"/>.
    /// </summary>
    public HarnessContextAttribution Attribution { get; }

    /// <summary>
    /// Builds a snapshot for one offload decision.
    /// </summary>
    /// <param name="outcome">
    /// The offload outcome. Must be one of <see cref="HarnessArtifactOutcomeCategory.Inline"/>,
    /// <see cref="HarnessArtifactOutcomeCategory.Offloaded"/>,
    /// <see cref="HarnessArtifactOutcomeCategory.ExistingReference"/>,
    /// <see cref="HarnessArtifactOutcomeCategory.Failed"/>, or
    /// <see cref="HarnessArtifactOutcomeCategory.RecoveryRequired"/>.
    /// </param>
    /// <param name="content">The kind of content the decision was made about.</param>
    /// <param name="reason">The reason category. Must be one of the offload-family reasons.</param>
    /// <param name="observedUtf8ByteSize">The UTF-8 byte size measured for this decision.</param>
    /// <param name="configuredThresholdBytes">The configured inline byte threshold.</param>
    /// <param name="referenceId">
    /// The committed or reused artifact reference identity, or <see langword="null"/> when the
    /// outcome never committed one.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="outcome"/> is not a valid offload outcome; <paramref name="reason"/> is not
    /// a valid offload reason; <paramref name="observedUtf8ByteSize"/> is negative; or
    /// <paramref name="configuredThresholdBytes"/> is not greater than zero.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="referenceId"/> is non-null but not a canonical
    /// <c>artifact://sha256/{64-lowercase-hex}</c> reference identity; <paramref name="outcome"/> is
    /// <see cref="HarnessArtifactOutcomeCategory.Offloaded"/> or
    /// <see cref="HarnessArtifactOutcomeCategory.ExistingReference"/> but <paramref name="referenceId"/>
    /// is <see langword="null"/>; or <paramref name="outcome"/> is
    /// <see cref="HarnessArtifactOutcomeCategory.Inline"/>, <see cref="HarnessArtifactOutcomeCategory.Failed"/>,
    /// or <see cref="HarnessArtifactOutcomeCategory.RecoveryRequired"/> but <paramref name="referenceId"/>
    /// is non-null.
    /// </exception>
    internal static HarnessArtifactDiagnostics ForOffload(
        HarnessArtifactOutcomeCategory outcome,
        HarnessArtifactContentCategory content,
        HarnessArtifactDecisionReason reason,
        int observedUtf8ByteSize,
        int configuredThresholdBytes,
        string? referenceId)
    {
        RequireOffloadOutcome(outcome);
        RequireOffloadReason(reason);
        RequireNonNegative(observedUtf8ByteSize, nameof(observedUtf8ByteSize));
        RequirePositive(configuredThresholdBytes, nameof(configuredThresholdBytes));

        if (OffloadOutcomesRequiringReferenceId.Contains(outcome))
        {
            RequireCanonicalReferenceId(referenceId, nameof(referenceId));
        }
        else if (referenceId is not null)
        {
            throw new ArgumentException(
                $"'{outcome}' outcomes must not carry a reference identity.", nameof(referenceId));
        }

        return new HarnessArtifactDiagnostics(
            HarnessArtifactOperationCategory.Offload,
            outcome,
            content,
            reason,
            observedUtf8ByteSize,
            configuredThresholdBytes,
            referenceId,
            HarnessContextAttribution.ForOffload(outcome, observedUtf8ByteSize, referenceId));
    }

    /// <summary>
    /// Builds a snapshot for one rehydration decision.
    /// </summary>
    /// <param name="outcome">
    /// The resolution outcome. Must be one of <see cref="HarnessArtifactOutcomeCategory.Resolved"/>,
    /// <see cref="HarnessArtifactOutcomeCategory.Stale"/>,
    /// <see cref="HarnessArtifactOutcomeCategory.Missing"/>,
    /// <see cref="HarnessArtifactOutcomeCategory.Unauthorized"/>, or
    /// <see cref="HarnessArtifactOutcomeCategory.OverBudget"/>.
    /// </param>
    /// <param name="reason">The reason category. Must be one of the rehydration-family reasons.</param>
    /// <param name="observedUtf8ByteSize">
    /// The UTF-8 byte size actually observed in the workspace, or <see langword="null"/> when the
    /// workspace was never read (<see cref="HarnessArtifactOutcomeCategory.Missing"/> or
    /// <see cref="HarnessArtifactOutcomeCategory.Unauthorized"/>).
    /// </param>
    /// <param name="configuredBudgetBytes">The caller-supplied maximum resolvable byte budget.</param>
    /// <param name="referenceId">The artifact reference identity being resolved.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="outcome"/> is not a valid rehydration outcome; <paramref name="reason"/> is
    /// not a valid rehydration reason; <paramref name="observedUtf8ByteSize"/> is negative; or
    /// <paramref name="configuredBudgetBytes"/> is not greater than zero.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="referenceId"/> is null, empty, whitespace-only, or not a canonical
    /// <c>artifact://sha256/{64-lowercase-hex}</c> reference identity.
    /// </exception>
    internal static HarnessArtifactDiagnostics ForRehydration(
        HarnessArtifactOutcomeCategory outcome,
        HarnessArtifactDecisionReason reason,
        int? observedUtf8ByteSize,
        int configuredBudgetBytes,
        string referenceId)
    {
        RequireRehydrationOutcome(outcome);
        RequireRehydrationReason(reason);
        if (observedUtf8ByteSize is { } value)
        {
            RequireNonNegative(value, nameof(observedUtf8ByteSize));
        }

        RequirePositive(configuredBudgetBytes, nameof(configuredBudgetBytes));
        RequireCanonicalReferenceId(referenceId, nameof(referenceId));

        return new HarnessArtifactDiagnostics(
            HarnessArtifactOperationCategory.Rehydration,
            outcome,
            HarnessArtifactContentCategory.RecoverableContextSegment,
            reason,
            observedUtf8ByteSize,
            configuredBudgetBytes,
            referenceId,
            HarnessContextAttribution.ForRehydration(outcome, referenceId, observedUtf8ByteSize));
    }

    private static void RequireOffloadOutcome(HarnessArtifactOutcomeCategory outcome)
    {
        if (!OffloadOutcomes.Contains(outcome))
        {
            throw new ArgumentOutOfRangeException(
                nameof(outcome), outcome, "The supplied outcome is not a valid offload outcome.");
        }
    }

    private static void RequireRehydrationOutcome(HarnessArtifactOutcomeCategory outcome)
    {
        if (!RehydrationOutcomes.Contains(outcome))
        {
            throw new ArgumentOutOfRangeException(
                nameof(outcome), outcome, "The supplied outcome is not a valid rehydration outcome.");
        }
    }

    private static void RequireOffloadReason(HarnessArtifactDecisionReason reason)
    {
        if (!OffloadReasons.Contains(reason))
        {
            throw new ArgumentOutOfRangeException(
                nameof(reason), reason, "The supplied reason is not a valid offload reason.");
        }
    }

    private static void RequireRehydrationReason(HarnessArtifactDecisionReason reason)
    {
        if (!RehydrationReasons.Contains(reason))
        {
            throw new ArgumentOutOfRangeException(
                nameof(reason), reason, "The supplied reason is not a valid rehydration reason.");
        }
    }

    /// <summary>
    /// Requires <paramref name="referenceId"/> to be exactly the canonical
    /// <c>artifact://sha256/{64-lowercase-hex}</c> shape produced by
    /// <see cref="HarnessArtifactIdentity.BuildReferenceId"/>.
    /// </summary>
    private static void RequireCanonicalReferenceId(string? referenceId, string paramName)
    {
        const string Prefix = "artifact://sha256/";

        if (string.IsNullOrWhiteSpace(referenceId) ||
            !referenceId.StartsWith(Prefix, StringComparison.Ordinal) ||
            !HarnessArtifactIdentity.IsWellFormedDigest(referenceId[Prefix.Length..]))
        {
            throw new ArgumentException(
                $"'{referenceId}' is not a canonical 'artifact://sha256/' reference identity.",
                paramName);
        }
    }

    private static void RequireNonNegative(int value, string paramName)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(paramName, value, "The value must not be negative.");
        }
    }

    private static void RequirePositive(int value, string paramName)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(paramName, value, "The value must be greater than zero.");
        }
    }
}
