using NexusLabs.Foundry.Evaluation.Experiments;

namespace NexusLabs.Foundry.Evaluation.Harness;

/// <summary>
/// Provides one arm executor with stable experiment identity plus the paired case/trial seed shared
/// across all three execution arms and every operational retry of the same statistical trial.
/// </summary>
public sealed record HarnessComparisonArmExecutionContext
{
    internal HarnessComparisonArmExecutionContext(
        HarnessComparisonArm arm,
        ExperimentTaskContext<HarnessManifestCase> experimentContext,
        ulong trialSeed)
    {
        Arm = arm;
        RunId = experimentContext.RunId;
        Sequence = experimentContext.Sequence;
        Case = experimentContext.Case;
        TrialIndex = experimentContext.TrialIndex;
        AttemptNumber = experimentContext.AttemptNumber;
        Features = experimentContext.Features;
        TrialSeed = trialSeed;
    }

    /// <summary>Gets the execution arm being invoked.</summary>
    public HarnessComparisonArm Arm { get; }

    /// <summary>Gets the caller-supplied experiment run identifier.</summary>
    public string RunId { get; }

    /// <summary>Gets the zero-based stable experiment item sequence.</summary>
    public int Sequence { get; }

    /// <summary>Gets the frozen manifest case for this trial.</summary>
    public ExperimentCase<HarnessManifestCase> Case { get; }

    /// <summary>Gets the one-based statistical trial index.</summary>
    public int TrialIndex { get; }

    /// <summary>Gets the one-based operational attempt number.</summary>
    public int AttemptNumber { get; }

    /// <summary>Gets adapter-owned features registered for this experiment item scope.</summary>
    public ExperimentItemFeatureCollection Features { get; }

    /// <summary>
    /// Gets the deterministic case/trial seed shared by every arm and retry for this statistical trial.
    /// </summary>
    public ulong TrialSeed { get; }
}
