namespace NexusLabs.Foundry.Evaluation.Harness.Judging;

/// <summary>
/// Identifies the response shape of a versioned Harness judge rubric.
/// </summary>
public enum HarnessJudgeRubricKind
{
    /// <summary>A closed nominal pairwise preference.</summary>
    Nominal,

    /// <summary>An ordered integer response-quality score.</summary>
    Ordinal,
}
