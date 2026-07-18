namespace NexusLabs.Foundry.MicrosoftAgentFramework;

/// <summary>
/// DI singleton that caches the resolved <see cref="AgentFrameworkBuilder"/>.
/// Used by both the <see cref="IAgentFactory"/> and <see cref="Progress.IProgressReporterFactory"/>
/// registrations so they observe identical configuration regardless of resolution order.
/// </summary>
/// <remarks>
/// The previous implementation captured sink types in a local closure mutated by the
/// <see cref="IAgentFactory"/> factory lambda, which created a resolve-order race with
/// <see cref="Progress.IProgressReporterFactory"/>. Routing both through this singleton
/// guarantees the configure delegate runs exactly once and both consumers see the same
/// built syringe.
/// </remarks>
internal sealed class BuiltAgentFrameworkBuilder
{
    internal BuiltAgentFrameworkBuilder(AgentFrameworkBuilder value)
    {
        Value = value;
    }

    public AgentFrameworkBuilder Value { get; }
}
