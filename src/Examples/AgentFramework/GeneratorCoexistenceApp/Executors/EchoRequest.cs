using NexusLabs.Needlr;

namespace GeneratorCoexistenceApp.Executors;

/// <summary>
/// Represents a message routed by MAF's <c>[MessageHandler]</c> generator.
/// </summary>
/// <param name="Text">The text to echo.</param>
[DoNotAutoRegister]
internal sealed record EchoRequest(string Text);
