using NexusLabs.Foundry.MicrosoftAgentFramework.Context;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness;

/// <summary>
/// The trusted-execution identity a compaction node revalidates before and after it assembles context.
/// </summary>
/// <remarks>
/// The selected-provider composition always supplies one, because that path owns an authoritative
/// <c>IWorkspace</c> whose authority the binding exists to protect. The complete-bundle path has no
/// workspace and no execution scope to bind to, so it supplies none and the node performs no trust
/// revalidation — there is no workspace authority to defend there. Grouping the three values into one
/// type makes that an all-or-nothing choice: a partially-supplied identity is rejected rather than
/// silently degrading the selected-provider guarantee to an unchecked one.
/// </remarks>
internal sealed class HarnessCompactionTrustBinding
{
    private readonly HarnessExecutionBinding _binding;
    private readonly IAgentExecutionContextAccessor _accessor;
    private readonly string _sessionId;

    private HarnessCompactionTrustBinding(
        HarnessExecutionBinding binding,
        IAgentExecutionContextAccessor accessor,
        string sessionId)
    {
        _binding = binding;
        _accessor = accessor;
        _sessionId = sessionId;
    }

    /// <summary>
    /// Builds a trust binding when all three values are present, or <see langword="null"/> when all
    /// three are absent.
    /// </summary>
    /// <exception cref="ArgumentException">Only some of the three values were supplied.</exception>
    internal static HarnessCompactionTrustBinding? CreateOrNone(
        HarnessExecutionBinding? binding,
        IAgentExecutionContextAccessor? accessor,
        string? sessionId)
    {
        var supplied = binding is not null
            && accessor is not null
            && !string.IsNullOrWhiteSpace(sessionId);
        var absent = binding is null
            && accessor is null
            && string.IsNullOrWhiteSpace(sessionId);

        if (supplied)
        {
            return new HarnessCompactionTrustBinding(binding!, accessor!, sessionId!);
        }

        if (absent)
        {
            return null;
        }

        throw new ArgumentException(
            "A compaction trust binding requires an execution binding, an execution context " +
            "accessor, and a session id together, or none of them. A partially-supplied identity " +
            "cannot be revalidated.",
            nameof(binding));
    }

    /// <exception cref="InvalidOperationException">The active execution identity no longer matches.</exception>
    internal void EnsureCurrent() => _binding.EnsureCurrent(_accessor, _sessionId);
}
