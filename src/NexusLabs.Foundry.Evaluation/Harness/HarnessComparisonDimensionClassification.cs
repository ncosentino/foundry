namespace NexusLabs.Foundry.Evaluation.Harness;

internal static class HarnessComparisonDimensionClassification
{
    public static bool IsBinary(HarnessEvaluationDimension dimension) =>
        dimension is
            HarnessEvaluationDimension.Completion or
            HarnessEvaluationDimension.Continuity or
            HarnessEvaluationDimension.ContextSafety or
            HarnessEvaluationDimension.ArtifactReuse or
            HarnessEvaluationDimension.ToolTrajectory or
            HarnessEvaluationDimension.Cancellation or
            HarnessEvaluationDimension.Termination;

    public static bool IsContinuous(HarnessEvaluationDimension dimension) =>
        dimension is
            HarnessEvaluationDimension.CumulativeTokens or
            HarnessEvaluationDimension.PeakTokens or
            HarnessEvaluationDimension.CostAttribution or
            HarnessEvaluationDimension.Latency;
}
