namespace NexusLabs.Foundry.MicrosoftAgentFramework.Diagnostics;

/// <summary>
/// Default <see cref="IInFlightAgentDiagnosticsAccessor"/> that delegates to the
/// AsyncLocal-scoped <see cref="AgentRunDiagnosticsBuilder"/>. Stateless; safe
/// to register as a singleton.
/// </summary>
internal sealed class InFlightAgentDiagnosticsAccessor : IInFlightAgentDiagnosticsAccessor
{
    /// <inheritdoc />
    public IAgentRunDiagnostics? Current => AgentRunDiagnosticsBuilder.GetCurrent()?.Build();
}
