using NexusLabs.Foundry.Evaluation.Harness.Judging;

namespace NexusLabs.Foundry.Evaluation.Harness;

/// <summary>
/// Provides one protocol-bound deterministic Harness comparison report over the three execution arms.
/// </summary>
public sealed record HarnessComparisonReport
{
    internal HarnessComparisonReport(
        string reportId,
        HarnessHostedRunState runState,
        string caseSetId,
        string caseSetVersion,
        string manifestSha256,
        string analysisPlanSha256,
        int fullyScheduledCaseCount,
        bool retentionEligible,
        IReadOnlyList<HarnessPairwiseContrastReport> contrasts,
        HarnessJudgeDisagreementReport judgeDisagreement)
    {
        ReportId = reportId;
        RunState = runState;
        CaseSetId = caseSetId;
        CaseSetVersion = caseSetVersion;
        ManifestSha256 = manifestSha256;
        AnalysisPlanSha256 = analysisPlanSha256;
        FullyScheduledCaseCount = fullyScheduledCaseCount;
        RetentionEligible = retentionEligible;
        Contrasts = Array.AsReadOnly(contrasts.ToArray());
        JudgeDisagreement = judgeDisagreement;
    }

    /// <summary>Gets the stable report identifier.</summary>
    public string ReportId { get; }

    /// <summary>Gets the reporter-level hosted run state.</summary>
    public HarnessHostedRunState RunState { get; }

    /// <summary>Gets the frozen case-set identifier.</summary>
    public string CaseSetId { get; }

    /// <summary>Gets the frozen case-set version.</summary>
    public string CaseSetVersion { get; }

    /// <summary>Gets the canonical manifest SHA-256 digest.</summary>
    public string ManifestSha256 { get; }

    /// <summary>Gets the canonical analysis-plan SHA-256 digest.</summary>
    public string AnalysisPlanSha256 { get; }

    /// <summary>Gets the number of hosted cases with all three complete paired batches scheduled.</summary>
    public int FullyScheduledCaseCount { get; }

    /// <summary>Gets whether at least six fully scheduled cases remain for a retention recommendation.</summary>
    public bool RetentionEligible { get; }

    /// <summary>Gets the three stable pairwise arm contrasts.</summary>
    public IReadOnlyList<HarnessPairwiseContrastReport> Contrasts { get; }

    /// <summary>Gets deterministic-versus-judge disagreement evidence.</summary>
    public HarnessJudgeDisagreementReport JudgeDisagreement { get; }
}
