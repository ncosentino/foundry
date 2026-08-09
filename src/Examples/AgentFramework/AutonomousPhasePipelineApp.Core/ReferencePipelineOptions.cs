namespace AutonomousPhasePipelineApp.Core;

internal sealed record ReferencePipelineOptions
{
    internal required bool FailRequiredSpecialist { get; init; }

    internal required bool FailOptionalSpecialist { get; init; }

    internal required bool HoldSpecialistsUntilCancellation { get; init; }

    internal required bool HoldBackgroundWorkersUntilRelease { get; init; }

    internal required ReferenceSynthesisExecutorKind SynthesisExecutorKind { get; init; }

    internal required int SynthesisMaxArtifactAttempts { get; init; }

    internal required int SynthesisMaxProviderCalls { get; init; }

    internal static ReferencePipelineOptions Default { get; } = new()
    {
        FailRequiredSpecialist = false,
        FailOptionalSpecialist = false,
        HoldSpecialistsUntilCancellation = false,
        HoldBackgroundWorkersUntilRelease = false,
        SynthesisExecutorKind = ReferenceSynthesisExecutorKind.Harness,
        SynthesisMaxArtifactAttempts = 2,
        SynthesisMaxProviderCalls = 24,
    };
}
