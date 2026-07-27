using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;

namespace NexusLabs.Foundry.Evaluation.Harness;

/// <summary>
/// Deterministic, per-item evaluator that scores diagnostics-schema completeness for one Harness run
/// and emits a stable schema profile fingerprint. Cross-arm parity is a downstream comparison over the
/// emitted profiles; this evaluator never compares arms.
/// </summary>
/// <remarks>
/// Reads the <see cref="HarnessDiagnosticsSchemaProfile"/> slice from the
/// <see cref="HarnessRunEvaluationContext"/>. When the context or slice is absent the evaluator returns
/// an empty <see cref="EvaluationResult"/> ("not applicable"). A present-but-incomplete profile yields a
/// <see langword="false"/> completeness metric rather than an empty result.
/// </remarks>
public sealed class HarnessDiagnosticsSchemaProfileEvaluator : IEvaluator
{
    /// <summary>Metric name for the boolean schema-completeness rollup.</summary>
    public const string SchemaCompleteMetricName = "Harness Diagnostics Schema Complete";

    /// <summary>Metric name for the count of captured schema fields.</summary>
    public const string FieldCountMetricName = "Harness Diagnostics Field Count";

    /// <summary>Metric name for the deterministic schema profile fingerprint.</summary>
    public const string SchemaProfileMetricName = "Harness Diagnostics Schema Profile";

    /// <summary>Metric name for the captured execution mode.</summary>
    public const string ExecutionModeMetricName = "Harness Execution Mode";

    private const int TrackedFieldCount = 8;

    /// <inheritdoc />
    public IReadOnlyCollection<string> EvaluationMetricNames { get; } =
    [
        SchemaCompleteMetricName,
        FieldCountMetricName,
        SchemaProfileMetricName,
        ExecutionModeMetricName,
    ];

    /// <inheritdoc />
    public ValueTask<EvaluationResult> EvaluateAsync(
        IEnumerable<ChatMessage> messages,
        ChatResponse modelResponse,
        ChatConfiguration? chatConfiguration = null,
        IEnumerable<EvaluationContext>? additionalContext = null,
        CancellationToken cancellationToken = default)
    {
        var profile = additionalContext?
            .OfType<HarnessRunEvaluationContext>()
            .FirstOrDefault()?
            .Evidence.DiagnosticsSchema;

        if (profile is null)
        {
            return new ValueTask<EvaluationResult>(new EvaluationResult());
        }

        var hasExecutionMode = !string.IsNullOrWhiteSpace(profile.ExecutionMode);
        var fieldCount =
            (profile.HasAggregateTokenUsage ? 1 : 0) +
            (profile.HasChatCompletionDiagnostics ? 1 : 0) +
            (profile.HasToolCallDiagnostics ? 1 : 0) +
            (profile.HasTimingBoundaries ? 1 : 0) +
            (profile.HasInputMessages ? 1 : 0) +
            (profile.HasOutputResponse ? 1 : 0) +
            (profile.SupportsContextDiagnostics ? 1 : 0) +
            (profile.SupportsArtifactDiagnostics ? 1 : 0);

        // Completeness requires the core counters every arm must emit to be comparable at all.
        var complete =
            hasExecutionMode &&
            profile.HasAggregateTokenUsage &&
            profile.HasTimingBoundaries &&
            profile.HasInputMessages;

        var executionMode = hasExecutionMode ? profile.ExecutionMode : "(unknown)";

        var fingerprint =
            $"mode={executionMode};" +
            $"tok={Bit(profile.HasAggregateTokenUsage)};" +
            $"cc={Bit(profile.HasChatCompletionDiagnostics)};" +
            $"tc={Bit(profile.HasToolCallDiagnostics)};" +
            $"time={Bit(profile.HasTimingBoundaries)};" +
            $"in={Bit(profile.HasInputMessages)};" +
            $"out={Bit(profile.HasOutputResponse)};" +
            $"ctx={Bit(profile.SupportsContextDiagnostics)};" +
            $"art={Bit(profile.SupportsArtifactDiagnostics)}";

        var completeMetric = new BooleanMetric(
            SchemaCompleteMetricName,
            value: complete,
            reason: complete
                ? "All core diagnostics-schema fields required for parity were captured."
                : "One or more core diagnostics-schema fields required for parity were missing.");

        var fieldCountMetric = new NumericMetric(
            FieldCountMetricName,
            value: fieldCount,
            reason: $"{fieldCount} of {TrackedFieldCount} tracked schema fields were captured.");

        var profileMetric = new StringMetric(
            SchemaProfileMetricName,
            value: fingerprint,
            reason: "Deterministic diagnostics-schema fingerprint for downstream parity comparison.");

        var modeMetric = new StringMetric(
            ExecutionModeMetricName,
            value: executionMode,
            reason: $"The captured execution mode was '{executionMode}'.");

        return new ValueTask<EvaluationResult>(new EvaluationResult(
            completeMetric,
            fieldCountMetric,
            profileMetric,
            modeMetric));
    }

    private static int Bit(bool value) => value ? 1 : 0;
}
