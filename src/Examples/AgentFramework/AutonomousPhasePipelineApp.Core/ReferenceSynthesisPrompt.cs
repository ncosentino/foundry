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
            manifest_id={manifestReference.Id}
            manifest_outcome={manifest.Outcome}
            branch_outcomes={branches}
            explicit_gaps={gaps}
            Resolve artifact bodies through the manifest tool; no prior transcript is authoritative.
            """;
    }
}
