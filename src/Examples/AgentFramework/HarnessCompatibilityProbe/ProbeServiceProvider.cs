namespace HarnessCompatibilityProbe;

internal sealed class ProbeServiceProvider : IServiceProvider
{
    internal static ProbeServiceProvider Instance { get; } = new();

    private ProbeServiceProvider()
    {
    }

    object? IServiceProvider.GetService(Type serviceType) => null;
}
