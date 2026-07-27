using NexusLabs.Foundry.MicrosoftAgentFramework.Diagnostics;

namespace NexusLabs.Foundry.Evaluation.Harness;

/// <summary>
/// Per-item, privacy-safe evidence for one artifact offload or rehydration decision, mirroring the
/// public categorical shape of <see cref="HarnessArtifactDiagnostics"/> and its
/// <see cref="HarnessContextAttribution"/>. It is constructable without the Harness runtime so
/// evaluators and tests can score artifact-reuse and rehydration dimensions from a plain evidence
/// record.
/// </summary>
public sealed record HarnessArtifactDecisionEvidence
{
    /// <summary>Gets whether this describes an offload decision or a rehydration decision.</summary>
    public required HarnessArtifactOperationCategory Operation { get; init; }

    /// <summary>Gets the explicit decision outcome.</summary>
    public required HarnessArtifactOutcomeCategory Outcome { get; init; }

    /// <summary>Gets the kind of content the decision concerned.</summary>
    public required HarnessArtifactContentCategory Content { get; init; }

    /// <summary>Gets the stable reason category behind the outcome.</summary>
    public required HarnessArtifactDecisionReason Reason { get; init; }

    /// <summary>Gets the configured inline threshold (offload) or maximum budget (rehydration).</summary>
    public required int ConfiguredThresholdOrBudget { get; init; }

    /// <summary>Gets the UTF-8 byte size observed on input for this decision.</summary>
    public required int InputUtf8Bytes { get; init; }

    /// <summary>
    /// Gets the UTF-8 byte size actually measured for the decision content, or <see langword="null"/>
    /// when no content was ever measured.
    /// </summary>
    public int? ObservedUtf8ByteSize { get; init; }

    /// <summary>
    /// Gets the UTF-8 byte size of the artifact-derived output committed to context, or
    /// <see langword="null"/> when the decision committed no artifact-derived output.
    /// </summary>
    public int? OutputUtf8Bytes { get; init; }

    /// <summary>
    /// Gets the bounded artifact reference identity this decision concerns, or <see langword="null"/>
    /// when the outcome never committed or reused one.
    /// </summary>
    public string? ReferenceId { get; init; }
}
