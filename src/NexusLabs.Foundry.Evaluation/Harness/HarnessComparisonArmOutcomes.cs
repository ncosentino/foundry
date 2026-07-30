using NexusLabs.Foundry.Evaluation.Experiments;

namespace NexusLabs.Foundry.Evaluation.Harness;

/// <summary>
/// Provides the canonical experiment outcomes for all three Harness comparison arms.
/// </summary>
/// <typeparam name="TOutput">The caller-owned arm output type.</typeparam>
public sealed record HarnessComparisonArmOutcomes<TOutput>
{
    /// <summary>
    /// Initializes the three arm outcomes and validates their stable experiment identities.
    /// </summary>
    /// <param name="iterative">The iterative-arm outcome.</param>
    /// <param name="plainHarness">The plain-Harness-arm outcome.</param>
    /// <param name="hybrid">The hybrid-arm outcome.</param>
    /// <exception cref="ArgumentException">An outcome carries the wrong experiment name.</exception>
    /// <exception cref="ArgumentNullException">An outcome is <see langword="null"/>.</exception>
    public HarnessComparisonArmOutcomes(
        ExperimentRunOutcome<HarnessManifestCase, TOutput> iterative,
        ExperimentRunOutcome<HarnessManifestCase, TOutput> plainHarness,
        ExperimentRunOutcome<HarnessManifestCase, TOutput> hybrid)
    {
        Iterative = Validate(
            iterative,
            HarnessComparisonExperiment.IterativeExperimentName,
            nameof(iterative));
        PlainHarness = Validate(
            plainHarness,
            HarnessComparisonExperiment.PlainHarnessExperimentName,
            nameof(plainHarness));
        Hybrid = Validate(
            hybrid,
            HarnessComparisonExperiment.HybridExperimentName,
            nameof(hybrid));
    }

    /// <summary>Gets the iterative-arm canonical outcome.</summary>
    public ExperimentRunOutcome<HarnessManifestCase, TOutput> Iterative { get; }

    /// <summary>Gets the plain-Harness-arm canonical outcome.</summary>
    public ExperimentRunOutcome<HarnessManifestCase, TOutput> PlainHarness { get; }

    /// <summary>Gets the hybrid-arm canonical outcome.</summary>
    public ExperimentRunOutcome<HarnessManifestCase, TOutput> Hybrid { get; }

    private static ExperimentRunOutcome<HarnessManifestCase, TOutput> Validate(
        ExperimentRunOutcome<HarnessManifestCase, TOutput> outcome,
        string expectedExperimentName,
        string parameterName)
    {
        ArgumentNullException.ThrowIfNull(outcome, parameterName);
        if (!string.Equals(
                outcome.Result.ExperimentName,
                expectedExperimentName,
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"The outcome must use experiment name '{expectedExperimentName}'.",
                parameterName);
        }

        return outcome;
    }
}
