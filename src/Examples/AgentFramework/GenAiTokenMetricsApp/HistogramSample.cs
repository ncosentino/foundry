using NexusLabs.Needlr;

namespace GenAiTokenMetricsApp;

[DoNotAutoRegister]
internal sealed record HistogramSample(
    int Value,
    Dictionary<string, object?> Tags);
