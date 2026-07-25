using NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Context;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Tests.Harness;

/// <summary>
/// Shared wiring for <see cref="HarnessContextAssembler"/> tests: builds an assembler over a
/// <see cref="HarnessMutableContextSnapshotProvider"/> and a <see cref="HarnessScriptedContextReducer"/>,
/// and a reducer callback that always returns its input unchanged (the deterministic "no-op" proposal
/// used by every test that expects the reducer's output to be rejected as non-progressing).
/// </summary>
internal static class HarnessAssemblerTestFixture
{
    internal static (
        HarnessContextAssembler Assembler,
        HarnessMutableContextSnapshotProvider Provider,
        HarnessScriptedContextReducer Reducer) Build(
        HarnessHybridContextPolicy policy,
        IReadOnlyList<HarnessContextEntry> initialEntries,
        Func<HarnessContextReductionRequest, CancellationToken, Task<IReadOnlyList<HarnessContextEntry>>> reducerCallback)
    {
        var provider = new HarnessMutableContextSnapshotProvider(initialEntries);
        var reducer = new HarnessScriptedContextReducer(reducerCallback);
        var assembler = new HarnessContextAssembler(policy, provider, reducer);
        return (assembler, provider, reducer);
    }

    /// <summary>A reducer callback that always proposes its exact input entries back, unchanged.</summary>
    internal static Task<IReadOnlyList<HarnessContextEntry>> Unchanged(
        HarnessContextReductionRequest request, CancellationToken cancellationToken) =>
        Task.FromResult(request.Entries);

    /// <summary>Every entry in <paramref name="entries"/> except the ones whose id is in <paramref name="excludedEntryIds"/>.</summary>
    internal static IReadOnlyList<HarnessContextEntry> Without(
        IReadOnlyList<HarnessContextEntry> entries, params string[] excludedEntryIds)
    {
        var excluded = new HashSet<string>(excludedEntryIds, StringComparer.Ordinal);
        return [.. entries.Where(entry => !excluded.Contains(entry.EntryId))];
    }
}
