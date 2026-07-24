namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Capabilities;

/// <summary>
/// Models the session-persistence concepts referenced by the G3 session-continuity
/// slice: whether a caller wants per-service-call chat history persistence at all,
/// and if so, which storage strategy backs it.
/// </summary>
internal enum HarnessHistoryPersistenceMode
{
    /// <summary>No persistence mode was requested; per-service history is not in use.</summary>
    NotApplicable,

    /// <summary>
    /// Chat history lives only in the in-process <see cref="Microsoft.Agents.AI.AgentSession"/>.
    /// Non-durable: the session must stay resident for history to survive.
    /// </summary>
    InMemory,

    /// <summary>
    /// Chat history is held in-process the same way as <see cref="InMemory"/>, but the
    /// caller takes responsibility for calling <c>SerializeSessionAsync</c> and persisting
    /// the resulting JSON somewhere durable, then restoring it with <c>DeserializeSessionAsync</c>.
    /// </summary>
    Serialized,

    /// <summary>
    /// The caller supplies its own <see cref="Microsoft.Agents.AI.ChatHistoryProvider"/> backed by
    /// durable storage it owns. Foundry does not implement this provider; it only composes it.
    /// </summary>
    DurableProvider,

    /// <summary>
    /// The underlying AI service manages conversation history through a service-issued
    /// conversation identifier. MAF 1.15 supports this lifecycle, but the selected-provider
    /// slice requires separate provider-specific capability evidence before enabling it.
    /// </summary>
    ServiceManaged,
}
