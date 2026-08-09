namespace AutonomousPhasePipelineApp.Core;

internal sealed record ReferencePhaseArtifact(
    string Phase,
    int Ordinal,
    bool Required,
    ReferencePipelineOutcome Outcome,
    ReferenceArtifactReference? Artifact,
    string? CandidateContent,
    ReferenceArtifactReference[] Inputs,
    string[] Gaps,
    string? Error)
{
    internal static ReferencePhaseArtifact Candidate(
        string phase,
        int ordinal,
        bool required,
        string content,
        params ReferenceArtifactReference[] inputs) =>
        new(
            phase,
            ordinal,
            required,
            ReferencePipelineOutcome.Completed,
            Artifact: null,
            CandidateContent: content,
            Inputs: inputs,
            Gaps: [],
            Error: null);

    internal static ReferencePhaseArtifact WithArtifact(
        ReferencePhaseArtifact candidate,
        ReferenceArtifactReference artifact) =>
        WithArtifact(
            candidate,
            artifact,
            ReferencePipelineOutcome.Completed,
            candidate.Gaps);

    internal static ReferencePhaseArtifact WithArtifact(
        ReferencePhaseArtifact candidate,
        ReferenceArtifactReference artifact,
        ReferencePipelineOutcome outcome,
        string[] gaps) =>
        candidate with
        {
            Outcome = outcome,
            Artifact = artifact,
            CandidateContent = null,
            Gaps = gaps,
            Error = null,
        };

    internal static ReferencePhaseArtifact Failed(
        string phase,
        int ordinal,
        bool required,
        string error,
        ReferenceArtifactReference[] inputs,
        params string[] gaps) =>
        new(
            phase,
            ordinal,
            required,
            ReferencePipelineOutcome.Failed,
            Artifact: null,
            CandidateContent: null,
            Inputs: inputs,
            Gaps: gaps.Length == 0 ? [$"{phase}:{error}"] : gaps,
            Error: error);

    internal static ReferencePhaseArtifact Skipped(
        string phase,
        int ordinal,
        bool required,
        string reason,
        params ReferenceArtifactReference[] inputs) =>
        new(
            phase,
            ordinal,
            required,
            ReferencePipelineOutcome.Skipped,
            Artifact: null,
            CandidateContent: null,
            Inputs: inputs,
            Gaps: [$"{phase}:{reason}"],
            Error: reason);
}
