namespace AutonomousPhasePipelineApp.Core;

internal sealed record MagenticLedgerSnapshot(
    bool IsRequestSatisfied,
    bool IsInLoop,
    bool IsProgressBeingMade,
    string NextSpeaker,
    string InstructionOrQuestion);
