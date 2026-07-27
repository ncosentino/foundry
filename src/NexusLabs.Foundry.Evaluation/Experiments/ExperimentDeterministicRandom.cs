namespace NexusLabs.Foundry.Evaluation.Experiments;

internal sealed class ExperimentDeterministicRandom
{
    private ulong _state;

    public ExperimentDeterministicRandom(ulong seed)
    {
        _state = seed;
    }

    public int NextInt32(int maxExclusive)
    {
        if (maxExclusive <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxExclusive),
                maxExclusive,
                "The exclusive upper bound must be positive.");
        }

        var bound = (ulong)maxExclusive;
        var threshold = unchecked(0UL - bound) % bound;
        while (true)
        {
            var value = NextUInt64();
            if (value >= threshold)
            {
                return (int)(value % bound);
            }
        }
    }

    private ulong NextUInt64()
    {
        _state += 0x9E3779B97F4A7C15UL;
        var z = _state;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }
}
