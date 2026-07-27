namespace NexusLabs.Foundry.Evaluation.Harness.Judging;

/// <summary>
/// Provides one validated, versioned, hashed judge rubric.
/// </summary>
public sealed record HarnessJudgeRubric
{
    internal HarnessJudgeRubric(
        string id,
        string version,
        HarnessJudgeRubricKind kind,
        string sha256,
        IReadOnlyList<string> instructions,
        IReadOnlyList<string> labels,
        IReadOnlyList<int> scale)
    {
        Id = id;
        Version = version;
        Kind = kind;
        Sha256 = sha256;
        Instructions = Array.AsReadOnly(instructions.ToArray());
        Labels = Array.AsReadOnly(labels.ToArray());
        Scale = Array.AsReadOnly(scale.ToArray());
    }

    /// <summary>Gets the stable rubric identifier.</summary>
    public string Id { get; }

    /// <summary>Gets the rubric version.</summary>
    public string Version { get; }

    /// <summary>Gets the rubric kind.</summary>
    public HarnessJudgeRubricKind Kind { get; }

    /// <summary>Gets the canonical SHA-256 digest.</summary>
    public string Sha256 { get; }

    /// <summary>Gets the ordered evaluator instructions.</summary>
    public IReadOnlyList<string> Instructions { get; }

    /// <summary>Gets the closed nominal labels, or an empty list for an ordinal rubric.</summary>
    public IReadOnlyList<string> Labels { get; }

    /// <summary>Gets the ordered ordinal scale, or an empty list for a nominal rubric.</summary>
    public IReadOnlyList<int> Scale { get; }
}
