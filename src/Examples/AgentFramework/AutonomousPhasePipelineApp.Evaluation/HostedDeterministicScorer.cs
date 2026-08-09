using System.Text.Json;

using AutonomousPhasePipelineApp.Core;

namespace AutonomousPhasePipelineApp.Evaluation;

internal static class HostedDeterministicScorer
{
    internal static (
        HostedEvaluationCorrectness Correctness,
        string? OutputText) Score(
        ReferenceArtifactStore artifacts,
        string runId,
        HostedEvaluationFixtureData fixture,
        ReferencePipelineResult? result,
        int authoritativeDeliveries)
    {
        HostedEvaluationExpectedOutput expected = fixture.Expected;
        ReferenceArtifactManifest manifest = artifacts.GetManifest(
            fixture.Manifest.Artifact!,
            runId);
        string? outputText = result?.Synthesis.Artifact is { } synthesis
            ? artifacts.Read(runId, synthesis.Id)
            : null;

        bool outputExpected =
            expected.SynthesisOutcome ==
                ReferencePipelineOutcome.Completed;
        bool artifactContractValid;
        bool evidenceExact;
        bool gapsExact;
        string? validationError;
        if (outputExpected)
        {
            artifactContractValid =
                ReferenceArtifactValidator.TryNormalizeSynthesis(
                    outputText,
                    manifest,
                    out _,
                    out string error);
            validationError = artifactContractValid
                ? null
                : error;
            evidenceExact = TryReadExactStringSet(
                outputText,
                "evidence",
                expected.EvidenceIds);
            gapsExact = TryReadExactStringSet(
                outputText,
                "gaps",
                expected.Gaps);
        }
        else
        {
            artifactContractValid =
                result?.Synthesis.Outcome ==
                    expected.SynthesisOutcome;
            evidenceExact = outputText is null;
            gapsExact = ExactSet(
                result?.Gaps ?? [],
                expected.Gaps);
            validationError = result?.Synthesis.Error;
        }

        bool expectedRequiredCoverage = manifest.Branches
            .Where(branch => branch.Required)
            .All(branch =>
                branch.Outcome ==
                    ReferencePipelineOutcome.Completed);
        bool actualRequiredCoverage = result?.Branches
            .Where(branch => branch.Required)
            .All(branch =>
                branch.Outcome ==
                    ReferencePipelineOutcome.Completed) == true;
        bool requiredCoverageMatches =
            actualRequiredCoverage == expectedRequiredCoverage;
        bool branchesExact = BranchesMatch(
            result?.Branches,
            manifest.Branches);
        bool siblingsPreserved =
            expected.PreservedSiblingPhases.All(
                phase => result?.Branches.Any(
                    branch =>
                        string.Equals(
                            branch.Phase,
                            phase,
                            StringComparison.Ordinal) &&
                        branch.Artifact is not null) == true);
        bool pipelineOutcomeMatches =
            result?.Outcome == expected.PipelineOutcome;
        bool synthesisOutcomeMatches =
            result?.Synthesis.Outcome ==
                expected.SynthesisOutcome;
        bool deliveryExact =
            authoritativeDeliveries ==
                expected.AuthoritativeDeliveries;
        bool scenarioPass =
            artifactContractValid &&
            evidenceExact &&
            gapsExact &&
            branchesExact &&
            requiredCoverageMatches &&
            siblingsPreserved &&
            pipelineOutcomeMatches &&
            synthesisOutcomeMatches &&
            deliveryExact;

        return (
            new HostedEvaluationCorrectness(
                artifactContractValid,
                evidenceExact,
                gapsExact,
                branchesExact,
                requiredCoverageMatches,
                siblingsPreserved,
                pipelineOutcomeMatches,
                synthesisOutcomeMatches,
                deliveryExact,
                scenarioPass,
                validationError),
            outputText);
    }

    private static bool BranchesMatch(
        IReadOnlyCollection<ReferencePhaseArtifact>? actual,
        IReadOnlyCollection<ReferencePhaseArtifact> expected)
    {
        if (actual is null ||
            actual.Count != expected.Count)
        {
            return false;
        }

        return expected.All(expectedBranch =>
        {
            ReferencePhaseArtifact[] matches = actual
                .Where(branch => string.Equals(
                    branch.Phase,
                    expectedBranch.Phase,
                    StringComparison.Ordinal))
                .Take(2)
                .ToArray();
            return matches.Length == 1 &&
                matches[0] is { } actualBranch &&
                actualBranch.Ordinal == expectedBranch.Ordinal &&
                actualBranch.Required == expectedBranch.Required &&
                actualBranch.Outcome == expectedBranch.Outcome &&
                string.Equals(
                    actualBranch.Artifact?.Id,
                    expectedBranch.Artifact?.Id,
                    StringComparison.Ordinal) &&
                ExactSet(
                    actualBranch.Gaps,
                    expectedBranch.Gaps);
        });
    }

    private static bool TryReadExactStringSet(
        string? content,
        string propertyName,
        IReadOnlyCollection<string> expected)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return false;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(content);
            if (!document.RootElement.TryGetProperty(
                    propertyName,
                    out JsonElement property) ||
                property.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            string?[] values = property
                .EnumerateArray()
                .Select(item =>
                    item.ValueKind == JsonValueKind.String
                        ? item.GetString()
                        : null)
                .ToArray();
            return values.All(value => value is not null) &&
                ExactSet(
                    values.OfType<string>().ToArray(),
                    expected);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool ExactSet(
        IReadOnlyCollection<string> actual,
        IReadOnlyCollection<string> expected) =>
        actual.Count == expected.Count &&
        new HashSet<string>(
            actual,
            StringComparer.Ordinal).SetEquals(expected);
}
