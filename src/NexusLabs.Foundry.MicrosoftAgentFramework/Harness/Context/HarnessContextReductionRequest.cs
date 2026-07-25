namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Context;

/// <summary>
/// One explicit invocation of an <see cref="IHarnessContextReducer"/>: the exact current entries an
/// attempt must propose a reduced replacement for, the entry ids the configured
/// <see cref="HarnessHybridContextPolicy"/> currently requires, the policy itself, and this attempt's
/// 1-based ordinal within <see cref="HarnessContextAssembler"/>'s bounded recompaction loop.
/// </summary>
/// <remarks>
/// <see cref="RequiredEntryIds"/> is informational only: a reducer should never remove or mutate one of
/// these ids, but nothing here enforces that — <see cref="HarnessCompactionVerifier"/> is what actually
/// checks every proposal independently before <see cref="HarnessContextAssembler"/> ever forwards it.
/// <see cref="Create"/> never shares a caller-supplied <see cref="HarnessContextEntry"/> instance or
/// list: <see cref="Entries"/> is a read-only collection of entries independently deep-copied via
/// <see cref="HarnessContextEntry.Copy"/>, and <see cref="RequiredEntryIds"/> is likewise wrapped
/// read-only, so a reducer can never mutate the assembler's authoritative snapshot entries in place —
/// whether by casting either collection back to a mutable list type and replacing an element, or by
/// mutating a returned entry's <see cref="HarnessContextEntry.Message"/> — even though nothing prevents
/// a reducer from attempting such a cast on its own copy.
/// </remarks>
internal sealed record HarnessContextReductionRequest
{
    private HarnessContextReductionRequest(
        IReadOnlyList<HarnessContextEntry> entries,
        IReadOnlyList<string> requiredEntryIds,
        HarnessHybridContextPolicy policy,
        int attemptNumber)
    {
        Entries = entries;
        RequiredEntryIds = requiredEntryIds;
        Policy = policy;
        AttemptNumber = attemptNumber;
    }

    /// <summary>
    /// The exact current entries this attempt must propose a reduced replacement for: a read-only
    /// collection of independently deep-copied entries (see <see cref="Create"/>'s remarks), never
    /// the assembler's own authoritative snapshot entry instances.
    /// </summary>
    internal IReadOnlyList<HarnessContextEntry> Entries { get; }

    /// <summary>
    /// The entry ids <see cref="Policy"/> currently requires, in their original relative order, as a
    /// read-only collection independent of any caller-supplied list.
    /// </summary>
    internal IReadOnlyList<string> RequiredEntryIds { get; }

    /// <summary>The governing policy, exposed so a reducer can reason about the hard limit and margin.</summary>
    internal HarnessHybridContextPolicy Policy { get; }

    /// <summary>This attempt's 1-based ordinal within the bounded recompaction loop.</summary>
    internal int AttemptNumber { get; }

    /// <exception cref="ArgumentNullException">
    /// <paramref name="entries"/>, <paramref name="requiredEntryIds"/>, or <paramref name="policy"/> is
    /// <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="attemptNumber"/> is not positive.</exception>
    internal static HarnessContextReductionRequest Create(
        IReadOnlyList<HarnessContextEntry> entries,
        IReadOnlyList<string> requiredEntryIds,
        HarnessHybridContextPolicy policy,
        int attemptNumber)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(requiredEntryIds);
        ArgumentNullException.ThrowIfNull(policy);

        if (attemptNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(attemptNumber), attemptNumber, "The attempt number must be positive.");
        }

        // Deep defensive copy: a reducer must never be able to mutate the assembler's authoritative
        // snapshot entries in place — not by casting Entries back to a mutable list type and
        // replacing an element, and not by mutating a returned HarnessContextEntry's Message —
        // so every entry is copied via HarnessContextEntry.Copy() into a read-only collection this
        // request alone constructs and holds. RequiredEntryIds is likewise wrapped read-only, so a
        // reducer cannot replace/append its elements even after casting back to a concrete list.
        var copiedEntries = new List<HarnessContextEntry>(entries.Count);
        foreach (var entry in entries)
        {
            copiedEntries.Add(entry.Copy());
        }

        var copiedRequiredEntryIds = new List<string>(requiredEntryIds).AsReadOnly();

        return new HarnessContextReductionRequest(
            copiedEntries.AsReadOnly(), copiedRequiredEntryIds, policy, attemptNumber);
    }
}
