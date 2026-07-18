using NexusLabs.Foundry.MicrosoftAgentFramework.Diagnostics;
using NexusLabs.Needlr;

namespace RfcPipelineApp;

/// <summary>
/// Implements an application-defined stage termination result for RFC review.
/// </summary>
[DoNotAutoRegister]
internal sealed record RfcReviewerConsensus(
    int ApprovingReviewers,
    int RequestedChanges) : IStageTermination
{
    public string ToTagValue() => "RfcReviewerConsensus";
}
