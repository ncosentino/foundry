using NexusLabs.Foundry.Evaluation.Harness;

namespace NexusLabs.Foundry.Evaluation.Tests.Harness;

internal readonly record struct BinaryKey(
    HarnessComparisonArm Arm,
    string CaseId,
    int TrialIndex,
    HarnessEvaluationDimension Dimension);
