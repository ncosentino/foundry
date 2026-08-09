using System.Text.Json;

namespace AutonomousPhasePipelineApp.Core;

internal static class ReferenceSynthesisArtifacts
{
    internal static string Create(
        ReferenceArtifactManifest manifest,
        bool includeRecommendation)
    {
        string[] evidence =
        [
            manifest.Research.Id,
            .. manifest.Branches
                .Where(branch => branch.Outcome is
                    ReferencePipelineOutcome.Completed or
                    ReferencePipelineOutcome.Partial)
                .Select(branch =>
                    branch.Artifact?.Id ??
                    throw new InvalidOperationException(
                        $"Accepted branch '{branch.Phase}' has no artifact.")),
        ];
        var artifact = new Dictionary<string, object?>
        {
            ["summary"] = includeRecommendation
                ? "Synthesis completed from accepted artifacts."
                : "Initial synthesis is missing a required field.",
            ["evidence"] = evidence,
            ["gaps"] = manifest.Gaps,
        };
        if (includeRecommendation)
        {
            artifact["recommendation"] =
                "Proceed with the synthetic release.";
        }

        return JsonSerializer.Serialize(artifact);
    }
}
