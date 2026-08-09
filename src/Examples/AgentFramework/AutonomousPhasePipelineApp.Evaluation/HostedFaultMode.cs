namespace AutonomousPhasePipelineApp.Evaluation;

internal enum HostedFaultMode
{
    None,
    InvalidFirstTerminal,
    InvalidEveryTerminal,
    DelayFirstTerminalUntilCanceled,
    ForceFirstMagenticStall,
    InvalidMagenticFinal,
}
