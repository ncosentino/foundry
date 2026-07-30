namespace NexusLabs.Foundry.Evaluation.Harness;

/// <summary>
/// Provides case-level diagnostics comparability evidence for one ordered arm contrast.
/// </summary>
public sealed record HarnessDiagnosticsParityComparison
{
    internal HarnessDiagnosticsParityComparison(
        IReadOnlyList<HarnessDiagnosticsParityCaseResult> cases)
    {
        Cases = Array.AsReadOnly(cases.ToArray());
        ComparableCaseCount = Cases.Count(@case => @case.FullyScheduled && @case.IsComparable);
        NonComparableCaseCount = Cases.Count(@case => @case.FullyScheduled && !@case.IsComparable);
        IncompleteDueToCapCaseCount = Cases.Count(@case => !@case.FullyScheduled);
    }

    /// <summary>Gets case-level diagnostics parity results in hosted case order.</summary>
    public IReadOnlyList<HarnessDiagnosticsParityCaseResult> Cases { get; }

    /// <summary>Gets the number of fully scheduled cases with comparable normalized schemas.</summary>
    public int ComparableCaseCount { get; }

    /// <summary>Gets the number of fully scheduled cases with a schema mismatch.</summary>
    public int NonComparableCaseCount { get; }

    /// <summary>Gets the number of cases excluded because all three paired batches were not scheduled.</summary>
    public int IncompleteDueToCapCaseCount { get; }
}
