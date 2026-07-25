namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Context;

/// <summary>
/// Thrown by <see cref="HarnessUpstreamChatReducerAdapter"/> when the bridged upstream
/// <see cref="Microsoft.Extensions.AI.IChatReducer"/> violates the bridge's structural contract — for
/// example by returning a message that carries function-call or function-result content not present
/// verbatim among the original tool-exchange entries. The bridge never silently reinterprets such a
/// violation as a dropped or summarized entry; it rejects the whole proposal by throwing, so
/// <see cref="HarnessContextAssembler"/> never forwards a fabricated tool exchange.
/// </summary>
internal sealed class HarnessCompactionReducerContractException : Exception
{
    internal HarnessCompactionReducerContractException(string message)
        : base(message)
    {
    }

    internal HarnessCompactionReducerContractException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
