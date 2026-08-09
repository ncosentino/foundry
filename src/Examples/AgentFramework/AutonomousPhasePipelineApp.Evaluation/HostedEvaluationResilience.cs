namespace AutonomousPhasePipelineApp.Evaluation;

internal sealed record HostedEvaluationResilience(
    bool FaultRequired,
    bool FaultActivated,
    bool FaultActivatedAfterProviderWork,
    bool FaultActivatedAfterCollaboratorWork,
    bool CancellationObserved,
    bool NoDeliveryAfterCancellation,
    bool RestoreAttempted,
    bool SameRunRestoreSucceeded,
    bool AcceptedPriorPhaseReran,
    bool ReplayIdempotent,
    bool CorrectionRecoveredWithinBound,
    string? FailureCode);
