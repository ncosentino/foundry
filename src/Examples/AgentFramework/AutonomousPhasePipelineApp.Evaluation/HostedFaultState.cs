namespace AutonomousPhasePipelineApp.Evaluation;

internal sealed class HostedFaultState
{
    private int _ledgerCount;
    private int _terminalCount;

    internal int IncrementLedger() =>
        Interlocked.Increment(ref _ledgerCount);

    internal int IncrementTerminal() =>
        Interlocked.Increment(ref _terminalCount);

    internal int LedgerCount =>
        Volatile.Read(ref _ledgerCount);
}
