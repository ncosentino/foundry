using NexusLabs.Foundry.Evaluation.Harness;

namespace NexusLabs.Foundry.Evaluation.Tests.Harness;

internal readonly record struct ContinuousKey(
    HarnessComparisonArm Arm,
    string CaseId,
    int TrialIndex,
    HarnessEvaluationDimension Dimension);
