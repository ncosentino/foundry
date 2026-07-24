using NexusLabs.Foundry.MicrosoftAgentFramework.Context;
using NexusLabs.Needlr;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Tests.Harness;

[DoNotAutoRegister]
internal sealed class MutableExecutionContextAccessor : IAgentExecutionContextAccessor
{
    public IAgentExecutionContext? Current { get; private set; }

    public IDisposable BeginScope(IAgentExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var previous = Current;
        Current = context;
        return new Scope(() => Current = previous);
    }

    internal void Clear() => Current = null;

    private sealed class Scope(Action restore) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            restore();
        }
    }
}
