using NexusLabs.Foundry.MicrosoftAgentFramework.Diagnostics;

namespace NexusLabs.Foundry.Evaluation.Harness;

/// <summary>
/// Per-item evidence for the artifact/context cost-attribution dimension. It carries the privacy-safe
/// UTF-8 byte attribution accumulated across artifact decisions plus the context size the compaction
/// pipeline finally admitted, so a per-item attributed cost can be emitted for downstream paired
/// comparison without exposing any content.
/// </summary>
public sealed record HarnessCostAttributionEvidence
{
    /// <summary>Gets the total UTF-8 input bytes observed across attributed artifact decisions.</summary>
    public required long ArtifactInputUtf8Bytes { get; init; }

    /// <summary>Gets the total UTF-8 artifact-derived output bytes committed to context.</summary>
    public required long ArtifactOutputUtf8Bytes { get; init; }

    /// <summary>Gets the original context size before compaction admitted the final bounded set.</summary>
    public required long ContextOriginalSize { get; init; }

    /// <summary>Gets the final admitted context size.</summary>
    public required long ContextFinalSize { get; init; }

    /// <summary>Gets the attributed cumulative token cost for the run.</summary>
    public required long AttributedTokenCost { get; init; }

    /// <summary>Gets the unit the context sizes are expressed in.</summary>
    public required HarnessContextMeasurementUnit MeasurementUnit { get; init; }
}
