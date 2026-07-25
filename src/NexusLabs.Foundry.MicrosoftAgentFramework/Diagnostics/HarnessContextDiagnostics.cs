namespace NexusLabs.Foundry.MicrosoftAgentFramework.Diagnostics;

/// <summary>
/// Privacy-safe, structured evidence for one hybrid context compaction/assembly decision. Carries
/// only categorical outcomes/stages, an explicit measurement unit, sizes, an attempt count,
/// per-category size/entry-count contributions, and a final sequence-validity flag — never raw
/// message text, artifact bodies, workspace paths, owner identities, tool arguments/results,
/// exception text, or classifier output text. Every instance is produced by <see cref="ForSuccess"/>
/// or <see cref="ForTermination"/>, mirroring exactly the success/termination split of the internal
/// assembly result this snapshot describes.
/// </summary>
/// <remarks>
/// The same instance attached to the internal <c>HarnessBoundedMessageAssembly</c> dispatch
/// result — the private structure that carries the final bounded messages returned for dispatch —
/// is also the instance carried by the <c>HarnessContextCompactionCompletedEvent</c> and,
/// on success, the subsequent <c>HarnessContextComposedEvent</c> for that same attempt, so
/// inspecting any of those three surfaces observes identical data.
/// </remarks>
public sealed record HarnessContextDiagnostics
{
    private static readonly HashSet<HarnessContextCompactionOutcome> SuccessOutcomes =
    [
        HarnessContextCompactionOutcome.WithinLimit,
        HarnessContextCompactionOutcome.Reduced,
        HarnessContextCompactionOutcome.PreservationFallback,
    ];

    private static readonly HashSet<HarnessContextCompactionOutcome> TerminationOutcomes =
    [
        HarnessContextCompactionOutcome.Irreducible,
        HarnessContextCompactionOutcome.ConcurrentMutationLimit,
    ];

    private HarnessContextDiagnostics(
        HarnessContextCompactionOutcome outcome,
        HarnessContextMeasurementUnit measurementUnit,
        int originalSize,
        int finalSize,
        int triggerThreshold,
        int hardLimit,
        int attemptCount,
        IReadOnlyList<HarnessContextAssemblyStageCategory> stages,
        IReadOnlyList<HarnessContextCategoryContribution> categoryContributions,
        bool? finalSequenceValid)
    {
        Outcome = outcome;
        MeasurementUnit = measurementUnit;
        OriginalSize = originalSize;
        FinalSize = finalSize;
        TriggerThreshold = triggerThreshold;
        HardLimit = hardLimit;
        AttemptCount = attemptCount;
        Stages = stages;
        CategoryContributions = categoryContributions;
        FinalSequenceValid = finalSequenceValid;
    }

    /// <summary>
    /// The explicit categorical outcome of this decision. The two termination members
    /// (<see cref="HarnessContextCompactionOutcome.Irreducible"/> and
    /// <see cref="HarnessContextCompactionOutcome.ConcurrentMutationLimit"/>) double as this
    /// decision's termination category.
    /// </summary>
    public HarnessContextCompactionOutcome Outcome { get; }

    /// <summary>
    /// The explicit unit every size, threshold, and limit on this instance is expressed in. Never
    /// assumed to be a provider token count unless it actually is one.
    /// </summary>
    public HarnessContextMeasurementUnit MeasurementUnit { get; }

    /// <summary>The estimated size of the entries captured at the start of this assembly.</summary>
    public int OriginalSize { get; }

    /// <summary>
    /// The estimated size of the final entries on success, or of the terminating fallback candidate
    /// on a termination.
    /// </summary>
    public int FinalSize { get; }

    /// <summary>The trigger threshold (hard limit minus trigger margin) in force for this assembly.</summary>
    public int TriggerThreshold { get; }

    /// <summary>The hard limit in force for this assembly. <see cref="FinalSize"/> never exceeds this on success.</summary>
    public int HardLimit { get; }

    /// <summary>The number of bounded recompaction attempts consumed, including any restarts.</summary>
    public int AttemptCount { get; }

    /// <summary>The reduction path, in execution order.</summary>
    public IReadOnlyList<HarnessContextAssemblyStageCategory> Stages { get; }

    /// <summary>
    /// The per-category size/entry-count contributions to <see cref="FinalSize"/>, one entry per
    /// category actually present in the final entries. Always empty on a termination, because no
    /// final entries exist to attribute. The sum of every contribution's size always equals
    /// <see cref="FinalSize"/> on success.
    /// </summary>
    public IReadOnlyList<HarnessContextCategoryContribution> CategoryContributions { get; }

    /// <summary>
    /// <see langword="true"/> when the final entries were verified as sequence-valid (always the
    /// case for a success instance, because a rejected verification is itself a termination);
    /// <see langword="null"/> on a termination, since verification against a final entry set was
    /// never reached.
    /// </summary>
    public bool? FinalSequenceValid { get; }

    /// <exception cref="ArgumentNullException">
    /// <paramref name="stages"/> or <paramref name="categoryContributions"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="outcome"/> is not a success outcome; <paramref name="measurementUnit"/> is not
    /// a defined <see cref="HarnessContextMeasurementUnit"/> value; <paramref name="originalSize"/>,
    /// <paramref name="finalSize"/>, <paramref name="triggerThreshold"/>, <paramref name="hardLimit"/>,
    /// or <paramref name="attemptCount"/> is negative; or any contribution in
    /// <paramref name="categoryContributions"/> has a non-positive <see cref="HarnessContextCategoryContribution.EntryCount"/>,
    /// a negative <see cref="HarnessContextCategoryContribution.Size"/>, or a
    /// <see cref="HarnessContextCategoryContribution.Category"/> that is not a defined
    /// <see cref="HarnessContextCategory"/> value (checked defensively here even though the only public
    /// construction path, <see cref="HarnessContextCategoryContribution.Create"/>, already rejects an
    /// undefined category itself).
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="finalSize"/> exceeds <paramref name="hardLimit"/>;
    /// <paramref name="categoryContributions"/> contains more than one entry for the same category; or
    /// the sum of <paramref name="categoryContributions"/>' sizes does not equal <paramref name="finalSize"/>.
    /// </exception>
    /// <exception cref="OverflowException">
    /// The sum of <paramref name="categoryContributions"/>' sizes overflows <see cref="int"/>.
    /// </exception>
    internal static HarnessContextDiagnostics ForSuccess(
        HarnessContextCompactionOutcome outcome,
        HarnessContextMeasurementUnit measurementUnit,
        int originalSize,
        int finalSize,
        int triggerThreshold,
        int hardLimit,
        int attemptCount,
        IReadOnlyList<HarnessContextAssemblyStageCategory> stages,
        IReadOnlyList<HarnessContextCategoryContribution> categoryContributions)
    {
        RequireSuccessOutcome(outcome);
        if (!Enum.IsDefined(measurementUnit))
        {
            throw new ArgumentOutOfRangeException(
                nameof(measurementUnit), measurementUnit,
                "The measurement unit is not a defined HarnessContextMeasurementUnit value.");
        }

        ArgumentNullException.ThrowIfNull(stages);
        ArgumentNullException.ThrowIfNull(categoryContributions);
        RequireNonNegative(originalSize, nameof(originalSize));
        RequireNonNegative(finalSize, nameof(finalSize));
        RequireNonNegative(triggerThreshold, nameof(triggerThreshold));
        RequireNonNegative(hardLimit, nameof(hardLimit));
        RequireNonNegative(attemptCount, nameof(attemptCount));

        if (finalSize > hardLimit)
        {
            throw new ArgumentException(
                "A success instance's final size must not exceed the hard limit.", nameof(finalSize));
        }

        foreach (var contribution in categoryContributions)
        {
            // Defensive: the only public construction path (HarnessContextCategoryContribution.Create)
            // already rejects an undefined category, but this factory never trusts that a supplied
            // contribution was actually produced by it — an undefined enum value is still checked here.
            if (!Enum.IsDefined(contribution.Category))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(categoryContributions),
                    $"A category contribution has a category value ({contribution.Category}) that is not " +
                    $"a defined {nameof(HarnessContextCategory)} value.");
            }

            if (contribution.Size < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(categoryContributions),
                    $"A category contribution for '{contribution.Category}' has a negative size ({contribution.Size}).");
            }

            if (contribution.EntryCount <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(categoryContributions),
                    $"A category contribution for '{contribution.Category}' has a non-positive entry count ({contribution.EntryCount}).");
            }
        }

        var seenCategories = new HashSet<HarnessContextCategory>();
        foreach (var contribution in categoryContributions)
        {
            if (!seenCategories.Add(contribution.Category))
            {
                throw new ArgumentException(
                    $"Category contributions must have unique categories; '{contribution.Category}' appears more than once.",
                    nameof(categoryContributions));
            }
        }

        var contributionTotal = 0;
        foreach (var contribution in categoryContributions)
        {
            contributionTotal = checked(contributionTotal + contribution.Size);
        }

        if (contributionTotal != finalSize)
        {
            throw new ArgumentException(
                $"The sum of category contribution sizes ({contributionTotal}) must equal the final " +
                $"size ({finalSize}).",
                nameof(categoryContributions));
        }

        return new HarnessContextDiagnostics(
            outcome,
            measurementUnit,
            originalSize,
            finalSize,
            triggerThreshold,
            hardLimit,
            attemptCount,
            new List<HarnessContextAssemblyStageCategory>(stages).AsReadOnly(),
            new List<HarnessContextCategoryContribution>(categoryContributions).AsReadOnly(),
            finalSequenceValid: true);
    }

    /// <exception cref="ArgumentNullException"><paramref name="stages"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="outcome"/> is not a termination outcome; <paramref name="measurementUnit"/> is
    /// not a defined <see cref="HarnessContextMeasurementUnit"/> value; <paramref name="originalSize"/>,
    /// <paramref name="finalSize"/>, <paramref name="triggerThreshold"/>, <paramref name="hardLimit"/>,
    /// or <paramref name="attemptCount"/> is negative.
    /// </exception>
    internal static HarnessContextDiagnostics ForTermination(
        HarnessContextCompactionOutcome outcome,
        HarnessContextMeasurementUnit measurementUnit,
        int originalSize,
        int finalSize,
        int triggerThreshold,
        int hardLimit,
        int attemptCount,
        IReadOnlyList<HarnessContextAssemblyStageCategory> stages)
    {
        RequireTerminationOutcome(outcome);
        if (!Enum.IsDefined(measurementUnit))
        {
            throw new ArgumentOutOfRangeException(
                nameof(measurementUnit), measurementUnit,
                "The measurement unit is not a defined HarnessContextMeasurementUnit value.");
        }

        ArgumentNullException.ThrowIfNull(stages);
        RequireNonNegative(originalSize, nameof(originalSize));
        RequireNonNegative(finalSize, nameof(finalSize));
        RequireNonNegative(triggerThreshold, nameof(triggerThreshold));
        RequireNonNegative(hardLimit, nameof(hardLimit));
        RequireNonNegative(attemptCount, nameof(attemptCount));

        return new HarnessContextDiagnostics(
            outcome,
            measurementUnit,
            originalSize,
            finalSize,
            triggerThreshold,
            hardLimit,
            attemptCount,
            new List<HarnessContextAssemblyStageCategory>(stages).AsReadOnly(),
            categoryContributions: [],
            finalSequenceValid: null);
    }

    private static void RequireSuccessOutcome(HarnessContextCompactionOutcome outcome)
    {
        if (!SuccessOutcomes.Contains(outcome))
        {
            throw new ArgumentOutOfRangeException(
                nameof(outcome), outcome, "The supplied outcome is not a valid success outcome.");
        }
    }

    private static void RequireTerminationOutcome(HarnessContextCompactionOutcome outcome)
    {
        if (!TerminationOutcomes.Contains(outcome))
        {
            throw new ArgumentOutOfRangeException(
                nameof(outcome), outcome, "The supplied outcome is not a valid termination outcome.");
        }
    }

    private static void RequireNonNegative(int value, string paramName)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(paramName, value, "The value must not be negative.");
        }
    }
}
