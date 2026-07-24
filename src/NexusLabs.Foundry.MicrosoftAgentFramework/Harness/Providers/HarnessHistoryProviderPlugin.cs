using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

using NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Capabilities;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Providers;

internal sealed class HarnessHistoryProviderPlugin
{
    private HarnessHistoryProviderPlugin(
        HarnessHistoryPersistenceMode persistenceMode,
        ChatHistoryProvider chatHistoryProvider)
    {
        PersistenceMode = persistenceMode;
        ChatHistoryProvider = chatHistoryProvider;
        var stateKeys = chatHistoryProvider.StateKeys ??
            throw new InvalidOperationException(
                "The selected ChatHistoryProvider returned null state keys.");
        if (stateKeys.Count == 0 ||
            stateKeys.Any(string.IsNullOrWhiteSpace) ||
            stateKeys.Distinct(StringComparer.Ordinal).Count() != stateKeys.Count)
        {
            throw new InvalidOperationException(
                "The selected ChatHistoryProvider must expose unique, non-empty state keys.");
        }

        ProviderStateKeys =
        [
            .. stateKeys.OrderBy(key => key, StringComparer.Ordinal),
        ];
    }

    internal HarnessHistoryPersistenceMode PersistenceMode { get; }

    internal ChatHistoryProvider ChatHistoryProvider { get; }

    internal IReadOnlyList<string> ProviderStateKeys { get; }

    internal static HarnessHistoryProviderPlugin Create(
        HarnessHistoryPersistenceMode persistenceMode,
        ChatHistoryProvider? callerSuppliedProvider)
    {
        if (persistenceMode is HarnessHistoryPersistenceMode.NotApplicable or
            HarnessHistoryPersistenceMode.ServiceManaged)
        {
            throw new InvalidOperationException(
                $"Persistence mode '{persistenceMode}' cannot create a history provider plugin.");
        }

        if (persistenceMode == HarnessHistoryPersistenceMode.DurableProvider &&
            callerSuppliedProvider is null)
        {
            throw new InvalidOperationException(
                "Durable-provider persistence requires a caller-supplied ChatHistoryProvider.");
        }

        if (persistenceMode != HarnessHistoryPersistenceMode.DurableProvider &&
            callerSuppliedProvider is not null)
        {
            throw new InvalidOperationException(
                $"Persistence mode '{persistenceMode}' does not accept a caller-supplied " +
                "ChatHistoryProvider.");
        }

        var selectedProvider = persistenceMode switch
        {
            HarnessHistoryPersistenceMode.DurableProvider => callerSuppliedProvider!,
            HarnessHistoryPersistenceMode.InMemory or
                HarnessHistoryPersistenceMode.Serialized =>
                new InMemoryChatHistoryProvider(new InMemoryChatHistoryProviderOptions()),
            _ => throw new InvalidOperationException(
                $"Persistence mode '{persistenceMode}' is not supported."),
        };

        return new HarnessHistoryProviderPlugin(
            persistenceMode,
            selectedProvider);
    }

    internal (ChatHistoryProvider ChatHistoryProvider,
        bool RequirePerServiceCallChatHistoryPersistence) GetAgentOptionsConfiguration() =>
        (ChatHistoryProvider, true);

    internal ChatClientBuilder UsePerServiceCallChatHistoryPersistence(
        ChatClientBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.UsePerServiceCallChatHistoryPersistence();
    }
}
