namespace NexusLabs.Foundry.Evaluation.Harness;

/// <summary>
/// Identifies one execution arm in the <c>harness-001</c> paired comparison.
/// </summary>
public enum HarnessComparisonArm
{
    /// <summary>The current Foundry workspace-driven iterative loop.</summary>
    Iterative,

    /// <summary>The plain upstream Harness execution path.</summary>
    PlainHarness,

    /// <summary>The Harness execution path composed with the Foundry workspace/context bridge.</summary>
    Hybrid,
}
