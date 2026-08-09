using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AutonomousPhasePipelineApp.Core;

internal sealed class IdempotentDeliverySink
{
    private readonly ConcurrentDictionary<string, ReferencePipelineResult> _deliveries =
        new(StringComparer.Ordinal);
    private int _attemptCount;
    private int _authoritativeCount;

    internal int AttemptCount => Volatile.Read(ref _attemptCount);

    internal int AuthoritativeCount => Volatile.Read(ref _authoritativeCount);

    internal ReferencePipelineResult Publish(
        ReferencePipelineResult candidate)
    {
        Interlocked.Increment(ref _attemptCount);
        if (_deliveries.TryAdd(candidate.RunId, candidate))
        {
            Interlocked.Increment(ref _authoritativeCount);
            return candidate;
        }

        ReferencePipelineResult authoritative = _deliveries[candidate.RunId];
        if (!string.Equals(
            ComputeDigest(authoritative),
            ComputeDigest(candidate),
            StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Delivery replay for run '{candidate.RunId}' carried a conflicting result.");
        }

        return authoritative;
    }

    private static string ComputeDigest(
        ReferencePipelineResult result)
    {
        byte[] payload = Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(result));
        return Convert
            .ToHexString(SHA256.HashData(payload))
            .ToLowerInvariant();
    }
}
