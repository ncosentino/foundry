using System.Text.Json;

using Microsoft.Extensions.AI;

namespace NexusLabs.Foundry.Evaluation.Harness.Judging;

/// <summary>
/// Executes the versioned post-hoc nominal pairwise rubric against captured Harness artifacts.
/// </summary>
public sealed class HarnessPairwiseJudgeEvaluator
{
    private readonly IChatClient _chatClient;
    private readonly HarnessJudgeAssets _assets;
    private readonly string _judgeModelId;
    private readonly string _judgeModelFamily;

    /// <summary>
    /// Initializes one pairwise judge evaluator.
    /// </summary>
    /// <param name="chatClient">The advisory judge chat client.</param>
    /// <param name="assets">The validated versioned judge assets.</param>
    /// <param name="judgeModelId">The judge model identifier.</param>
    /// <param name="judgeModelFamily">The judge model family.</param>
    /// <exception cref="ArgumentException">A model value is blank.</exception>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="chatClient"/> or <paramref name="assets"/> is <see langword="null"/>.
    /// </exception>
    public HarnessPairwiseJudgeEvaluator(
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
    /// Evaluates one captured pair.
    /// </summary>
    /// <param name="request">The captured pairwise evidence.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The structured advisory result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is <see langword="null"/>.</exception>
    /// <exception cref="HarnessJudgeEvaluationException">The judge response is invalid.</exception>
    public async ValueTask<HarnessPairwiseJudgeResult> EvaluateAsync(
        HarnessPairwiseJudgeRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var rubric = _assets.NominalRubric;
        var messages = new ChatMessage[]
        {
            new(
                ChatRole.System,
                string.Join(Environment.NewLine, rubric.Instructions) +
                Environment.NewLine +
                "Return only JSON with string properties 'preference' and 'reason'."),
            new(
                ChatRole.User,
                $"""
                CASE ID:
                {request.CaseId}

                CASE PROMPT:
                {request.CasePrompt}

                DETERMINISTIC REFERENCE:
                {request.DeterministicReference}

                LEFT TRAJECTORY:
                {request.LeftTrajectory}

                LEFT RESPONSE:
                {request.LeftResponse}

                RIGHT TRAJECTORY:
                {request.RightTrajectory}

                RIGHT RESPONSE:
                {request.RightResponse}
                """),
        };
        var response = await _chatClient
            .GetResponseAsync(messages, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        var (preference, reason) = Parse(response.Text, rubric);
        return new HarnessPairwiseJudgeResult(
            preference,
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

    private static (HarnessPairwisePreference Preference, string Reason) Parse(
        string? response,
        HarnessJudgeRubric rubric)
    {
        if (string.IsNullOrWhiteSpace(response))
        {
            throw new HarnessJudgeEvaluationException("The pairwise judge returned an empty response.");
        }

        try
        {
            using var document = JsonDocument.Parse(response);
            var preferenceText = document.RootElement.GetProperty("preference").GetString();
            var reason = document.RootElement.GetProperty("reason").GetString();
            if (!Enum.TryParse<HarnessPairwisePreference>(
                    preferenceText,
                    ignoreCase: false,
                    out var preference) ||
                !Enum.IsDefined(preference) ||
                !rubric.Labels.Contains(preference.ToString(), StringComparer.Ordinal))
            {
                throw new HarnessJudgeEvaluationException(
                    $"The pairwise judge returned unsupported preference '{preferenceText}'.");
            }

            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new HarnessJudgeEvaluationException("The pairwise judge returned a blank reason.");
            }

            return (preference, reason);
        }
        catch (JsonException exception)
        {
            throw new HarnessJudgeEvaluationException(
                "The pairwise judge response was not valid JSON.",
                exception);
        }
        catch (KeyNotFoundException exception)
        {
            throw new HarnessJudgeEvaluationException(
                "The pairwise judge response omitted a required property.",
                exception);
        }
        catch (InvalidOperationException exception)
        {
            throw new HarnessJudgeEvaluationException(
                "The pairwise judge response used the wrong JSON value type.",
                exception);
        }
    }
}
