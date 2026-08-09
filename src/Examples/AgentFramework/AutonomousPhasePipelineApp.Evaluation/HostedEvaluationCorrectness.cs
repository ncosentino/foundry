namespace AutonomousPhasePipelineApp.Evaluation;

internal sealed record HostedEvaluationCorrectness(
    bool ArtifactContractValid,
    bool EvidenceExactSet,
    bool GapsExactSet,
    bool BranchesExact,
    bool RequiredCoverageMatches,
    bool SuccessfulSiblingsPreserved,
    bool PipelineOutcomeMatches,
    bool SynthesisOutcomeMatches,
    bool AuthoritativeDeliveryExact,
    bool TaskContractPass,
    string? ValidationErrorCode);
