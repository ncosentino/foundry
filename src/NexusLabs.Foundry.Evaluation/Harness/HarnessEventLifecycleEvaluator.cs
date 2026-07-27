using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;

namespace NexusLabs.Foundry.Evaluation.Harness;

/// <summary>
/// Deterministic, per-item evaluator that scores progress-event ordering and lifecycle pairing for one
/// Harness run: the normalized records are globally ordered by strictly increasing sequence number, and
/// every started span is followed by exactly one completed or terminated record sharing its correlation
/// identity, and a composed-context record occurs only after successful compaction completion.
/// </summary>
/// <remarks>
/// Reads the lifecycle-events slice from the <see cref="HarnessRunEvaluationContext"/>. A
/// <see langword="null"/> slice returns an empty result; a present slice is scored, with any ordering or
/// pairing violation driving the boolean metrics to <see langword="false"/>.
/// </remarks>
public sealed class HarnessEventLifecycleEvaluator : IEvaluator
{
    /// <summary>Metric name for the strictly-increasing ordering rollup.</summary>
    public const string OrderedMetricName = "Harness Events Ordered";

    /// <summary>Metric name for the lifecycle-pairing rollup.</summary>
    public const string PairedMetricName = "Harness Lifecycle Paired";

    /// <summary>Metric name for the count of unpaired or invalidly ordered lifecycle records.</summary>
    public const string UnpairedCountMetricName = "Harness Unpaired Lifecycle Count";

    /// <summary>Metric name for the number of normalized records observed.</summary>
    public const string EventCountMetricName = "Harness Event Count";

    /// <inheritdoc />
    public IReadOnlyCollection<string> EvaluationMetricNames { get; } =
    [
        OrderedMetricName,
        PairedMetricName,
        UnpairedCountMetricName,
        EventCountMetricName,
    ];

    /// <inheritdoc />
    public ValueTask<EvaluationResult> EvaluateAsync(
        IEnumerable<ChatMessage> messages,
        ChatResponse modelResponse,
        ChatConfiguration? chatConfiguration = null,
        IEnumerable<EvaluationContext>? additionalContext = null,
        CancellationToken cancellationToken = default)
    {
        var events = additionalContext?
            .OfType<HarnessRunEvaluationContext>()
            .FirstOrDefault()?
            .Evidence.LifecycleEvents;

        if (events is null)
        {
            return new ValueTask<EvaluationResult>(new EvaluationResult());
        }

        var ordered = IsStrictlyOrdered(events);
        var unpairedCount = CountLifecycleViolations(events);
        var paired = unpairedCount == 0;

        var orderedMetric = new BooleanMetric(
            OrderedMetricName,
            value: ordered,
            reason: ordered
                ? "Progress records were globally ordered by strictly increasing sequence number."
                : "Progress records were not strictly ordered by sequence number.");

        var pairedMetric = new BooleanMetric(
            PairedMetricName,
            value: paired,
            reason: paired
                ? "Every lifecycle span and composed-context record followed the required state order."
                : $"{unpairedCount} lifecycle record(s) were unpaired or invalidly ordered.");

        var unpairedMetric = new NumericMetric(
            UnpairedCountMetricName,
            value: unpairedCount,
            reason: $"{unpairedCount} lifecycle record(s) were unpaired or invalidly ordered.");

        var eventCountMetric = new NumericMetric(
            EventCountMetricName,
            value: events.Count,
            reason: $"{events.Count} normalized progress record(s) were observed.");

        return new ValueTask<EvaluationResult>(new EvaluationResult(
            orderedMetric,
            pairedMetric,
            unpairedMetric,
            eventCountMetric));
    }

    private static bool IsStrictlyOrdered(IReadOnlyList<HarnessLifecycleEventEvidence> events)
    {
        if (events.Count > 0 && events[0] is null)
        {
            return false;
        }

        for (var i = 1; i < events.Count; i++)
        {
            if (events[i] is null ||
                events[i - 1] is null ||
                events[i].SequenceNumber <= events[i - 1].SequenceNumber)
            {
                return false;
            }
        }

        return true;
    }

    private static int CountLifecycleViolations(IReadOnlyList<HarnessLifecycleEventEvidence> events)
    {
        var stateByKey = new Dictionary<string, HarnessLifecyclePhase>(StringComparer.Ordinal);
        var violationCount = 0;

        foreach (var record in events)
        {
            if (record is null ||
                !Enum.IsDefined(record.Kind) ||
                !Enum.IsDefined(record.Phase) ||
                string.IsNullOrWhiteSpace(record.CorrelationId))
            {
                violationCount++;
                continue;
            }

            if (record.Phase == HarnessLifecyclePhase.Instant)
            {
                if (record.Kind == HarnessLifecycleEventKind.ContextComposed)
                {
                    var compactionKey = Key(
                        HarnessLifecycleEventKind.ContextCompaction,
                        record.CorrelationId);
                    if (!stateByKey.TryGetValue(compactionKey, out var compactionState) ||
                        compactionState != HarnessLifecyclePhase.Completed)
                    {
                        violationCount++;
                    }
                }
                else if (record.Kind != HarnessLifecycleEventKind.ArtifactDecision)
                {
                    violationCount++;
                }

                continue;
            }

            if (record.Kind is HarnessLifecycleEventKind.ContextComposed
                or HarnessLifecycleEventKind.ArtifactDecision)
            {
                violationCount++;
                continue;
            }

            var key = Key(record.Kind, record.CorrelationId);
            if (record.Phase == HarnessLifecyclePhase.Started)
            {
                if (stateByKey.ContainsKey(key))
                {
                    violationCount++;
                }
                else
                {
                    stateByKey[key] = HarnessLifecyclePhase.Started;
                }

                continue;
            }

            if (!stateByKey.TryGetValue(key, out var state) ||
                state != HarnessLifecyclePhase.Started)
            {
                violationCount++;
                continue;
            }

            stateByKey[key] = record.Phase;
        }

        violationCount += stateByKey.Values.Count(state => state == HarnessLifecyclePhase.Started);
        return violationCount;
    }

    private static string Key(HarnessLifecycleEventKind kind, string correlationId) =>
        $"{kind}::{correlationId}";
}
