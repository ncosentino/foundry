using Microsoft.Extensions.AI;

using NexusLabs.Foundry.Evaluation.Harness;
using NexusLabs.Foundry.Evaluation.Harness.Judging;
using NexusLabs.Foundry.Evaluation.Tests;

namespace NexusLabs.Foundry.Evaluation.Tests.Harness.Judging;

public sealed class HarnessJudgeEvaluatorTests
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public void AssetLoader_ProvisionalCalibration_RemainsUncalibratedAndNonRanking()
    {
        var assets = HarnessJudgeAssetLoader.Load(FindJudgeDirectory());

        Assert.Equal(HarnessJudgeCalibrationState.Uncalibrated, assets.CalibrationState);
        Assert.False(assets.UsableForArmRanking);
        Assert.Equal(0.6, assets.MinimumKappa);
        Assert.Equal(0, assets.EligibleCalibrationItemCount);
        Assert.Equal(7, assets.ProvisionalCalibrationItemCount);
        Assert.Equal("harness-nominal-pairwise-preference", assets.NominalRubric.Id);
        Assert.Equal("harness-ordinal-response-quality", assets.OrdinalRubric.Id);
    }

    [Fact]
    public async Task PairwiseEvaluator_ReturnsAdvisoryUncalibratedResult()
    {
        var assets = HarnessJudgeAssetLoader.Load(FindJudgeDirectory());
        using var client = new RecordingChatClient(
            new ChatResponse(new ChatMessage(
                ChatRole.Assistant,
                """{"preference":"Right","reason":"The right response is clearer."}""")));
        var evaluator = new HarnessPairwiseJudgeEvaluator(
            client,
            assets,
            judgeModelId: "judge-model-1",
            judgeModelFamily: "family-b");

        var result = await evaluator.EvaluateAsync(
            new HarnessPairwiseJudgeRequest(
                caseId: "h001-01",
                casePrompt: "Produce the required result.",
                deterministicReference: "Both required fields must be present.",
                generatorModelFamily: "family-a",
                leftResponse: "Partial result.",
                rightResponse: "Complete result.",
                leftTrajectory: "lookup_north",
                rightTrajectory: "lookup_north -> lookup_south -> combine_values"),
            _ct);

        Assert.Equal(HarnessPairwisePreference.Right, result.Preference);
        Assert.Equal(HarnessJudgeCalibrationState.Uncalibrated, result.CalibrationState);
        Assert.False(result.UsableForArmRanking);
        Assert.Equal("judge-model-1", result.JudgeModelId);
        Assert.Equal(assets.NominalRubric.Sha256, result.RubricSha256);
        Assert.True(result.UsesDifferentModelFamily);
        Assert.Equal(1, client.CallCount);
    }

    [Fact]
    public async Task OrdinalEvaluator_ReturnsBoundedAdvisoryScore()
    {
        var assets = HarnessJudgeAssetLoader.Load(FindJudgeDirectory());
        using var client = new RecordingChatClient(
            new ChatResponse(new ChatMessage(
                ChatRole.Assistant,
                """{"score":4,"reason":"Correct and clear."}""")));
        var evaluator = new HarnessOrdinalJudgeEvaluator(
            client,
            assets,
            judgeModelId: "judge-model-1",
            judgeModelFamily: "family-b");

        var result = await evaluator.EvaluateAsync(
            new HarnessOrdinalJudgeRequest(
                caseId: "h001-02",
                casePrompt: "Return the sentinel.",
                deterministicReference: "The exact sentinel is required.",
                generatorModelFamily: "family-a",
                response: "CONTEXT-SENTINEL-7F3A",
                trajectory: "read_context"),
            _ct);

        Assert.Equal(4, result.Score);
        Assert.Equal(HarnessJudgeCalibrationState.Uncalibrated, result.CalibrationState);
        Assert.False(result.UsableForArmRanking);
        Assert.Equal(assets.OrdinalRubric.Sha256, result.RubricSha256);
        Assert.True(result.UsesDifferentModelFamily);
    }

    [Fact]
    public async Task PairwiseEvaluator_MalformedOutput_ThrowsExplicitly()
    {
        var assets = HarnessJudgeAssetLoader.Load(FindJudgeDirectory());
        using var client = new RecordingChatClient(
            new ChatResponse(new ChatMessage(ChatRole.Assistant, "Right is better.")));
        var evaluator = new HarnessPairwiseJudgeEvaluator(
            client,
            assets,
            judgeModelId: "judge-model-1",
            judgeModelFamily: "family-b");

        await Assert.ThrowsAsync<HarnessJudgeEvaluationException>(() =>
            evaluator.EvaluateAsync(
                new HarnessPairwiseJudgeRequest(
                    caseId: "h001-01",
                    casePrompt: "Produce the required result.",
                    deterministicReference: "Both fields are required.",
                    generatorModelFamily: "family-a",
                    leftResponse: "Partial.",
                    rightResponse: "Complete.",
                    leftTrajectory: "lookup_north",
                    rightTrajectory: "lookup_north -> lookup_south"),
                _ct).AsTask());
    }

    private static string FindJudgeDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(
                directory.FullName,
                "artifacts",
                "eval",
                "case-sets",
                "harness-001",
                "v1.0",
                "judges");
            if (File.Exists(Path.Combine(candidate, "manifest.json")))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the harness-001 v1.0 judge directory.");
    }
}
