using Microsoft.Agents.AI;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Tests;

/// <summary>
/// A minimal <see cref="AIContextProvider"/> fake used only to prove that supplying a
/// non-empty <see cref="Bundle.FoundryHarnessAgentConfiguration.AdditionalContextProviders"/> list
/// flips the additional-context-providers disposition to requested/effective enabled with
/// caller-supplied backing. It relies entirely on the base class's default (no-op) behavior; no
/// members are overridden.
/// </summary>
internal sealed class FakeAIContextProvider : AIContextProvider
{
}
