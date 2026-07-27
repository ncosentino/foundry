namespace HarnessEvaluationApp;

internal readonly record struct HostedBatchKey(
    string CaseId,
    int TrialIndex);
