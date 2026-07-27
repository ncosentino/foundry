using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;

namespace NexusLabs.Foundry.Evaluation.Harness;

/// <summary>
/// Carries the compositional <see cref="HarnessRunEvaluationEvidence"/> for a single Harness agent run
/// through the <c>Microsoft.Extensions.AI.Evaluation</c> evaluator pipeline. The Harness deterministic
/// evaluators locate the single instance of this context in the <c>additionalContext</c> collection
/// passed to <see cref="IEvaluator.EvaluateAsync"/> and read only the slice they score.
/// </summary>
/// <remarks>
/// This context is deliberately separate from <see cref="AgentRunDiagnosticsContext"/>: it does not
/// carry, wrap, or modify <see cref="MicrosoftAgentFramework.Diagnostics.IAgentRunDiagnostics"/>.
/// Evaluators that need the raw diagnostics snapshot (such as the tool-trajectory evaluator) continue
/// to read <see cref="AgentRunDiagnosticsContext"/>, and can additionally read the Harness evidence
/// slices from this context.
/// </remarks>
public sealed class HarnessRunEvaluationContext : EvaluationContext
{
    /// <summary>The stable name used for this context.</summary>
    public const string ContextName = "Foundry Harness Run Evaluation Evidence";

    /// <summary>
    /// Initializes a new instance of the <see cref="HarnessRunEvaluationContext"/> class.
    /// </summary>
    /// <param name="evidence">The compositional per-item evidence bundle.</param>
    /// <exception cref="ArgumentNullException"><paramref name="evidence"/> is <see langword="null"/>.</exception>
    public HarnessRunEvaluationContext(HarnessRunEvaluationEvidence evidence)
        : base(ContextName, BuildContents(evidence))
    {
        Evidence = evidence;
    }

    /// <summary>Gets the compositional per-item evidence bundle.</summary>
    public HarnessRunEvaluationEvidence Evidence { get; }

    private static AIContent[] BuildContents(HarnessRunEvaluationEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);

        var summary =
            $"DiagnosticsSchema={(evidence.DiagnosticsSchema is null ? "n/a" : "present")} " +
            $"ContextCompactions={evidence.ContextCompactions?.Count.ToString() ?? "n/a"} " +
            $"ArtifactDecisions={evidence.ArtifactDecisions?.Count.ToString() ?? "n/a"} " +
            $"Telemetry={(evidence.Telemetry is null ? "n/a" : "present")} " +
            $"LifecycleEvents={evidence.LifecycleEvents?.Count.ToString() ?? "n/a"} " +
            $"IdentityAttribution={(evidence.IdentityAttribution is null ? "n/a" : "present")} " +
            $"Cancellation={(evidence.Cancellation is null ? "n/a" : "present")} " +
            $"SessionContinuity={(evidence.SessionContinuity is null ? "n/a" : "present")} " +
            $"CostAttribution={(evidence.CostAttribution is null ? "n/a" : "present")} " +
            $"ToolTrajectory={(evidence.ToolTrajectory is null ? "n/a" : "present")}";

        return [new TextContent(summary)];
    }
}
