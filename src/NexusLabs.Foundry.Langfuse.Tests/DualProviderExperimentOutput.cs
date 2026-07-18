using Microsoft.Extensions.AI;

namespace NexusLabs.Foundry.Langfuse.Tests;

/// <summary>
/// Carries subject messages, response, and ExampleProduct-shaped artifacts through conformance tests.
/// </summary>
internal sealed record DualProviderExperimentOutput(
    IReadOnlyList<ChatMessage> Messages,
    ChatResponse Response,
    IReadOnlyDictionary<string, string> Artifacts);
