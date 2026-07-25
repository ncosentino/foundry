namespace NexusLabs.Foundry.MicrosoftAgentFramework.Tools;

/// <summary>
/// Bounded strategy producing the human-readable <c>description</c> field recorded on a fresh
/// <see cref="Harness.Context.HarnessArtifactReference"/>. Receives only the tool/function name
/// and call ID — never the raw tool result content — so a caller-supplied strategy structurally
/// cannot leak oversized or sensitive payload data into artifact metadata.
/// </summary>
/// <param name="toolName">The name of the tool/function whose result is being offloaded.</param>
/// <param name="callId">The call ID of the tool invocation being offloaded.</param>
/// <returns>
/// A human-readable description. <see cref="HarnessToolResultOffloadTransform"/> always truncates
/// this to <see cref="Harness.Context.HarnessArtifactReference.MaximumDescriptionLength"/> before
/// constructing a reference, so a caller-supplied strategy cannot itself violate the bound.
/// </returns>
internal delegate string HarnessToolResultOffloadDescriptionStrategy(string toolName, string callId);

/// <summary>
/// The single bounded, constant default <see cref="HarnessToolResultOffloadDescriptionStrategy"/>.
/// Production call sites (<c>IterativeAgentLoop</c>, <c>HarnessProviderComposition</c>) use this by
/// default; it exists as an explicit, reviewable constant rather than an inline lambda so its
/// bounded shape is a single, testable source of truth.
/// </summary>
internal static class HarnessToolResultOffloadDescriptions
{
    /// <summary>
    /// Mirrors <see cref="Harness.Context.HarnessArtifactReference.MaximumDescriptionLength"/> —
    /// restated here so this file's bound is self-evident without following the reference type.
    /// </summary>
    internal const int MaximumLength = Harness.Context.HarnessArtifactReference.MaximumDescriptionLength;

    /// <summary>The bounded default description template.</summary>
    internal static string Default(string toolName, string callId)
    {
        var description = $"Offloaded tool result for '{toolName}' (call '{callId}').";
        return description.Length > MaximumLength
            ? description[..MaximumLength]
            : description;
    }
}
