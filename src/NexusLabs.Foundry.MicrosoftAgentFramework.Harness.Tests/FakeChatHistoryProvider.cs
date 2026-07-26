using Microsoft.Agents.AI;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Tests;

/// <summary>
/// A minimal <see cref="ChatHistoryProvider"/> fake used only to prove that supplying a
/// non-<see langword="null"/> <see cref="Bundle.FoundryHarnessAgentConfiguration.ChatHistoryProvider"/>
/// flips the history-persistence disposition's backing selection to caller-supplied. It relies
/// entirely on the base class's default (no-op) behavior; no members are overridden.
/// </summary>
internal sealed class FakeChatHistoryProvider : ChatHistoryProvider
{
}
