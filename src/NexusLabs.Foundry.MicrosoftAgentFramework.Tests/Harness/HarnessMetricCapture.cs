using System.Collections.Concurrent;
using System.Diagnostics.Metrics;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Tests.Harness;

internal sealed class HarnessMetricCapture : IDisposable
{
    private readonly MeterListener _listener = new();
    private readonly ConcurrentDictionary<string, int> _counts =
        new(StringComparer.Ordinal);

    internal HarnessMetricCapture(string meterName)
    {
        _listener.InstrumentPublished = (instrument, listener) =>
        {
            if (string.Equals(
                instrument.Meter.Name,
                meterName,
                StringComparison.Ordinal))
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };
        _listener.SetMeasurementEventCallback<long>(
            (instrument, _, _, _) => Increment(instrument.Name));
        _listener.SetMeasurementEventCallback<double>(
            (instrument, _, _, _) => Increment(instrument.Name));
        _listener.Start();
    }

    internal int Count(string instrumentName) =>
        _counts.TryGetValue(instrumentName, out var count) ? count : 0;

    public void Dispose() => _listener.Dispose();

    private void Increment(string instrumentName) =>
        _counts.AddOrUpdate(instrumentName, 1, static (_, count) => count + 1);
}
