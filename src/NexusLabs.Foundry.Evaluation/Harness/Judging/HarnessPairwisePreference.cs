namespace NexusLabs.Foundry.Evaluation.Harness.Judging;

/// <summary>
/// Identifies one nominal pairwise judge preference.
/// </summary>
public enum HarnessPairwisePreference
{
    /// <summary>The left candidate is preferred.</summary>
    Left,

    /// <summary>The candidates are equivalent for the judged dimension.</summary>
    Tie,

    /// <summary>The right candidate is preferred.</summary>
    Right,

    /// <summary>The supplied evidence is insufficient for a defensible preference.</summary>
    Abstain,
}
