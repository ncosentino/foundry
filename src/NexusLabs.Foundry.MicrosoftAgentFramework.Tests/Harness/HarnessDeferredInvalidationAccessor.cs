using NexusLabs.Foundry.MicrosoftAgentFramework.Context;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Tests.Harness;

/// <summary>
/// An execution-context accessor that keeps reporting a trusted context for a fixed number of
/// reads after <see cref="Arm"/> and then reports none, so a test can invalidate the trusted
/// binding at a chosen point inside a composed pipeline rather than only at a run boundary.
/// </summary>
internal sealed class HarnessDeferredInvalidationAccessor(int validReadsAfterArm) :
    IAgentExecutionContextAccessor
{
    private IAgentExecutionContext? _context;
    private bool _armed;
    private bool _invalidated;

    /// <summary>
    /// Gets the number of reads that were still served a trusted context after <see cref="Arm"/>.
    /// </summary>
    internal int ReadsServedAfterArm { get; private set; }

    public IAgentExecutionContext? Current
    {
        get
        {
            if (_armed && !_invalidated)
            {
                if (ReadsServedAfterArm >= validReadsAfterArm)
                {
                    _invalidated = true;
                    return null;
                }

                ReadsServedAfterArm++;
            }

            return _invalidated ? null : _context;
        }
    }

    public IDisposable BeginScope(IAgentExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var previous = _context;
        _context = context;
        return new Scope(() => _context = previous);
    }

    /// <summary>
    /// Begins counting reads toward the configured invalidation point.
    /// </summary>
    internal void Arm() => _armed = true;

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
