using System.Text.Json;

using Microsoft.Extensions.AI;

namespace NexusLabs.Foundry.Evaluation.Harness.Judging;

/// <summary>
/// Executes the versioned post-hoc ordinal response-quality rubric against captured Harness artifacts.
/// </summary>
public sealed class HarnessOrdinalJudgeEvaluator
{
    private readonly IChatClient _chatClient;
    private readonly HarnessJudgeAssets _assets;
    private readonly string _judgeModelId;
    private readonly string _judgeModelFamily;

    /// <summary>
    /// Initializes one ordinal judge evaluator.
    /// </summary>
    /// <param name="chatClient">The advisory judge chat client.</param>
    /// <param name="assets">The validated versioned judge assets.</param>
    /// <param name="judgeModelId">The judge model identifier.</param>
    /// <param name="judgeModelFamily">The judge model family.</param>
    /// <exception cref="ArgumentException">A model value is blank.</exception>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="chatClient"/> or <paramref name="assets"/> is <see langword="null"/>.
    /// </exception>
    public HarnessOrdinalJudgeEvaluator(
        IChatClient chatClient,
        HarnessJudgeAssets assets,
        string judgeModelId,
        string judgeModelFamily)
    {
        ArgumentNullException.ThrowIfNull(chatClient);
        ArgumentNullException.ThrowIfNull(assets);
        ArgumentException.ThrowIfNullOrWhiteSpace(judgeModelId);
        ArgumentException.ThrowIfNullOrWhiteSpace(judgeModelFamily);
        _chatClient = chatClient;
        _assets = assets;
        _judgeModelId = judgeModelId;
        _judgeModelFamily = judgeModelFamily;
    }

    /// <summary>
    /// Evaluates one captured response.
    /// </summary>
    /// <param name="request">The captured ordinal evidence.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The structured advisory result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is <see langword="null"/>.</exception>
    /// <exception cref="HarnessJudgeEvaluationException">The judge response is invalid.</exception>
    public async ValueTask<HarnessOrdinalJudgeResult> EvaluateAsync(
        HarnessOrdinalJudgeRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var rubric = _assets.OrdinalRubric;
        var messages = new ChatMessage[]
        {
            new(
                ChatRole.System,
                string.Join(Environment.NewLine, rubric.Instructions) +
                Environment.NewLine +
                "Return only JSON with integer property 'score' and string property 'reason'."),
            new(
                ChatRole.User,
                $"""
                CASE ID:
                {request.CaseId}

                CASE PROMPT:
                {request.CasePrompt}

                DETERMINISTIC REFERENCE:
                {request.DeterministicReference}

                NORMALIZED TRAJECTORY:
                {request.Trajectory}

                FINAL RESPONSE:
                {request.Response}
                """),
        };
        var response = await _chatClient
            .GetResponseAsync(messages, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        var (score, reason) = Parse(response.Text, rubric);
        return new HarnessOrdinalJudgeResult(
            score,
            reason,
            _judgeModelId,
            _judgeModelFamily,
            !string.Equals(
                request.GeneratorModelFamily,
                _judgeModelFamily,
                StringComparison.OrdinalIgnoreCase),
            _assets.CalibrationState,
            rubric.Id,
            rubric.Version,
            rubric.Sha256);
    }

    private static (int Score, string Reason) Parse(
        string? response,
        HarnessJudgeRubric rubric)
    {
        if (string.IsNullOrWhiteSpace(response))
        {
            throw new HarnessJudgeEvaluationException("The ordinal judge returned an empty response.");
        }

        try
        {
            using var document = JsonDocument.Parse(response);
            var score = document.RootElement.GetProperty("score").GetInt32();
            var reason = document.RootElement.GetProperty("reason").GetString();
            if (!rubric.Scale.Contains(score))
            {
                throw new HarnessJudgeEvaluationException(
                    $"The ordinal judge returned unsupported score '{score}'.");
            }

            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new HarnessJudgeEvaluationException("The ordinal judge returned a blank reason.");
            }

            return (score, reason);
        }
        catch (JsonException exception)
        {
            throw new HarnessJudgeEvaluationException(
                "The ordinal judge response was not valid JSON.",
                exception);
        }
        catch (KeyNotFoundException exception)
        {
            throw new HarnessJudgeEvaluationException(
                "The ordinal judge response omitted a required property.",
                exception);
        }
        catch (InvalidOperationException exception)
        {
            throw new HarnessJudgeEvaluationException(
                "The ordinal judge response used the wrong JSON value type.",
                exception);
        }
        catch (FormatException exception)
        {
            throw new HarnessJudgeEvaluationException(
                "The ordinal judge score was outside the supported integer range.",
                exception);
        }
    }
}
