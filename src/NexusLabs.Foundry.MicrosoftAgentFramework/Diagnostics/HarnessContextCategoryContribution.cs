namespace NexusLabs.Foundry.MicrosoftAgentFramework.Diagnostics;

/// <summary>
/// One category's contribution to a successful compaction/assembly decision's final size. The sum of
/// every <see cref="Size"/> across a <see cref="HarnessContextDiagnostics"/> instance's
/// <see cref="HarnessContextDiagnostics.CategoryContributions"/> always equals that instance's
/// <see cref="HarnessContextDiagnostics.FinalSize"/> exactly, because both are computed from the same
/// final entries using the same estimator that governed the policy decision.
/// </summary>
/// <remarks>
/// Instances are created exclusively by the <c>internal</c> <see cref="Create"/> factory, which
/// enforces every validity invariant. There is no public constructor.
/// </remarks>
public sealed record HarnessContextCategoryContribution
{
    private HarnessContextCategoryContribution(
        HarnessContextCategory category,
        int size,
        int entryCount)
    {
        Category = category;
        Size = size;
        EntryCount = entryCount;
    }

    /// <summary>The structural category this contribution describes.</summary>
    public HarnessContextCategory Category { get; }

    /// <summary>
    /// The non-negative total estimated size, in <see cref="HarnessContextDiagnostics.MeasurementUnit"/>,
    /// of every final entry belonging to <see cref="Category"/>.
    /// </summary>
    public int Size { get; }

    /// <summary>The positive count of final entries belonging to <see cref="Category"/>.</summary>
    public int EntryCount { get; }

    /// <summary>
    /// Creates and validates a <see cref="HarnessContextCategoryContribution"/> instance.
    /// </summary>
    /// <param name="category">The structural category.</param>
    /// <param name="size">The non-negative total estimated size for this category.</param>
    /// <param name="entryCount">The positive entry count for this category.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="category"/> is not a defined <see cref="HarnessContextCategory"/> value;
    /// <paramref name="size"/> is negative; or <paramref name="entryCount"/> is not positive.
    /// </exception>
    internal static HarnessContextCategoryContribution Create(
        HarnessContextCategory category, int size, int entryCount)
    {
        if (!Enum.IsDefined(category))
        {
            throw new ArgumentOutOfRangeException(
                nameof(category), category, "The category is not a defined HarnessContextCategory value.");
        }

        if (size < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(size), size, "Category contribution size must not be negative.");
        }

        if (entryCount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(entryCount), entryCount, "Category contribution entry count must be positive.");
        }

        return new HarnessContextCategoryContribution(category, size, entryCount);
    }
}
