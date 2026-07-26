using NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Context;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Diagnostics;

/// <summary>
/// Privacy-safe UTF-8 byte attribution for one artifact offload or rehydration decision: how many
/// bytes were observed coming in, and how many artifact-derived bytes actually resulted from the
/// decision. Always measured in UTF-8 bytes regardless of the operation, so an offload's "shrink to a
/// reference" and a rehydration's "expand from a reference" are reported through the same two-number
/// shape. Never carries the artifact body itself — only byte counts and, transitively via
/// <see cref="HarnessArtifactDiagnostics.ReferenceId"/> on the snapshot this attribution rides along
/// with, a bounded reference identity. <see cref="OutputUtf8Bytes"/> is <see langword="null"/> for
/// failure and recovery-required outcomes; the caller may still emit a separate bounded error string
/// for those outcomes, but that string is not part of the artifact-derived output counted here.
/// </summary>
public sealed record HarnessContextAttribution
{
    private HarnessContextAttribution(
        HarnessArtifactOperationCategory operation, int inputUtf8Bytes, int? outputUtf8Bytes)
    {
        Operation = operation;
        InputUtf8Bytes = inputUtf8Bytes;
        OutputUtf8Bytes = outputUtf8Bytes;
    }

    /// <summary>Whether this attribution describes an offload decision or a rehydration decision.</summary>
    public HarnessArtifactOperationCategory Operation { get; }

    /// <summary>
    /// The UTF-8 byte size observed on input: for an offload, the serialized content's observed
    /// size; for a rehydration, the fixed size of the canonical reference identity being resolved.
    /// </summary>
    public int InputUtf8Bytes { get; }

    /// <summary>
    /// The UTF-8 byte size of the artifact-derived output produced by this decision — the reference
    /// identity's byte length when an artifact reference was written to or reused from context, or the
    /// resolved body's byte length when a body was successfully rehydrated — or <see langword="null"/>
    /// when this decision committed no artifact-derived output to context (for example, a failed or
    /// recovery-required offload, or a non-<c>Resolved</c> rehydration outcome). This is the size of
    /// the artifact reference or body specifically; the caller may still emit a bounded error or
    /// status string for failure outcomes, but those are not part of the artifact-derived output and
    /// are not counted here. Measured in UTF-8 bytes, consistent with <see cref="InputUtf8Bytes"/>.
    /// </summary>
    public int? OutputUtf8Bytes { get; }

    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="outcome"/> is not a valid offload outcome, or <paramref name="observedUtf8ByteSize"/>
    /// is negative.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="outcome"/> is <see cref="HarnessArtifactOutcomeCategory.Offloaded"/> or
    /// <see cref="HarnessArtifactOutcomeCategory.ExistingReference"/> but <paramref name="referenceId"/>
    /// is <see langword="null"/> or whitespace-only.
    /// </exception>
    internal static HarnessContextAttribution ForOffload(
        HarnessArtifactOutcomeCategory outcome, int observedUtf8ByteSize, string? referenceId)
    {
        if (observedUtf8ByteSize < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(observedUtf8ByteSize), observedUtf8ByteSize, "The value must not be negative.");
        }

        int? output = outcome switch
        {
            HarnessArtifactOutcomeCategory.Inline => observedUtf8ByteSize,
            HarnessArtifactOutcomeCategory.Offloaded or HarnessArtifactOutcomeCategory.ExistingReference =>
                RequireReferenceByteSize(referenceId),
            HarnessArtifactOutcomeCategory.Failed or HarnessArtifactOutcomeCategory.RecoveryRequired => null,
            _ => throw new ArgumentOutOfRangeException(
                nameof(outcome), outcome, "The supplied outcome is not a valid offload outcome."),
        };

        return new HarnessContextAttribution(HarnessArtifactOperationCategory.Offload, observedUtf8ByteSize, output);
    }

    /// <exception cref="ArgumentException"><paramref name="referenceId"/> is null or whitespace-only.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="outcome"/> is not a valid rehydration outcome, or
    /// <paramref name="resolvedBodyUtf8Bytes"/> is negative.
    /// </exception>
    internal static HarnessContextAttribution ForRehydration(
        HarnessArtifactOutcomeCategory outcome, string referenceId, int? resolvedBodyUtf8Bytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(referenceId);

        var input = HarnessArtifactIdentity.ComputeUtf8ByteLength(referenceId);

        int? output = outcome switch
        {
            HarnessArtifactOutcomeCategory.Resolved => resolvedBodyUtf8Bytes is { } value && value >= 0
                ? value
                : throw new ArgumentOutOfRangeException(
                    nameof(resolvedBodyUtf8Bytes),
                    resolvedBodyUtf8Bytes,
                    "A Resolved outcome requires a non-negative resolved body byte size."),
            HarnessArtifactOutcomeCategory.Stale
                or HarnessArtifactOutcomeCategory.Missing
                or HarnessArtifactOutcomeCategory.Unauthorized
                or HarnessArtifactOutcomeCategory.OverBudget => null,
            _ => throw new ArgumentOutOfRangeException(
                nameof(outcome), outcome, "The supplied outcome is not a valid rehydration outcome."),
        };

        return new HarnessContextAttribution(HarnessArtifactOperationCategory.Rehydration, input, output);
    }

    private static int RequireReferenceByteSize(string? referenceId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(referenceId);
        return HarnessArtifactIdentity.ComputeUtf8ByteLength(referenceId);
    }
}
