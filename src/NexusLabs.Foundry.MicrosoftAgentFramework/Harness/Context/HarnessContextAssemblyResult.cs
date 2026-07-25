namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Context;

/// <summary>
/// Explicit, immutable result of <see cref="HarnessContextAssembler.AssembleAsync"/>. A
/// <see cref="Outcome"/> of <see cref="HarnessContextAssemblyOutcome.Irreducible"/> or
/// <see cref="HarnessContextAssemblyOutcome.ConcurrentMutationLimit"/> is a distinct termination: it
/// never carries <see cref="FinalEntries"/> or raw content, only categorical evidence, so an
/// over-budget history is never mistakenly forwarded as if it were a success.
/// </summary>
/// <remarks>
/// <see cref="Success"/> and <see cref="Terminated"/> never share the caller's <see cref="FinalEntries"/>
/// (or the entry instances within it), <see cref="Stages"/>, or <see cref="RequiredEntryIds"/> list:
/// each is independently copied/wrapped into a read-only collection this result alone constructs and
/// holds — <see cref="FinalEntries"/> via <see cref="HarnessContextEntry.Copy"/> for every entry — so
/// this type's "immutable" claim holds even if the caller continues to hold and mutate what it passed
/// in to either factory.
/// </remarks>
internal sealed record HarnessContextAssemblyResult
{
    private HarnessContextAssemblyResult(
        HarnessContextAssemblyOutcome outcome,
        IReadOnlyList<HarnessContextEntry>? finalEntries,
        int originalEstimatedSize,
        int finalEstimatedSize,
        int hardLimit,
        int attemptCount,
        IReadOnlyList<HarnessContextAssemblyStage> stages,
        IReadOnlyList<string> requiredEntryIds,
        long latestSnapshotVersion,
        HarnessCompactionVerificationResult? finalVerification)
    {
        Outcome = outcome;
        FinalEntries = finalEntries;
        OriginalEstimatedSize = originalEstimatedSize;
        FinalEstimatedSize = finalEstimatedSize;
        HardLimit = hardLimit;
        AttemptCount = attemptCount;
        Stages = stages;
        RequiredEntryIds = requiredEntryIds;
        LatestSnapshotVersion = latestSnapshotVersion;
        FinalVerification = finalVerification;
    }

    /// <summary>The explicit categorical outcome of this assembly.</summary>
    internal HarnessContextAssemblyOutcome Outcome { get; }

    /// <summary>
    /// <see langword="true"/> only for <see cref="HarnessContextAssemblyOutcome.WithinLimit"/>,
    /// <see cref="HarnessContextAssemblyOutcome.Reduced"/>, or
    /// <see cref="HarnessContextAssemblyOutcome.PreservationFallback"/>.
    /// </summary>
    internal bool IsSuccess =>
        Outcome is HarnessContextAssemblyOutcome.WithinLimit
            or HarnessContextAssemblyOutcome.Reduced
            or HarnessContextAssemblyOutcome.PreservationFallback;

    /// <summary>The final dispatch-eligible entries. Always <see langword="null"/> on a termination.</summary>
    internal IReadOnlyList<HarnessContextEntry>? FinalEntries { get; }

    /// <summary>The estimated size of the entries captured at the start of this assembly.</summary>
    internal int OriginalEstimatedSize { get; }

    /// <summary>
    /// The estimated size of <see cref="FinalEntries"/> on success, or of the terminating fallback
    /// candidate on a termination.
    /// </summary>
    internal int FinalEstimatedSize { get; }

    /// <summary>The hard limit in force for this assembly. <see cref="FinalEstimatedSize"/> &lt;= this on success.</summary>
    internal int HardLimit { get; }

    /// <summary>The number of bounded recompaction attempts consumed, including any restarts.</summary>
    internal int AttemptCount { get; }

    /// <summary>The reduction path, in execution order.</summary>
    internal IReadOnlyList<HarnessContextAssemblyStage> Stages { get; }

    /// <summary>The required entry ids as of the latest observed snapshot.</summary>
    internal IReadOnlyList<string> RequiredEntryIds { get; }

    /// <summary>The <see cref="HarnessContextSnapshot.Version"/> last observed during this assembly.</summary>
    internal long LatestSnapshotVersion { get; }

    /// <summary>
    /// The verification result for <see cref="FinalEntries"/> against the latest observed snapshot.
    /// Always <see langword="null"/> on a termination.
    /// </summary>
    internal HarnessCompactionVerificationResult? FinalVerification { get; }

    /// <exception cref="ArgumentNullException">Any reference-typed argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="outcome"/> is not a success outcome; <paramref name="finalVerification"/> is not
    /// accepted (a rejected verification indicates an invalid tool sequence or missing required entry —
    /// the assembler must return a structured termination instead of forwarding invalid context); or
    /// <paramref name="finalEstimatedSize"/> exceeds <paramref name="hardLimit"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="originalEstimatedSize"/>, <paramref name="finalEstimatedSize"/>,
    /// <paramref name="attemptCount"/>, or <paramref name="latestSnapshotVersion"/> is negative.
    /// </exception>
    internal static HarnessContextAssemblyResult Success(
        HarnessContextAssemblyOutcome outcome,
        IReadOnlyList<HarnessContextEntry> finalEntries,
        int originalEstimatedSize,
        int finalEstimatedSize,
        int hardLimit,
        int attemptCount,
        IReadOnlyList<HarnessContextAssemblyStage> stages,
        IReadOnlyList<string> requiredEntryIds,
        long latestSnapshotVersion,
        HarnessCompactionVerificationResult finalVerification)
    {
        ArgumentNullException.ThrowIfNull(finalEntries);
        ArgumentNullException.ThrowIfNull(stages);
        ArgumentNullException.ThrowIfNull(requiredEntryIds);
        ArgumentNullException.ThrowIfNull(finalVerification);

        if (outcome is not (HarnessContextAssemblyOutcome.WithinLimit
            or HarnessContextAssemblyOutcome.Reduced
            or HarnessContextAssemblyOutcome.PreservationFallback))
        {
            throw new ArgumentException($"'{outcome}' is not a success outcome.", nameof(outcome));
        }

        if (originalEstimatedSize < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(originalEstimatedSize), originalEstimatedSize,
                "Original estimated size must not be negative.");
        }

        if (finalEstimatedSize < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(finalEstimatedSize), finalEstimatedSize,
                "Final estimated size must not be negative.");
        }

        if (finalEstimatedSize > hardLimit)
        {
            throw new ArgumentException(
                "A success result's final estimated size must not exceed the hard limit.",
                nameof(finalEstimatedSize));
        }

        if (attemptCount < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(attemptCount), attemptCount,
                "Attempt count must not be negative.");
        }

        if (latestSnapshotVersion < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(latestSnapshotVersion), latestSnapshotVersion,
                "Latest snapshot version must not be negative.");
        }

        if (!finalVerification.IsAccepted)
        {
            throw new ArgumentException(
                "A success result requires an accepted final verification. A rejected verification " +
                "indicates an invalid tool sequence or missing required entry; the assembler must " +
                "return a structured Irreducible termination instead of forwarding invalid context.",
                nameof(finalVerification));
        }

        // Defensive copy/wrap: never share the caller's finalEntries list or entry instances,
        // stages list, or requiredEntryIds list. This result's "immutable" claim must hold even if
        // the caller continues to hold and mutate the collections it passed in.
        var copiedFinalEntries = new List<HarnessContextEntry>(finalEntries.Count);
        foreach (var entry in finalEntries)
        {
            copiedFinalEntries.Add(entry.Copy());
        }

        var copiedStages = new List<HarnessContextAssemblyStage>(stages).AsReadOnly();
        var copiedRequiredEntryIds = new List<string>(requiredEntryIds).AsReadOnly();

        return new HarnessContextAssemblyResult(
            outcome,
            copiedFinalEntries.AsReadOnly(),
            originalEstimatedSize,
            finalEstimatedSize,
            hardLimit,
            attemptCount,
            copiedStages,
            copiedRequiredEntryIds,
            latestSnapshotVersion,
            finalVerification);
    }

    /// <exception cref="ArgumentNullException">Any reference-typed argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="outcome"/> is not a termination outcome.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="originalEstimatedSize"/>, <paramref name="finalEstimatedSize"/>,
    /// <paramref name="attemptCount"/>, or <paramref name="latestSnapshotVersion"/> is negative.
    /// </exception>
    internal static HarnessContextAssemblyResult Terminated(
        HarnessContextAssemblyOutcome outcome,
        int originalEstimatedSize,
        int finalEstimatedSize,
        int hardLimit,
        int attemptCount,
        IReadOnlyList<HarnessContextAssemblyStage> stages,
        IReadOnlyList<string> requiredEntryIds,
        long latestSnapshotVersion)
    {
        ArgumentNullException.ThrowIfNull(stages);
        ArgumentNullException.ThrowIfNull(requiredEntryIds);

        if (outcome is not (HarnessContextAssemblyOutcome.Irreducible
            or HarnessContextAssemblyOutcome.ConcurrentMutationLimit))
        {
            throw new ArgumentException($"'{outcome}' is not a termination outcome.", nameof(outcome));
        }

        if (originalEstimatedSize < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(originalEstimatedSize), originalEstimatedSize,
                "Original estimated size must not be negative.");
        }

        if (finalEstimatedSize < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(finalEstimatedSize), finalEstimatedSize,
                "Final estimated size must not be negative.");
        }

        if (attemptCount < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(attemptCount), attemptCount,
                "Attempt count must not be negative.");
        }

        if (latestSnapshotVersion < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(latestSnapshotVersion), latestSnapshotVersion,
                "Latest snapshot version must not be negative.");
        }

        // Defensive copy/wrap: never share the caller's stages or requiredEntryIds list, so this
        // result's "immutable" claim holds even if the caller continues to hold and mutate what it
        // passed in.
        var copiedStages = new List<HarnessContextAssemblyStage>(stages).AsReadOnly();
        var copiedRequiredEntryIds = new List<string>(requiredEntryIds).AsReadOnly();

        return new HarnessContextAssemblyResult(
            outcome,
            finalEntries: null,
            originalEstimatedSize,
            finalEstimatedSize,
            hardLimit,
            attemptCount,
            copiedStages,
            copiedRequiredEntryIds,
            latestSnapshotVersion,
            finalVerification: null);
    }
}
