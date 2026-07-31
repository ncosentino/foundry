using System.Text.RegularExpressions;

namespace NexusLabs.Foundry.Evaluation.Harness;

/// <summary>
/// Provides the frozen metadata and complete normalized scheduling ledger used to build one Harness
/// comparison report.
/// </summary>
public sealed partial record HarnessComparisonReportRequest
{
    /// <summary>
    /// Initializes one comparison report request and snapshots the manifest and ledger.
    /// </summary>
    /// <param name="reportId">The stable report identifier.</param>
    /// <param name="runState">The reporter-level hosted run state.</param>
    /// <param name="manifest">The frozen case-set manifest.</param>
    /// <param name="manifestSha256">The canonical manifest SHA-256 digest.</param>
    /// <param name="analysisPlanSha256">The canonical analysis-plan SHA-256 digest.</param>
    /// <param name="bootstrapSeed">The pinned case-level bootstrap seed.</param>
    /// <param name="confidenceLevel">The two-sided confidence level.</param>
    /// <param name="trials">The normalized scheduling ledger rows.</param>
    /// <exception cref="ArgumentException">Identity, digests, or confidence are invalid.</exception>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="manifest"/>, <paramref name="trials"/>, or a row is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="runState"/> is not defined.</exception>
    public HarnessComparisonReportRequest(
        string reportId,
        HarnessHostedRunState runState,
        HarnessCaseSetManifest manifest,
        string manifestSha256,
        string analysisPlanSha256,
        ulong bootstrapSeed,
        double confidenceLevel,
        IReadOnlyList<HarnessComparisonTrialRecord> trials)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportId);
        if (!Enum.IsDefined(runState))
        {
            throw new ArgumentOutOfRangeException(nameof(runState), runState, "The hosted run state is not defined.");
        }

        ArgumentNullException.ThrowIfNull(manifest);
        ValidateDigest(manifestSha256, nameof(manifestSha256));
        ValidateDigest(analysisPlanSha256, nameof(analysisPlanSha256));
        if (!double.IsFinite(confidenceLevel) || confidenceLevel <= 0 || confidenceLevel >= 1)
        {
            throw new ArgumentException(
                "The confidence level must be finite and strictly between zero and one.",
                nameof(confidenceLevel));
        }

        ArgumentNullException.ThrowIfNull(trials);
        var trialSnapshot = new HarnessComparisonTrialRecord[trials.Count];
        for (var index = 0; index < trials.Count; index++)
        {
            var trial = trials[index];
            ArgumentNullException.ThrowIfNull(trial);
            trialSnapshot[index] = trial;
        }

        ReportId = reportId;
        RunState = runState;
        Manifest = new HarnessManifestCaseSource(manifest).Manifest;
        ManifestSha256 = manifestSha256;
        AnalysisPlanSha256 = analysisPlanSha256;
        BootstrapSeed = bootstrapSeed;
        ConfidenceLevel = confidenceLevel;
        Trials = Array.AsReadOnly(trialSnapshot);
    }

    /// <summary>Gets the stable report identifier.</summary>
    public string ReportId { get; }

    /// <summary>Gets the reporter-level hosted run state.</summary>
    public HarnessHostedRunState RunState { get; }

    /// <summary>Gets a defensive snapshot of the frozen manifest.</summary>
    public HarnessCaseSetManifest Manifest { get; }

    /// <summary>Gets the canonical manifest SHA-256 digest.</summary>
    public string ManifestSha256 { get; }

    /// <summary>Gets the canonical analysis-plan SHA-256 digest.</summary>
    public string AnalysisPlanSha256 { get; }

    /// <summary>Gets the pinned case-level bootstrap seed.</summary>
    public ulong BootstrapSeed { get; }

    /// <summary>Gets the two-sided confidence level.</summary>
    public double ConfidenceLevel { get; }

    /// <summary>Gets a defensive snapshot of normalized scheduling-ledger rows.</summary>
    public IReadOnlyList<HarnessComparisonTrialRecord> Trials { get; }

    private static void ValidateDigest(string digest, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(digest);
        if (!DigestPattern().IsMatch(digest))
        {
            throw new ArgumentException(
                "The digest must be a lowercase 64-character SHA-256 value.",
                parameterName);
        }
    }

    [GeneratedRegex("^[0-9a-f]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex DigestPattern();
}
