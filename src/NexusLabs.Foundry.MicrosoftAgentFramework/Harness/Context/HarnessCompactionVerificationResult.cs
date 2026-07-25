namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Context;

/// <summary>
/// Explicit, immutable result of <see cref="HarnessCompactionVerifier.Verify"/>. Never a bare boolean:
/// a <see cref="HarnessCompactionVerificationOutcome.Rejected"/> result always carries at least one
/// categorical <see cref="HarnessCompactionRejectionReason"/> plus the specific missing or invalid
/// entry ids that caused it, so a caller never has to re-derive why a reduction was rejected.
/// </summary>
internal sealed record HarnessCompactionVerificationResult
{
    private HarnessCompactionVerificationResult(
        HarnessCompactionVerificationOutcome outcome,
        IReadOnlyList<HarnessCompactionRejectionReason> rejectionReasons,
        IReadOnlyList<string> missingRequiredEntryIds,
        IReadOnlyList<string> invalidEntryIds,
        IReadOnlyList<string> preservationOnlyFallbackEntryIds)
    {
        Outcome = outcome;
        RejectionReasons = rejectionReasons;
        MissingRequiredEntryIds = missingRequiredEntryIds;
        InvalidEntryIds = invalidEntryIds;
        PreservationOnlyFallbackEntryIds = preservationOnlyFallbackEntryIds;
    }

    /// <summary>The explicit accept/reject outcome.</summary>
    internal HarnessCompactionVerificationOutcome Outcome { get; }

    /// <summary><see langword="true"/> only when <see cref="Outcome"/> is <see cref="HarnessCompactionVerificationOutcome.Accepted"/>.</summary>
    internal bool IsAccepted => Outcome == HarnessCompactionVerificationOutcome.Accepted;

    /// <summary>
    /// Every distinct categorical reason this result was rejected. Always empty when
    /// <see cref="IsAccepted"/> is <see langword="true"/>; never empty when it is
    /// <see langword="false"/>.
    /// </summary>
    internal IReadOnlyList<HarnessCompactionRejectionReason> RejectionReasons { get; }

    /// <summary>Required entry ids entirely absent from the proposed entries. Always empty when accepted.</summary>
    internal IReadOnlyList<string> MissingRequiredEntryIds { get; }

    /// <summary>
    /// Entry ids implicated in a content mismatch, an orphaned/duplicated/reordered tool exchange, or a
    /// forged structural entry. Always empty when accepted.
    /// </summary>
    internal IReadOnlyList<string> InvalidEntryIds { get; }

    /// <summary>
    /// The deterministic preservation-only fallback candidate: the original entries' required entry
    /// ids, in their original relative order, with every reducible entry excluded. Exposed for a later
    /// assembler to use as a fallback candidate; this type does not itself retry or assemble a final
    /// context envelope.
    /// </summary>
    internal IReadOnlyList<string> PreservationOnlyFallbackEntryIds { get; }

    /// <exception cref="ArgumentNullException">
    /// <paramref name="preservationOnlyFallbackEntryIds"/> is <see langword="null"/>.
    /// </exception>
    internal static HarnessCompactionVerificationResult Accepted(
        IReadOnlyList<string> preservationOnlyFallbackEntryIds)
    {
        ArgumentNullException.ThrowIfNull(preservationOnlyFallbackEntryIds);

        return new HarnessCompactionVerificationResult(
            HarnessCompactionVerificationOutcome.Accepted,
            [],
            [],
            [],
            preservationOnlyFallbackEntryIds);
    }

    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="rejectionReasons"/> is empty.</exception>
    internal static HarnessCompactionVerificationResult Rejected(
        IReadOnlyList<HarnessCompactionRejectionReason> rejectionReasons,
        IReadOnlyList<string> missingRequiredEntryIds,
        IReadOnlyList<string> invalidEntryIds,
        IReadOnlyList<string> preservationOnlyFallbackEntryIds)
    {
        ArgumentNullException.ThrowIfNull(rejectionReasons);
        ArgumentNullException.ThrowIfNull(missingRequiredEntryIds);
        ArgumentNullException.ThrowIfNull(invalidEntryIds);
        ArgumentNullException.ThrowIfNull(preservationOnlyFallbackEntryIds);

        if (rejectionReasons.Count == 0)
        {
            throw new ArgumentException(
                "A rejected result requires at least one categorical reason.", nameof(rejectionReasons));
        }

        return new HarnessCompactionVerificationResult(
            HarnessCompactionVerificationOutcome.Rejected,
            rejectionReasons,
            missingRequiredEntryIds,
            invalidEntryIds,
            preservationOnlyFallbackEntryIds);
    }
}
