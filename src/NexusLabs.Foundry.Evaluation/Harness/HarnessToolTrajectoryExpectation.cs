namespace NexusLabs.Foundry.Evaluation.Harness;

/// <summary>
/// Per-item expectation for the required/forbidden tool trajectory dimension. The required tools must
/// appear, in order, as a subsequence of the run's observed tool-call names; no forbidden tool may
/// appear at all. The expectation is compared against the tool-call diagnostics carried in the run's
/// <see cref="AgentRunDiagnosticsContext"/>.
/// </summary>
public sealed record HarnessToolTrajectoryExpectation
{
    /// <summary>Gets the ordered tool names that must appear as an in-order subsequence.</summary>
    public required IReadOnlyList<string> RequiredToolSequence { get; init; }

    /// <summary>Gets the tool names that must not appear at all.</summary>
    public required IReadOnlyList<string> ForbiddenTools { get; init; }
}
