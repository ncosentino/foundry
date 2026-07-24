using System.Text.Json;

using NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Capabilities;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness;

/// <summary>
/// Foundry-owned envelope wrapped around the inner MAF session JSON produced by
/// <see cref="Microsoft.Agents.AI.AIAgent.SerializeSessionAsync"/>. Binds the serialized
/// state to the trusted identity, session, and persistence configuration that produced it
/// so <see cref="HarnessGuardedAgent"/> can fail closed on restore if any of them changed.
/// </summary>
/// <remarks>
/// Deliberately does not carry a workspace reference: the active, host-authorized workspace
/// is never selected from serialized state. It always comes from the current
/// <see cref="NexusLabs.Foundry.MicrosoftAgentFramework.Context.IAgentExecutionContext"/> and is revalidated by
/// <see cref="HarnessExecutionBinding.ValidateCurrent"/> before and after every session
/// operation.
/// </remarks>
internal sealed record HarnessSessionEnvelope(
    int SchemaVersion,
    string UserId,
    string OrchestrationId,
    string SessionId,
    HarnessHistoryPersistenceMode PersistenceMode,
    IReadOnlyList<string> ProviderStateKeys,
    IReadOnlyList<HarnessCapability> EnabledCapabilities,
    JsonElement InnerSession)
{
    /// <summary>The current envelope schema version. Bump when the envelope shape changes.</summary>
    internal const int CurrentSchemaVersion = 2;
}
