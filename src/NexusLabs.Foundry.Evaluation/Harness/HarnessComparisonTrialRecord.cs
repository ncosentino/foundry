using NexusLabs.Foundry.Evaluation.Experiments;

namespace NexusLabs.Foundry.Evaluation.Harness;

/// <summary>
/// Describes one normalized arm/case/trial ledger row, including explicit unscheduled rows retained
/// after run truncation.
/// </summary>
public sealed record HarnessComparisonTrialRecord
{
    /// <summary>
    /// Initializes one normalized trial record and snapshots its evidence collections.
    /// </summary>
    /// <param name="arm">The execution arm.</param>
    /// <param name="caseId">The hosted case identifier.</param>
    /// <param name="trialIndex">The one-based statistical trial index.</param>
    /// <param name="scheduled">Whether the complete paired batch containing this arm was scheduled.</param>
    /// <param name="status">The terminal item status for a scheduled row; otherwise <see langword="null"/>.</param>
    /// <param name="binaryValues">The binary dimension values.</param>
    /// <param name="continuousValues">The continuous dimension values.</param>
    /// <param name="responseCaptureReference">The response capture reference, when available.</param>
    /// <param name="evidenceArtifactReference">The incremental evidence artifact reference, when available.</param>
    /// <exception cref="ArgumentException">
    /// Identity, status, evidence dimensions, or artifact references are invalid.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// A required collection or collection element is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="arm"/>, <paramref name="trialIndex"/>, or <paramref name="status"/> is invalid.
    /// </exception>
    public HarnessComparisonTrialRecord(
        HarnessComparisonArm arm,
        string caseId,
        int trialIndex,
        bool scheduled,
        ExperimentItemStatus? status,
        IReadOnlyList<HarnessComparisonBinaryTrialValue> binaryValues,
        IReadOnlyList<HarnessComparisonContinuousTrialValue> continuousValues,
        string? responseCaptureReference,
        string? evidenceArtifactReference)
    {
        if (!Enum.IsDefined(arm))
        {
            throw new ArgumentOutOfRangeException(nameof(arm), arm, "The comparison arm is not defined.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(caseId);
        if (trialIndex < 1 || trialIndex > HarnessManifestCaseSource.RequiredHostedTrialCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(trialIndex),
                trialIndex,
                $"The trial index must be from 1 through {HarnessManifestCaseSource.RequiredHostedTrialCount}.");
        }

        if (scheduled)
        {
            if (!status.HasValue || !Enum.IsDefined(status.Value))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(status),
                    status,
                    "A scheduled trial must carry a defined terminal item status.");
            }
        }
        else if (status.HasValue)
        {
            throw new ArgumentException("An unscheduled trial cannot carry an item status.", nameof(status));
        }

        BinaryValues = SnapshotBinaryValues(binaryValues);
        ContinuousValues = SnapshotContinuousValues(continuousValues);
        if (!scheduled && (BinaryValues.Count != 0 || ContinuousValues.Count != 0))
        {
            throw new ArgumentException("An unscheduled trial cannot carry evaluator values.", nameof(scheduled));
        }

        ValidateReference(responseCaptureReference, nameof(responseCaptureReference));
        ValidateReference(evidenceArtifactReference, nameof(evidenceArtifactReference));
        if (!scheduled && (responseCaptureReference is not null || evidenceArtifactReference is not null))
        {
            throw new ArgumentException("An unscheduled trial cannot carry artifact references.", nameof(scheduled));
        }

        Arm = arm;
        CaseId = caseId;
        TrialIndex = trialIndex;
        Scheduled = scheduled;
        Status = status;
        ResponseCaptureReference = responseCaptureReference;
        EvidenceArtifactReference = evidenceArtifactReference;
    }

    /// <summary>Gets the execution arm.</summary>
    public HarnessComparisonArm Arm { get; }

    /// <summary>Gets the hosted case identifier.</summary>
    public string CaseId { get; }

    /// <summary>Gets the one-based statistical trial index.</summary>
    public int TrialIndex { get; }

    /// <summary>Gets whether the complete paired batch containing this row was scheduled.</summary>
    public bool Scheduled { get; }

    /// <summary>Gets the terminal item status for a scheduled row.</summary>
    public ExperimentItemStatus? Status { get; }

    /// <summary>Gets a defensive snapshot of binary dimension values.</summary>
    public IReadOnlyList<HarnessComparisonBinaryTrialValue> BinaryValues { get; }

    /// <summary>Gets a defensive snapshot of continuous dimension values.</summary>
    public IReadOnlyList<HarnessComparisonContinuousTrialValue> ContinuousValues { get; }

    /// <summary>Gets the response capture reference, when available.</summary>
    public string? ResponseCaptureReference { get; }

    /// <summary>Gets the incremental evidence artifact reference, when available.</summary>
    public string? EvidenceArtifactReference { get; }

    private static IReadOnlyList<HarnessComparisonBinaryTrialValue> SnapshotBinaryValues(
        IReadOnlyList<HarnessComparisonBinaryTrialValue> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var dimensions = new HashSet<HarnessEvaluationDimension>();
        var snapshot = new HarnessComparisonBinaryTrialValue[values.Count];
        for (var index = 0; index < values.Count; index++)
        {
            var value = values[index];
            ArgumentNullException.ThrowIfNull(value);
            if (!dimensions.Add(value.Dimension))
            {
                throw new ArgumentException(
                    $"Binary dimension '{value.Dimension}' appears more than once.",
                    nameof(values));
            }

            snapshot[index] = new HarnessComparisonBinaryTrialValue(
                value.Dimension,
                value.Value,
                value.IsComparable);
        }

        return Array.AsReadOnly(snapshot);
    }

    private static IReadOnlyList<HarnessComparisonContinuousTrialValue> SnapshotContinuousValues(
        IReadOnlyList<HarnessComparisonContinuousTrialValue> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var dimensions = new HashSet<HarnessEvaluationDimension>();
        var snapshot = new HarnessComparisonContinuousTrialValue[values.Count];
        for (var index = 0; index < values.Count; index++)
        {
            var value = values[index];
            ArgumentNullException.ThrowIfNull(value);
            if (!dimensions.Add(value.Dimension))
            {
                throw new ArgumentException(
                    $"Continuous dimension '{value.Dimension}' appears more than once.",
                    nameof(values));
            }

            snapshot[index] = new HarnessComparisonContinuousTrialValue(
                value.Dimension,
                value.Value,
                value.PessimisticScheduledFailureValue,
                value.IsComparable);
        }

        return Array.AsReadOnly(snapshot);
    }

    private static void ValidateReference(string? reference, string parameterName)
    {
        if (reference is not null && string.IsNullOrWhiteSpace(reference))
        {
            throw new ArgumentException("An artifact reference cannot be blank.", parameterName);
        }
    }
}
