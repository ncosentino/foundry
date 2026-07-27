using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;

namespace NexusLabs.Foundry.Evaluation.Harness;

/// <summary>
/// Deterministic, per-item evaluator that scores telemetry completeness for one Harness run: the
/// expected per-call and aggregate counters were captured and no required telemetry field is missing.
/// </summary>
/// <remarks>
/// Reads the telemetry slice from the <see cref="HarnessRunEvaluationContext"/>. A <see langword="null"/>
/// slice returns an empty result; a present slice is scored, with any missing counter driving the
/// completeness metric to <see langword="false"/>.
/// </remarks>
public sealed class HarnessTelemetryCompletenessEvaluator : IEvaluator
{
    /// <summary>Metric name for the telemetry-completeness rollup.</summary>
    public const string TelemetryCompleteMetricName = "Harness Telemetry Complete";

    /// <summary>Metric name for the count of missing required telemetry fields.</summary>
    public const string MissingFieldCountMetricName = "Harness Telemetry Missing Field Count";

    /// <summary>Metric name for whether observed call counts matched expectations exactly.</summary>
    public const string CountsMatchMetricName = "Harness Telemetry Counts Match";

    /// <summary>Metric name for the observed chat-completion count.</summary>
    public const string ChatCompletionCountMetricName = "Harness Telemetry Chat Completion Count";

    /// <inheritdoc />
    public IReadOnlyCollection<string> EvaluationMetricNames { get; } =
    [
        TelemetryCompleteMetricName,
        MissingFieldCountMetricName,
        CountsMatchMetricName,
        ChatCompletionCountMetricName,
    ];

    /// <inheritdoc />
    public ValueTask<EvaluationResult> EvaluateAsync(
        IEnumerable<ChatMessage> messages,
        ChatResponse modelResponse,
        ChatConfiguration? chatConfiguration = null,
        IEnumerable<EvaluationContext>? additionalContext = null,
        CancellationToken cancellationToken = default)
    {
        var telemetry = additionalContext?
            .OfType<HarnessRunEvaluationContext>()
            .FirstOrDefault()?
            .Evidence.Telemetry;

        if (telemetry is null)
        {
            return new ValueTask<EvaluationResult>(new EvaluationResult());
        }

        var missingFieldsValid =
            telemetry.MissingFields is not null &&
            telemetry.MissingFields.All(field => !string.IsNullOrWhiteSpace(field));
        var missingFieldCount = telemetry.MissingFields?.Count ?? 1;
        var countersValid =
            telemetry.ExpectedChatCompletionCount >= 0 &&
            telemetry.ObservedChatCompletionCount >= 0 &&
            telemetry.ExpectedToolCallCount >= 0 &&
            telemetry.ObservedToolCallCount >= 0;
        var countsMatch =
            countersValid &&
            telemetry.ObservedChatCompletionCount == telemetry.ExpectedChatCompletionCount &&
            telemetry.ObservedToolCallCount == telemetry.ExpectedToolCallCount;

        var complete =
            countersValid &&
            missingFieldsValid &&
            missingFieldCount == 0 &&
            telemetry.HasAggregateTokenUsage &&
            telemetry.HasCallDurations &&
            telemetry.HasProgressEvents &&
            telemetry.ObservedChatCompletionCount >= telemetry.ExpectedChatCompletionCount &&
            telemetry.ObservedToolCallCount >= telemetry.ExpectedToolCallCount;

        var completeMetric = new BooleanMetric(
            TelemetryCompleteMetricName,
            value: complete,
            reason: complete
                ? "All required telemetry counters were captured."
                : "One or more required telemetry counters were missing or below the expected count.");

        var missingFieldMetric = new NumericMetric(
            MissingFieldCountMetricName,
            value: missingFieldCount,
            reason: missingFieldCount == 0
                ? "No required telemetry fields were reported missing."
                : $"{missingFieldCount} required telemetry field(s) were reported missing.");

        var countsMatchMetric = new BooleanMetric(
            CountsMatchMetricName,
            value: countsMatch,
            reason: countsMatch
                ? "Observed chat-completion and tool-call counts matched expectations."
                : "Observed chat-completion or tool-call counts did not match expectations.");

        var chatCompletionCountMetric = new NumericMetric(
            ChatCompletionCountMetricName,
            value: telemetry.ObservedChatCompletionCount,
            reason: $"{telemetry.ObservedChatCompletionCount} chat-completion call(s) were recorded.");

        return new ValueTask<EvaluationResult>(new EvaluationResult(
            completeMetric,
            missingFieldMetric,
            countsMatchMetric,
            chatCompletionCountMetric));
    }
}
