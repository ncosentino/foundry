using System.Text.Json;

using AutonomousPhasePipelineApp.Core;

namespace AutonomousPhasePipelineApp.Evaluation;

internal static class HostedDeterministicScorer
{
    internal static (
        bool SchemaValid,
        bool RequiredCoverage,
        bool GapCorrect,
        bool SiblingPreserved,
        bool EvidenceValid,
        string? OutputText) Score(
        ReferenceArtifactStore artifacts,
        string runId,
        HostedEvaluationScenario scenario,
        HostedEvaluationFixtureData fixture,
        ReferencePipelineResult? result)
    {
        if (result is null)
        {
            return (
                SchemaValid: false,
                RequiredCoverage: false,
                GapCorrect: false,
                SiblingPreserved: false,
                EvidenceValid: false,
                OutputText: null);
        }

        string? outputText = result.Synthesis.Artifact is { } synthesis
            ? artifacts.Read(runId, synthesis.Id)
            : null;
        bool schemaValid =
            result.Synthesis.Outcome == ReferencePipelineOutcome.Skipped ||
            ReferenceArtifactValidator.TryValidateSynthesis(
                outputText,
                out _);
        bool requiredCoverage = result.Branches
            .Where(branch => branch.Required)
            .All(branch =>
                branch.Outcome == ReferencePipelineOutcome.Completed);
        bool gapCorrect = scenario switch
        {
            HostedEvaluationScenario.OptionalBranchFailure =>
                result.Gaps.Any(gap => gap.Contains(
                    "operations",
                    StringComparison.Ordinal)),
            HostedEvaluationScenario.RequiredBranchFailure =>
                result.Gaps.Any(gap => gap.Contains(
                    "risk",
                    StringComparison.Ordinal)),
            _ => result.Gaps.Length == fixture.ExpectedGaps.Length,
        };
        bool siblingPreserved = scenario switch
        {
            HostedEvaluationScenario.OptionalBranchFailure =>
                result.Branches.Any(branch =>
                    branch.Phase == "risk" &&
                    branch.Artifact is not null),
            HostedEvaluationScenario.RequiredBranchFailure =>
                result.Branches.Any(branch =>
                    branch.Phase == "operations" &&
                    branch.Artifact is not null),
            _ => true,
        };
        bool evidenceValid = outputText is null
            ? result.Synthesis.Outcome == ReferencePipelineOutcome.Skipped
            : EvidenceIsAuthorized(
                outputText,
                fixture.AcceptedEvidenceIds);
        return (
            schemaValid,
            requiredCoverage,
            gapCorrect,
            siblingPreserved,
            evidenceValid,
            outputText);
    }

    private static bool EvidenceIsAuthorized(
        string output,
        IReadOnlyList<string> acceptedEvidenceIds)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(output);
            if (!document.RootElement.TryGetProperty(
                    "evidence",
                    out JsonElement evidence) ||
                evidence.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            var accepted = new HashSet<string>(
                acceptedEvidenceIds,
                StringComparer.Ordinal);
            return evidence
                .EnumerateArray()
                .Select(item => item.GetString())
                .All(item =>
                    item is not null &&
                    accepted.Contains(item));
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
