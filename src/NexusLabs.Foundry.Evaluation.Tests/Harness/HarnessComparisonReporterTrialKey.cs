using NexusLabs.Foundry.Evaluation.Harness;

namespace NexusLabs.Foundry.Evaluation.Tests.Harness;

internal readonly record struct TrialKey(
    HarnessComparisonArm Arm,
    string CaseId,
    int TrialIndex);
