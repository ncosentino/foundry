using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;

namespace NexusLabs.Foundry.Evaluation.Harness;

/// <summary>
/// Deterministic, per-item evaluator that scores progress-event ordering and lifecycle pairing for one
/// Harness run: the normalized records are globally ordered by strictly increasing sequence number, and
/// every started span is paired with exactly one completed or terminated record sharing its correlation
/// identity.
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

    /// <summary>Metric name for the count of unpaired started/completed/terminated records.</summary>
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
        var unpairedCount = CountUnpaired(events);
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
                ? "Every started span was paired with exactly one completed or terminated record."
                : $"{unpairedCount} lifecycle record(s) were unpaired.");

        var unpairedMetric = new NumericMetric(
            UnpairedCountMetricName,
            value: unpairedCount,
            reason: $"{unpairedCount} started/completed/terminated record(s) were unpaired.");

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
        for (var i = 1; i < events.Count; i++)
        {
            if (events[i].SequenceNumber <= events[i - 1].SequenceNumber)
            {
                return false;
            }
        }

        return true;
    }

    private static int CountUnpaired(IReadOnlyList<HarnessLifecycleEventEvidence> events)
    {
        // Correlation-scoped tallies of started versus completed/terminated records.
        var startedByKey = new Dictionary<string, int>(StringComparer.Ordinal);
        var terminalByKey = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var record in events)
        {
            switch (record.Phase)
            {
                case HarnessLifecyclePhase.Started:
                    Increment(startedByKey, Key(record));
                    break;
                case HarnessLifecyclePhase.Completed:
                case HarnessLifecyclePhase.Terminated:
                    Increment(terminalByKey, Key(record));
                    break;
                case HarnessLifecyclePhase.Instant:
                default:
                    break;
            }
        }

        var unpaired = 0;
        var keys = new HashSet<string>(StringComparer.Ordinal);
        keys.UnionWith(startedByKey.Keys);
        keys.UnionWith(terminalByKey.Keys);

        foreach (var key in keys)
        {
            startedByKey.TryGetValue(key, out var started);
            terminalByKey.TryGetValue(key, out var terminal);
            unpaired += Math.Abs(started - terminal);
        }

        return unpaired;
    }

    private static string Key(HarnessLifecycleEventEvidence record) =>
        $"{record.Kind}::{record.CorrelationId}";

    private static void Increment(Dictionary<string, int> tally, string key)
    {
        tally.TryGetValue(key, out var count);
        tally[key] = count + 1;
    }
}
