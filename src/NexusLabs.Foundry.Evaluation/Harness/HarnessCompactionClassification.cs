using NexusLabs.Foundry.MicrosoftAgentFramework.Diagnostics;

namespace NexusLabs.Foundry.Evaluation.Harness;

/// <summary>
/// Classifies a <see cref="HarnessContextCompactionOutcome"/> as a dispatchable success or a
/// non-dispatchable termination, matching the split enforced by the Harness context diagnostics
/// factories.
/// </summary>
internal static class HarnessCompactionClassification
{
    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="outcome"/> is a dispatchable success
    /// outcome (<see cref="HarnessContextCompactionOutcome.WithinLimit"/>,
    /// <see cref="HarnessContextCompactionOutcome.Reduced"/>, or
    /// <see cref="HarnessContextCompactionOutcome.PreservationFallback"/>).
    /// </summary>
    /// <param name="outcome">The compaction outcome to classify.</param>
    /// <returns><see langword="true"/> for a success outcome; otherwise <see langword="false"/>.</returns>
    public static bool IsSuccess(HarnessContextCompactionOutcome outcome) =>
        outcome is HarnessContextCompactionOutcome.WithinLimit
            or HarnessContextCompactionOutcome.Reduced
            or HarnessContextCompactionOutcome.PreservationFallback;
}
