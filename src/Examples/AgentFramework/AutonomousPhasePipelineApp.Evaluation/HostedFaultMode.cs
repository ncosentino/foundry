namespace AutonomousPhasePipelineApp.Evaluation;

internal enum HostedFaultMode
{
    None,
    InvalidFirstTerminal,
    InvalidEveryTerminal,
    FailFirstTerminal,
    DelayFirstCallUntilCanceled,
    ForceFirstMagenticStall,
    InvalidMagenticFinal,
}
