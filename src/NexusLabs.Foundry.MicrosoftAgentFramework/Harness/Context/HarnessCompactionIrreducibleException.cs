namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Context;

/// <summary>
/// Thrown by <see cref="HarnessHybridCompactionChatClient"/> when
/// <see cref="HarnessContextAssembler.AssembleAsync"/> returns a non-success
/// (<see cref="HarnessContextAssemblyOutcome.Irreducible"/> or
/// <see cref="HarnessContextAssemblyOutcome.ConcurrentMutationLimit"/>) result for the current provider
/// call. An over-budget or invalid context is never forwarded to the real provider client and never
/// surfaced as a successful agent response — this exception is the explicit, structured failure instead.
/// </summary>
internal sealed class HarnessCompactionIrreducibleException : Exception
{
    internal HarnessCompactionIrreducibleException(
        HarnessContextAssemblyOutcome outcome, int finalEstimatedSize, int hardLimit)
        : base(
            $"Hybrid compaction could not produce dispatch-eligible context for this provider call " +
            $"(outcome '{outcome}', estimated size {finalEstimatedSize} against hard limit {hardLimit}). " +
            "Over-budget or invalid context is never forwarded to the provider.")
    {
        Outcome = outcome;
        FinalEstimatedSize = finalEstimatedSize;
        HardLimit = hardLimit;
    }

    /// <summary>The exact non-success outcome the assembler returned.</summary>
    internal HarnessContextAssemblyOutcome Outcome { get; }

    /// <summary>The estimated size of the terminating fallback candidate, if any.</summary>
    internal int FinalEstimatedSize { get; }

    /// <summary>The hard limit in force for this assembly.</summary>
    internal int HardLimit { get; }
}
