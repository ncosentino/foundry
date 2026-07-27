using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;

namespace NexusLabs.Foundry.Evaluation.Harness;

/// <summary>
/// Deterministic, per-item evaluator that scores the required/forbidden tool trajectory for one Harness
/// run. It reuses the existing <see cref="ToolCallTrajectoryEvaluator"/> evidence (total/failed/sequence
/// metrics from the captured <see cref="AgentRunDiagnosticsContext"/>) and adds required-subsequence and
/// forbidden-tool compliance derived from the run's observed tool-call names.
/// </summary>
/// <remarks>
/// Reads the tool-trajectory expectation from the <see cref="HarnessRunEvaluationContext"/> and the
/// observed tool calls from the <see cref="AgentRunDiagnosticsContext"/>. When the expectation slice is
/// absent the evaluator returns an empty result ("not applicable"). When the expectation is present but
/// no diagnostics were captured, the observed sequence is treated as empty and the required-tools metric
/// fails rather than disappearing.
/// </remarks>
public sealed class HarnessToolTrajectoryEvaluator : IEvaluator
{
    /// <summary>Metric name for whether every required tool appeared in order.</summary>
    public const string RequiredToolsPresentMetricName = "Harness Required Tools Present";

    /// <summary>Metric name for whether no forbidden tool appeared.</summary>
    public const string ForbiddenToolsAbsentMetricName = "Harness Forbidden Tools Absent";

    /// <summary>Metric name for the overall trajectory-compliance rollup.</summary>
    public const string TrajectoryCompliantMetricName = "Harness Tool Trajectory Compliant";

    /// <summary>Metric name for the count of forbidden tool invocations observed.</summary>
    public const string ForbiddenInvocationCountMetricName = "Harness Forbidden Tool Invocations";

    private readonly ToolCallTrajectoryEvaluator _baseEvaluator = new();

    /// <inheritdoc />
    public IReadOnlyCollection<string> EvaluationMetricNames { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="HarnessToolTrajectoryEvaluator"/> class.
    /// </summary>
    public HarnessToolTrajectoryEvaluator()
    {
        var names = new List<string>
        {
            RequiredToolsPresentMetricName,
            ForbiddenToolsAbsentMetricName,
            TrajectoryCompliantMetricName,
            ForbiddenInvocationCountMetricName,
        };
        names.AddRange(_baseEvaluator.EvaluationMetricNames);
        EvaluationMetricNames = names;
    }

    /// <inheritdoc />
    public async ValueTask<EvaluationResult> EvaluateAsync(
        IEnumerable<ChatMessage> messages,
        ChatResponse modelResponse,
        ChatConfiguration? chatConfiguration = null,
        IEnumerable<EvaluationContext>? additionalContext = null,
        CancellationToken cancellationToken = default)
    {
        var contextList = additionalContext as IReadOnlyCollection<EvaluationContext> ?? additionalContext?.ToArray();

        var expectation = contextList?
            .OfType<HarnessRunEvaluationContext>()
            .FirstOrDefault()?
            .Evidence.ToolTrajectory;

        if (expectation is null)
        {
            return new EvaluationResult();
        }

        var observedTools = contextList?
            .OfType<AgentRunDiagnosticsContext>()
            .FirstOrDefault()?
            .Diagnostics.ToolCalls
            .Select(call => call.ToolName)
            .ToArray() ?? [];

        var requiredPresent = ContainsInOrder(observedTools, expectation.RequiredToolSequence);
        var forbiddenSet = new HashSet<string>(expectation.ForbiddenTools, StringComparer.Ordinal);
        var forbiddenInvocations = observedTools.Count(forbiddenSet.Contains);
        var forbiddenAbsent = forbiddenInvocations == 0;
        var compliant = requiredPresent && forbiddenAbsent;

        var metrics = new List<EvaluationMetric>
        {
            new BooleanMetric(
                RequiredToolsPresentMetricName,
                value: requiredPresent,
                reason: requiredPresent
                    ? "Every required tool appeared as an in-order subsequence of the observed tool calls."
                    : "At least one required tool was missing or out of order in the observed tool calls."),
            new BooleanMetric(
                ForbiddenToolsAbsentMetricName,
                value: forbiddenAbsent,
                reason: forbiddenAbsent
                    ? "No forbidden tool was invoked."
                    : $"{forbiddenInvocations} forbidden tool invocation(s) were observed."),
            new BooleanMetric(
                TrajectoryCompliantMetricName,
                value: compliant,
                reason: compliant
                    ? "The tool trajectory satisfied both the required-order and forbidden-tool constraints."
                    : "The tool trajectory violated the required-order or forbidden-tool constraints."),
            new NumericMetric(
                ForbiddenInvocationCountMetricName,
                value: forbiddenInvocations,
                reason: $"{forbiddenInvocations} forbidden tool invocation(s) were observed."),
        };

        var baseResult = await _baseEvaluator.EvaluateAsync(
            messages,
            modelResponse,
            chatConfiguration,
            contextList,
            cancellationToken).ConfigureAwait(false);
        metrics.AddRange(baseResult.Metrics.Values);

        return new EvaluationResult(metrics);
    }

    private static bool ContainsInOrder(IReadOnlyList<string> observed, IReadOnlyList<string> required)
    {
        if (required.Count == 0)
        {
            return true;
        }

        var matched = 0;
        foreach (var tool in observed)
        {
            if (string.Equals(tool, required[matched], StringComparison.Ordinal))
            {
                matched++;
                if (matched == required.Count)
                {
                    return true;
                }
            }
        }

        return false;
    }
}
