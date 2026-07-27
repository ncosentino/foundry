namespace NexusLabs.Foundry.Evaluation.Tests.Harness;

internal readonly record struct ContinuousOverride(
    double? Value,
    double PessimisticValue,
    bool IsComparable);
