namespace AutonomousPhasePipelineApp.Core;

internal static class ReferenceSynthesisPrompt
{
    internal static string Build(
        ReferenceArtifactReference manifestReference,
        ReferenceArtifactManifest manifest)
    {
        string branches = string.Join(
            ",",
            manifest.Branches.Select(
                branch => $"{branch.Phase}:{branch.Outcome}"));
        string gaps = manifest.Gaps.Length == 0
            ? "none"
            : string.Join(",", manifest.Gaps);
        return
            $"""
            Build the synthesis artifact from the accepted manifest.
            {SynthesisPhaseExecutor.ManifestIdPrefix}{manifestReference.Id}
            manifest_outcome={manifest.Outcome}
            branch_outcomes={branches}
            explicit_gaps={gaps}
            Resolve artifact bodies through the manifest tool; no prior transcript is authoritative.
            """;
    }

    internal static string WithCorrection(
        string task,
        string candidate,
        string error) =>
        $"""
        {task}

        {ReferenceArtifactValidator.CreateSynthesisCorrection(error)}
        The prior candidate was:
        {candidate}

        Return a corrected JSON artifact that satisfies the accepted manifest.
        """;
}
