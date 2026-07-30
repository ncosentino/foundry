using NexusLabs.Foundry.Evaluation.Harness;

namespace HarnessEvaluationApp;

internal sealed record HostedTrialOutput(
    HarnessComparisonArm Arm,
    string CaseId,
    int TrialIndex,
    HarnessRunTerminalCategory TerminalCategory,
    bool Completion,
    string? ResponseText,
    IReadOnlyList<string> ToolCalls,
    long CumulativeTokens,
    long PeakTokens,
    double LatencyMilliseconds,
    string CaptureReference,
    string? OutputContent);
